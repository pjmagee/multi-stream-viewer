using System.Text.Json;
using Microsoft.JSInterop;
using MultiStreamViewer.Models;

namespace MultiStreamViewer.Services;

/// <summary>
/// "Watch together" session sync. Wraps the PeerJS module and keeps the local
/// <see cref="StreamService"/> stream list in lockstep with every participant:
/// any local add/remove/replace is broadcast as a snapshot, and incoming
/// snapshots are applied locally (echo-suppressed so they don't bounce back).
/// Per-stream audio/playback is intentionally NOT synced — each viewer controls
/// their own. The active session is persisted to localStorage so a page refresh
/// rejoins the same room instead of dropping out.
/// </summary>
public class SyncService : IAsyncDisposable
{
    private const string SessionStorageKey = "msv-session";

    private readonly StreamService _streams;
    private readonly IJSRuntime _js;

    private IJSObjectReference? _module;
    private DotNetObjectReference<SyncService>? _ref;
    private bool _applyingRemote;

    public bool IsConnected { get; private set; }
    public bool IsHost { get; private set; }
    public string? SessionId { get; private set; }
    public int PeerCount { get; private set; }
    public string? LastError { get; private set; }

    public event Action? StateChanged;

    public SyncService(StreamService streams, IJSRuntime js)
    {
        _streams = streams;
        _js = js;
        _streams.StreamsChanged += OnLocalChanged;
    }

    public async Task StartSessionAsync(string? preferredId = null)
    {
        await EnsureModuleAsync();
        IsHost = true;
        LastError = null;
        SessionId = await _module!.InvokeAsync<string>("startSession", _ref, preferredId);
        IsConnected = true;
        await PersistSessionAsync();
        StateChanged?.Invoke();
    }

    public async Task JoinSessionAsync(string hostId)
    {
        if (string.IsNullOrWhiteSpace(hostId))
        {
            return;
        }

        await EnsureModuleAsync();
        IsHost = false;
        LastError = null;
        SessionId = hostId.Trim();
        await PersistSessionAsync();
        // IsConnected flips true once the data channel to the host actually
        // opens (reported via OnPeerCountChanged), not just when dialing starts.
        await _module!.InvokeAsync<string>("joinSession", SessionId, _ref);
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Re-establishes a session saved in localStorage (called on page load).
    /// Hosts reclaim their previous id; guests redial the same host.
    /// </summary>
    public async Task TryRestoreSessionAsync()
    {
        if (IsConnected)
        {
            return;
        }

        StoredSession? stored;
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", SessionStorageKey);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            stored = JsonSerializer.Deserialize<StoredSession>(raw);
        }
        catch
        {
            return;
        }

        if (stored is null || string.IsNullOrWhiteSpace(stored.Id))
        {
            await ClearStoredSessionAsync();
            return;
        }

        try
        {
            if (stored.Host)
            {
                await StartSessionAsync(stored.Id);
            }
            else
            {
                await JoinSessionAsync(stored.Id);
            }
        }
        catch
        {
            await ClearStoredSessionAsync();
        }
    }

    public async Task LeaveSessionAsync()
    {
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("leave"); } catch { /* tearing down */ }
        }

        await ClearStoredSessionAsync();

        IsConnected = false;
        IsHost = false;
        SessionId = null;
        PeerCount = 0;
        LastError = null;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public string GetCurrentSnapshot() => BuildSnapshotJson();

    [JSInvokable]
    public void OnSnapshotReceived(string json)
    {
        SyncSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<SyncSnapshot>(json);
        }
        catch
        {
            return;
        }

        if (snapshot is null)
        {
            return;
        }

        _applyingRemote = true;
        try
        {
            var incoming = snapshot.Streams
                .Select(s => (
                    s.Id,
                    Enum.TryParse<StreamPlatform>(s.Platform, out var platform) ? platform : StreamPlatform.Twitch,
                    s.Name))
                .ToList();

            _streams.ApplySyncedStreams(incoming);
        }
        finally
        {
            _applyingRemote = false;
        }
    }

    [JSInvokable]
    public void OnPeerCountChanged(int count)
    {
        PeerCount = count;

        // A host stays "connected" while hosting even with no guests; a guest is
        // connected only while it has a live channel to the host.
        if (!IsHost)
        {
            IsConnected = count > 0;
        }

        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnSyncError(string error)
    {
        LastError = error;
        StateChanged?.Invoke();
    }

    /// <summary>
    /// The guest exhausted its reconnection attempts — the room is gone. Drop the
    /// persisted session so we stop trying to rejoin a dead room on each refresh.
    /// </summary>
    [JSInvokable]
    public void OnReconnectFailed()
    {
        IsConnected = false;
        SessionId = null;
        LastError = "Session ended";
        _ = ClearStoredSessionAsync();
        StateChanged?.Invoke();
    }

    private async void OnLocalChanged()
    {
        if (_applyingRemote || !IsConnected || _module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("broadcast", BuildSnapshotJson());
        }
        catch
        {
            // Connection may be mid-teardown; the next change re-syncs.
        }
    }

    private async Task EnsureModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/peer-sync.js");
        _ref ??= DotNetObjectReference.Create(this);
    }

    private string BuildSnapshotJson()
    {
        var snapshot = new SyncSnapshot(
            _streams.Streams
                .Select(s => new SyncStream(s.Id, s.Platform.ToString(), s.StreamerName))
                .ToList());

        return JsonSerializer.Serialize(snapshot);
    }

    private async Task PersistSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionId))
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(new StoredSession(SessionId, IsHost));
            await _js.InvokeVoidAsync("localStorage.setItem", SessionStorageKey, json);
        }
        catch
        {
            // localStorage may be unavailable (private mode); sync still works for this tab.
        }
    }

    private async Task ClearStoredSessionAsync()
    {
        try { await _js.InvokeVoidAsync("localStorage.removeItem", SessionStorageKey); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        _streams.StreamsChanged -= OnLocalChanged;

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("leave");
                await _module.DisposeAsync();
            }
            catch
            {
                // Ignore teardown failures.
            }
        }

        _ref?.Dispose();
    }

    private sealed record SyncSnapshot(List<SyncStream> Streams);
    private sealed record SyncStream(string Id, string Platform, string Name);
    private sealed record StoredSession(string Id, bool Host);
}
