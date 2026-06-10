using Microsoft.AspNetCore.Components;
using MultiStreamViewer.Models;
using System.Collections.ObjectModel;

namespace MultiStreamViewer.Services;

public class StreamService
{
    public ObservableCollection<StreamInfo> Streams { get; } = new();
    public ChatDisplayMode ChatMode { get; set; } = ChatDisplayMode.Pane;
    public ChatPanePosition ChatPosition { get; set; } = ChatPanePosition.Right;
    public bool IsChatPaneVisible { get; set; } = false;

    public event Action? StreamsChanged;
    public event Action? ChatSettingsChanged;
    public event Action? ManageDialogRequested;

    public StreamService(NavigationManager navigationManager)
    {
        InitializeEmbedDomain(navigationManager);
    }

    public void AddStream(StreamPlatform platform, string streamerName)
    {
        if (string.IsNullOrWhiteSpace(streamerName))
            return;

        var stream = new StreamInfo(platform, streamerName);
        Streams.Add(stream);
        StreamsChanged?.Invoke();
    }

    public void RemoveStream(string streamId)
    {
        var stream = Streams.FirstOrDefault(s => s.Id == streamId);
        if (stream != null)
        {
            Streams.Remove(stream);
            StreamsChanged?.Invoke();
        }
    }

    public void ReplaceStream(string streamId, StreamPlatform platform, string streamerName)
    {
        if (string.IsNullOrWhiteSpace(streamerName))
            return;

        var stream = Streams.FirstOrDefault(s => s.Id == streamId);
        if (stream != null)
        {
            stream.Update(platform, streamerName);
            StreamsChanged?.Invoke();
        }
    }

    /// <summary>
    /// Reconciles the local stream list to match a synced snapshot, preserving
    /// existing <see cref="StreamInfo"/> instances (and their embeds) for streams
    /// that are unchanged so they don't reload. Streams are matched by Id, which
    /// is shared across the session.
    /// </summary>
    public void ApplySyncedStreams(IReadOnlyList<(string Id, StreamPlatform Platform, string Name)> snapshot)
    {
        var existing = Streams.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var reconciled = new List<StreamInfo>(snapshot.Count);

        foreach (var (id, platform, name) in snapshot)
        {
            if (existing.TryGetValue(id, out var stream))
            {
                if (stream.Platform != platform
                    || !string.Equals(stream.StreamerName, name, StringComparison.OrdinalIgnoreCase))
                {
                    stream.Update(platform, name);
                }

                reconciled.Add(stream);
            }
            else
            {
                reconciled.Add(new StreamInfo(platform, name) { Id = id });
            }
        }

        Streams.Clear();
        foreach (var stream in reconciled)
        {
            Streams.Add(stream);
        }

        StreamsChanged?.Invoke();
    }

    public void SetChatMode(ChatDisplayMode mode)
    {
        ChatMode = mode;
        ChatSettingsChanged?.Invoke();
    }

    public void SetChatPosition(ChatPanePosition position)
    {
        ChatPosition = position;
        ChatSettingsChanged?.Invoke();
    }

    public void ToggleChatPane()
    {
        IsChatPaneVisible = !IsChatPaneVisible;
        ChatSettingsChanged?.Invoke();
    }

    public void LoadStreamsFromUrl(string url)
    {
        // Clear existing streams
        Streams.Clear();

        // Parse URL segments
        var uri = new Uri(url);
        var segments = uri.Segments.Skip(1).Select(s => s.TrimEnd('/')).ToArray();

        for (int i = 0; i < segments.Length - 1; i += 2)
        {
            var platformName = segments[i];
            var streamerName = segments[i + 1];

            if (Enum.TryParse<StreamPlatform>(platformName, true, out var platform))
            {
                AddStream(platform, streamerName);
            }
        }
    }

    public string GenerateShareUrl(string baseUrl)
    {
        var segments = new List<string>();
        foreach (var stream in Streams)
        {
            segments.Add(stream.Platform.ToString().ToLower());
            segments.Add(stream.StreamerName);
        }

        return $"{baseUrl.TrimEnd('/')}/{string.Join("/", segments)}";
    }

    public void SetStreams(List<StreamInfo> streams)
    {
        Streams.Clear();
        foreach (var stream in streams)
        {
            Streams.Add(stream);
        }
        StreamsChanged?.Invoke();
    }

    public void ClearAllStreams()
    {
        Streams.Clear();
        StreamsChanged?.Invoke();
    }

    public void TriggerManageDialog()
    {
        ManageDialogRequested?.Invoke();
    }

    private static void InitializeEmbedDomain(NavigationManager navigationManager)
    {
        try
        {
            var host = new Uri(navigationManager.BaseUri).Host;
            StreamInfo.ConfigureEmbedDomain(host);
        }
        catch (UriFormatException)
        {
            StreamInfo.ConfigureEmbedDomain("localhost");
        }
    }
}
