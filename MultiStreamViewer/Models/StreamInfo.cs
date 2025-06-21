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
    }

    /// <summary>
    /// Gets the appropriate embed URL for the streaming platform
    /// 
    /// Platform Requirements:
    /// - Twitch: streamerName is the channel name (e.g., "shroud")
    /// - YouTube: streamerName should be the Video ID (e.g., "dQw4w9WgXcQ") NOT channel name
    /// - Kick: streamerName is the channel name (e.g., "trainwreckstv")
    /// 
    /// Note: For YouTube, users must provide the specific video ID of the live stream,
    /// not the channel name. This follows YouTube's embed requirements.
    /// </summary>
    private static string GetEmbedUrl(StreamPlatform platform, string streamerName)
    {
        return platform switch
        {
            StreamPlatform.Twitch => $"https://player.twitch.tv/?channel={streamerName}&parent=localhost&parent=127.0.0.1&parent=github.io&parent=pages.dev&autoplay=false&muted=false",
            StreamPlatform.YouTube => $"https://www.youtube.com/embed/{streamerName}?autoplay=0&allow=autoplay; encrypted-media",
            StreamPlatform.Kick => $"https://player.kick.com/{streamerName}?autoplay=false",
            _ => string.Empty
        };
    }

    private static string GetChatUrl(StreamPlatform platform, string streamerName)
    {
        return platform switch
        {
            StreamPlatform.Twitch => $"https://www.twitch.tv/embed/{streamerName}/chat?parent=localhost&parent=127.0.0.1&parent=github.io&parent=pages.dev",
            StreamPlatform.YouTube => GetYouTubeChatUrl(streamerName),
            StreamPlatform.Kick => $"https://kick.com/{streamerName}/chatroom",
            _ => string.Empty
        };
    }

    private static string GetYouTubeChatUrl(string videoId)
    {
        // For YouTube chat, we need to dynamically determine the embed_domain
        // Based on the current hosting environment following YouTube's requirements

        // Determine embed domain based on common deployment scenarios
        var embedDomain = GetEmbedDomain();

        return $"https://www.youtube.com/live_chat?v={videoId}&embed_domain={embedDomain}";
    }

    /// <summary>
    /// Determines the appropriate embed domain for YouTube chat based on deployment environment
    /// This is required by YouTube's embed policies
    /// </summary>
    private static string GetEmbedDomain()
    {
        // For GitHub Pages deployment: 
        // This should be "pjmagee.github.io" based on your README
        // For local development: "localhost"
        // For custom domains: the actual domain
        
        // Since this is a static method, we'll set reasonable defaults
        // In a real application, this could be injected from configuration
        
        #if DEBUG
            return "localhost";
        #else
            return "pjmagee.github.io"; // Update this for your GitHub Pages domain
        #endif
    }
}
