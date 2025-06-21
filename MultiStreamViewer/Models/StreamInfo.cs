namespace MultiStreamViewer.Models;

public enum StreamPlatform
{
    Twitch,
    YouTube,
    Kick
}

public enum LayoutMode
{
    Grid,
    Stacked,
    Horizontal
}

public enum ChatDisplayMode
{
    Pane,
    Attached
}

public enum ChatPanePosition
{
    Left,
    Right
}

public class StreamInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public StreamPlatform Platform { get; set; }
    public string StreamerName { get; set; } = string.Empty;
    public string EmbedUrl { get; set; } = string.Empty;
    public string ChatUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public StreamInfo() { }

    public StreamInfo(StreamPlatform platform, string streamerName)
    {
        Platform = platform;
        StreamerName = streamerName;
        EmbedUrl = GetEmbedUrl(platform, streamerName);
        ChatUrl = GetChatUrl(platform, streamerName);
    }    private static string GetEmbedUrl(StreamPlatform platform, string streamerName)
    {
        return platform switch
        {
            StreamPlatform.Twitch => $"https://player.twitch.tv/?channel={streamerName}&parent=localhost&parent=127.0.0.1&parent=github.io&parent=pages.dev&autoplay=false&muted=false",
            StreamPlatform.YouTube => $"https://www.youtube.com/embed/live_stream?channel={streamerName}&autoplay=0",
            StreamPlatform.Kick => $"https://player.kick.com/{streamerName}?autoplay=false",
            _ => string.Empty
        };
    }    private static string GetChatUrl(StreamPlatform platform, string streamerName)
    {
        return platform switch
        {
            StreamPlatform.Twitch => $"https://www.twitch.tv/embed/{streamerName}/chat?parent=localhost&parent=127.0.0.1&parent=github.io&parent=pages.dev",
            StreamPlatform.YouTube => $"https://www.youtube.com/live_chat?v={streamerName}&embed_domain=localhost",
            StreamPlatform.Kick => $"https://kick.com/{streamerName}/chatroom",
            _ => string.Empty
        };
    }
}
