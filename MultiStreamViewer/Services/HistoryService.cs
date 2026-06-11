using System.Text.Json;
using Microsoft.JSInterop;
using MultiStreamViewer.Models;

namespace MultiStreamViewer.Services;

/// <summary>
/// Remembers, in localStorage, the stream-sets and watch-together sessions the
/// user has opened so they can be relaunched from a fresh page load.
///
///  - Stream-sets are recorded automatically (deduped by a channel signature)
///    whenever the open set settles, so re-opening the same channels just bumps
///    recency rather than creating a duplicate.
///  - Sessions are recorded whenever one is started or joined, together with a
///    snapshot of the streams being watched so the room can be re-created later.
///
/// Entries can be pinned (kept regardless of age) and given a custom label.
/// Recording is debounced so loading several streams in quick succession (e.g.
/// from a URL) records the final set once instead of every intermediate state.
/// </summary>
public class HistoryService : IAsyncDisposable
{
    private const string StreamSetsKey = "msv-stream-sets";
    private const string SessionsKey = "msv-sessions";
    private const int MaxStreamSets = 15;
    private const int MaxSessions = 10;
    private static readonly TimeSpan RecordDebounce = TimeSpan.FromMilliseconds(900);

    private readonly StreamService _streams;
    private readonly SyncService _sync;
    private readonly IJSRuntime _js;

    private List<StreamSetEntry> _streamSets = new();
    private List<SessionEntry> _sessions = new();
    private Task? _loadTask;

    private CancellationTokenSource? _debounceCts;
    private string? _recordedSession;

    public event Action? Changed;

    public HistoryService(StreamService streams, SyncService sync, IJSRuntime js)
    {
        _streams = streams;
        _sync = sync;
        _js = js;
        _streams.StreamsChanged += OnStreamsChanged;
        _sync.StateChanged += OnSyncStateChanged;
    }

    public IReadOnlyList<StreamSetEntry> StreamSets => _streamSets;
    public IReadOnlyList<SessionEntry> Sessions => _sessions;

    /// <summary>Loads persisted history once; concurrent callers share the same load.</summary>
    public Task EnsureLoadedAsync() => _loadTask ??= LoadAllAsync();

    private async Task LoadAllAsync()
    {
        _streamSets = await LoadAsync<StreamSetEntry>(StreamSetsKey);
        _sessions = await LoadAsync<SessionEntry>(SessionsKey);
        Normalize();
        Changed?.Invoke();
    }

    // --- UI-driven actions ------------------------------------------------

    /// <summary>Replaces the open streams with a saved set (recency bumps via the recorder).</summary>
    public Task LoadStreamSetAsync(StreamSetEntry entry) => LoadStreamsAsync(entry.Streams);

    /// <summary>Loads an arbitrary list of saved stream references into the viewer.</summary>
    public Task LoadStreamsAsync(IEnumerable<StreamRef> streams)
    {
        var infos = new List<StreamInfo>();
        foreach (var reference in streams)
        {
            if (Enum.TryParse<StreamPlatform>(reference.Platform, true, out var platform)
                && !string.IsNullOrWhiteSpace(reference.Name))
            {
                infos.Add(new StreamInfo(platform, reference.Name));
            }
        }

        _streams.SetStreams(infos);
        return Task.CompletedTask;
    }

    public async Task RemoveStreamSetAsync(string id)
    {
        await EnsureLoadedAsync();
        if (_streamSets.RemoveAll(e => e.Id == id) > 0)
        {
            await PersistStreamSetsAsync();
            Changed?.Invoke();
        }
    }

    public async Task RenameStreamSetAsync(string id, string? label)
    {
        await EnsureLoadedAsync();
        var entry = _streamSets.FirstOrDefault(e => e.Id == id);
        if (entry is null)
        {
            return;
        }

        entry.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        await PersistStreamSetsAsync();
        Changed?.Invoke();
    }

    public async Task ToggleStreamSetPinAsync(string id)
    {
        await EnsureLoadedAsync();
        var entry = _streamSets.FirstOrDefault(e => e.Id == id);
        if (entry is null)
        {
            return;
        }

        entry.Pinned = !entry.Pinned;
        Normalize();
        await PersistStreamSetsAsync();
        Changed?.Invoke();
    }

    public async Task RemoveSessionAsync(string id)
    {
        await EnsureLoadedAsync();
        if (_sessions.RemoveAll(e => e.Id == id) > 0)
        {
            await PersistSessionsAsync();
            Changed?.Invoke();
        }
    }

    public async Task RenameSessionAsync(string id, string? label)
    {
        await EnsureLoadedAsync();
        var entry = _sessions.FirstOrDefault(e => e.Id == id);
        if (entry is null)
        {
            return;
        }

        entry.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        await PersistSessionsAsync();
        Changed?.Invoke();
    }

    public async Task ToggleSessionPinAsync(string id)
    {
        await EnsureLoadedAsync();
        var entry = _sessions.FirstOrDefault(e => e.Id == id);
        if (entry is null)
        {
            return;
        }

        entry.Pinned = !entry.Pinned;
        Normalize();
        await PersistSessionsAsync();
        Changed?.Invoke();
    }

    // --- Automatic recording ----------------------------------------------

    private void OnStreamsChanged()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        _ = RecordAfterDelayAsync(_debounceCts.Token);
    }

    private async Task RecordAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(RecordDebounce, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await EnsureLoadedAsync();

        var snapshot = SnapshotCurrentStreams();
        if (snapshot.Count == 0)
        {
            // Don't record the empty state; just leave history as-is.
            return;
        }

        UpsertStreamSet(snapshot);

        // Keep the active session's snapshot in step with what's on screen.
        if (_sync.IsConnected && !string.IsNullOrWhiteSpace(_sync.SessionId))
        {
            UpsertSession(_sync.SessionId!, _sync.IsHost ? "Host" : "Guest", snapshot);
            await PersistSessionsAsync();
        }

        await PersistStreamSetsAsync();
        Changed?.Invoke();
    }

    private async void OnSyncStateChanged()
    {
        if (!_sync.IsConnected || string.IsNullOrWhiteSpace(_sync.SessionId))
        {
            // Allow a future (re)connection to record/bump again.
            _recordedSession = null;
            return;
        }

        var role = _sync.IsHost ? "Host" : "Guest";
        var marker = $"{_sync.SessionId}:{role}";
        if (marker == _recordedSession)
        {
            return;
        }

        _recordedSession = marker;

        try
        {
            await EnsureLoadedAsync();
            UpsertSession(_sync.SessionId!, role, SnapshotCurrentStreams());
            await PersistSessionsAsync();
            Changed?.Invoke();
        }
        catch
        {
            // Recording is best-effort; never disrupt the session over it.
        }
    }

    private List<StreamRef> SnapshotCurrentStreams() =>
        _streams.Streams
            .Select(s => new StreamRef(s.Platform.ToString(), s.StreamerName))
            .ToList();

    private void UpsertStreamSet(List<StreamRef> streams)
    {
        var signature = Signature(streams);
        var existing = _streamSets.FirstOrDefault(e => e.Id == signature);
        if (existing is not null)
        {
            existing.Streams = streams;
            existing.LastOpened = Now();
        }
        else
        {
            _streamSets.Add(new StreamSetEntry
            {
                Id = signature,
                Streams = streams,
                LastOpened = Now(),
            });
        }

        Normalize();
    }

    private void UpsertSession(string id, string role, List<StreamRef> streams)
    {
        var existing = _sessions.FirstOrDefault(e => e.Id == id);
        if (existing is not null)
        {
            existing.Role = role;
            if (streams.Count > 0)
            {
                existing.Streams = streams;
            }

            existing.LastUsed = Now();
        }
        else
        {
            _sessions.Add(new SessionEntry
            {
                Id = id,
                Role = role,
                Streams = streams,
                LastUsed = Now(),
            });
        }

        Normalize();
    }

    // --- Persistence & housekeeping ---------------------------------------

    private void Normalize()
    {
        _streamSets = Cap(_streamSets, MaxStreamSets, e => e.Pinned, e => e.LastOpened);
        _sessions = Cap(_sessions, MaxSessions, e => e.Pinned, e => e.LastUsed);
    }

    // Pinned entries are always kept; the remaining slots go to the most recent.
    // Result is ordered for display: pinned first, then most-recent-first.
    private static List<T> Cap<T>(List<T> list, int max, Func<T, bool> pinned, Func<T, long> recency)
    {
        var pins = list.Where(pinned).OrderByDescending(recency).ToList();
        var rest = list.Where(e => !pinned(e))
            .OrderByDescending(recency)
            .Take(Math.Max(0, max - pins.Count))
            .ToList();

        return pins.Concat(rest).ToList();
    }

    private async Task<List<T>> LoadAsync<T>(string key)
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", key);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<T>();
            }

            return JsonSerializer.Deserialize<List<T>>(raw) ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }

    private async Task PersistStreamSetsAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StreamSetsKey, JsonSerializer.Serialize(_streamSets));
        }
        catch
        {
            // localStorage may be unavailable (private mode); history is best-effort.
        }
    }

    private async Task PersistSessionsAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", SessionsKey, JsonSerializer.Serialize(_sessions));
        }
        catch
        {
            // localStorage may be unavailable (private mode); history is best-effort.
        }
    }

    // Order-independent identity for a set of channels, so {a,b} == {b,a}.
    private static string Signature(IEnumerable<StreamRef> streams) =>
        string.Join("|", streams
            .Select(s => $"{s.Platform.ToLowerInvariant()}:{s.Name.ToLowerInvariant()}")
            .OrderBy(s => s, StringComparer.Ordinal));

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public ValueTask DisposeAsync()
    {
        _streams.StreamsChanged -= OnStreamsChanged;
        _sync.StateChanged -= OnSyncStateChanged;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>A single channel reference (platform + channel/video identifier).</summary>
public sealed class StreamRef
{
    public string Platform { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public StreamRef()
    {
    }

    public StreamRef(string platform, string name)
    {
        Platform = platform;
        Name = name;
    }
}

/// <summary>A remembered combination of streams the user has opened.</summary>
public sealed class StreamSetEntry
{
    public string Id { get; set; } = string.Empty;
    public List<StreamRef> Streams { get; set; } = new();
    public string? Label { get; set; }
    public bool Pinned { get; set; }
    public long LastOpened { get; set; }
}

/// <summary>A remembered watch-together session and the streams it was watching.</summary>
public sealed class SessionEntry
{
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = "Guest";
    public List<StreamRef> Streams { get; set; } = new();
    public string? Label { get; set; }
    public bool Pinned { get; set; }
    public long LastUsed { get; set; }
}
