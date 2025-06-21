using MultiStreamViewer.Models;
using System.Collections.ObjectModel;

namespace MultiStreamViewer.Services;

public class StreamService
{
    public ObservableCollection<StreamInfo> Streams { get; } = new();
    public LayoutMode CurrentLayout { get; set; } = LayoutMode.Grid;
    public ChatDisplayMode ChatMode { get; set; } = ChatDisplayMode.Pane;
    public ChatPanePosition ChatPosition { get; set; } = ChatPanePosition.Right;
    public bool IsChatPaneVisible { get; set; } = false;

    public event Action? StreamsChanged;
    public event Action? LayoutChanged;
    public event Action? ChatSettingsChanged;

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

    public void SetLayout(LayoutMode layout)
    {
        CurrentLayout = layout;
        LayoutChanged?.Invoke();
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
}
