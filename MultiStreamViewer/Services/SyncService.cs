using System.Text.Json;
using Microsoft.JSInterop;
using MultiStreamViewer.Models;

namespace MultiStreamViewer.Services;

/// <summary>
/// "Watch together" session sync. Wraps the PeerJS module and keeps every
/// participant in lockstep:
///  - the stream set (add/remove/replace) is broadcast as a snapshot, and
///  - YouTube playback (play/pause/seek) is synced via the IFrame Player API,
///    collaboratively — anyone's action applies to everyone, and the host emits
///    a periodic drift-correction heartbeat.
/// Per-stream audio/volume is intentionally NOT synced. The active session is
/// persisted to localStorage so a page refresh rejoins the same room.
/// </summary>
public class SyncService : IAsyncDisposable
{
    private const string SessionStorageKey = "msv-session";

    private readonly StreamService _streams;
    private readonly IJSRuntime _js;

    private IJSObjectReference? _module;
    private IJSObjectReference? _ytModule;
    private DotNetObjectReference<SyncService>? _ref;
    private CancellationTokenSource? _heartbeatCts;
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
        _streams.StreamsChanged += OnLocalStreamsChanged;
    }

    public async Task StartSessionAsync(string? preferredId = null)
    {
        await EnsureModuleAsync();
        IsHost = true;
        LastError = null;
        SessionId = await _module!.InvokeAsync<string>("startSession", _ref, preferredId);
        IsConnected = true;
        await PersistSessionAsync();
        StartHeartbeat();
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Checks whether a saved session's room is still reachable, without joining
    /// it. Used by the "relaunch from history" flow to decide between rejoining a
    /// live room and offering to re-create a dead one. Returns false if the room
    /// is gone or the check times out.
    /// </summary>
    public async Task<bool> ProbeSessionAsync(string hostId)
    {
        if (string.IsNullOrWhiteSpace(hostId))
        {
            return false;
        }

        await EnsureModuleAsync();
        try
        {
            return await _module!.InvokeAsync<bool>("probeHost", hostId.Trim(), 5000);
        }
        catch
        {
            return false;
        }
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
        StopHeartbeat();

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

    /// <summary>Attaches a YouTube IFrame player so its playback can be synced.</summary>
    public async Task RegisterYouTubeAsync(string elementId, string streamId)
    {
        await EnsureYtModuleAsync();
        try { await _ytModule!.InvokeVoidAsync("register", elementId, streamId, _ref); } catch { }
    }

    public async Task UnregisterYouTubeAsync(string elementId)
    {
        if (_ytModule is not null)
        {
            try { await _ytModule.InvokeVoidAsync("unregister", elementId); } catch { }
        }
    }

    [JSInvokable]
    public string GetCurrentSnapshot() => BuildSnapshotJson();

    [JSInvokable]
    public async Task OnMessageReceived(string json)
    {
        string? type;
        try
        {
            using var doc = JsonDocument.Parse(json);
            type = doc.RootElement.TryGetProperty("Type", out var t) ? t.GetString() : null;
        }
        catch
        {
            return;
        }

        if (type == "yt")
        {
            await ApplyYouTubeMessageAsync(json);
        }
        else
        {
            ApplyStreamsMessage(json);
        }
    }

    [JSInvokable]
    public async Task OnYouTubePlayback(string streamId, bool playing, double time, long version)
    {
        if (!IsConnected || _module is null)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(new YtMessage("yt", streamId, playing, time, version));
            await _module.InvokeVoidAsync("broadcast", json);
        }
        catch
        {
            // Connection mid-teardown; the next event re-syncs.
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

    private async Task ApplyYouTubeMessageAsync(string json)
    {
        YtMessage? message;
        try { message = JsonSerializer.Deserialize<YtMessage>(json); } catch { return; }
        if (message is null)
        {
            return;
        }

        await EnsureYtModuleAsync();
        try
        {
            await _ytModule!.InvokeVoidAsync("applyMessage", message.StreamId, message.Playing, message.Time, message.Version);
        }
        catch { }
    }

    private void ApplyStreamsMessage(string json)
    {
        StreamsMessage? message;
        try { message = JsonSerializer.Deserialize<StreamsMessage>(json); } catch { return; }
        if (message is null)
        {
            return;
        }

        _applyingRemote = true;
        try
        {
            var incoming = message.Streams
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

    private async void OnLocalStreamsChanged()
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

    private void StartHeartbeat()
    {
        StopHeartbeat();
        _heartbeatCts = new CancellationTokenSource();
        _ = HeartbeatLoopAsync(_heartbeatCts.Token);
    }

    private void StopHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
    }

    // Host-only: periodically broadcast each YouTube player's position so guests
    // self-correct drift and late-joiners snap to the right spot.
    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
            while (await timer.WaitForNextTickAsync(ct))
            {
                if (!IsHost || !IsConnected || _ytModule is null || _module is null)
                {
                    continue;
                }

                try
                {
                    var states = await _ytModule.InvokeAsync<YtState[]>("getStates");
                    foreach (var state in states)
                    {
                        var json = JsonSerializer.Serialize(new YtMessage("yt", state.StreamId, state.Playing, state.Time, state.Version));
                        await _module.InvokeVoidAsync("broadcast", json);
                    }
                }
                catch { }
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task EnsureModuleAsync()
    {
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/peer-sync.js");
        _ref ??= DotNetObjectReference.Create(this);
    }

    private async Task EnsureYtModuleAsync()
    {
        _ytModule ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/youtube-sync.js");
        _ref ??= DotNetObjectReference.Create(this);
    }

    private string BuildSnapshotJson()
    {
        var message = new StreamsMessage(
            "streams",
            _streams.Streams
                .Select(s => new SyncStream(s.Id, s.Platform.ToString(), s.StreamerName))
                .ToList());

        return JsonSerializer.Serialize(message);
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
        _streams.StreamsChanged -= OnLocalStreamsChanged;
        StopHeartbeat();

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("leave");
                await _module.DisposeAsync();
            }
            catch { /* Ignore teardown failures. */ }
        }

        if (_ytModule is not null)
        {
            try { await _ytModule.DisposeAsync(); } catch { }
        }

        _ref?.Dispose();
    }

    private sealed record StreamsMessage(string Type, List<SyncStream> Streams);
    private sealed record SyncStream(string Id, string Platform, string Name);
    private sealed record YtMessage(string Type, string StreamId, bool Playing, double Time, long Version);
    private sealed record YtState(string StreamId, bool Playing, double Time, long Version);
    private sealed record StoredSession(string Id, bool Host);
}
