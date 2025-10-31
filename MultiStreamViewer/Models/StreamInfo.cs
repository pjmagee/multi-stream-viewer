namespace MultiStreamViewer.Models;

public enum StreamPlatform
{
    Twitch,
    YouTube,
    Kick
}

public enum LayoutMode
{
    Grid
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
    private static readonly object _embedDomainLock = new();
    private static string? _runtimeEmbedDomain;
    private static HashSet<string>? _twitchParentHosts;
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public StreamPlatform Platform { get; set; }
    public string StreamerName { get; set; } = string.Empty;
    public string EmbedUrl { get; set; } = string.Empty;
    public string ChatUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public bool IsTwitch => Platform == StreamPlatform.Twitch;
    public bool IsYouTube => Platform == StreamPlatform.YouTube;
    public bool IsKick => Platform == StreamPlatform.Kick;

    public string VideoTitle => string.IsNullOrWhiteSpace(StreamerName)
        ? $"{Platform} stream"
        : $"{Platform} stream: {StreamerName}";

    public string ChatTitle => string.IsNullOrWhiteSpace(StreamerName)
        ? $"{Platform} chat"
        : $"{Platform} chat: {StreamerName}";

    public string? VideoPermissions => Platform switch
    {
        StreamPlatform.Twitch => "autoplay; encrypted-media; fullscreen; picture-in-picture",
        StreamPlatform.YouTube => "accelerometer *; clipboard-write *; encrypted-media *; gyroscope *; picture-in-picture *; web-share *;",
        StreamPlatform.Kick => null,
        _ => null
    };

    public bool AllowFullscreen => Platform is StreamPlatform.Twitch or StreamPlatform.YouTube or StreamPlatform.Kick;

    public string? VideoReferrerPolicy => Platform switch
    {
        StreamPlatform.YouTube => "strict-origin-when-cross-origin",
        _ => null
    };

    public string? ChatReferrerPolicy => Platform switch
    {
        StreamPlatform.YouTube => "strict-origin-when-cross-origin",
        _ => null
    };

    public StreamInfo() { }

    public StreamInfo(StreamPlatform platform, string streamerName)
    {
        Platform = platform;
        StreamerName = streamerName;
        EmbedUrl = GetEmbedUrl(platform, streamerName);
        ChatUrl = GetChatUrl(platform, streamerName);
    }

    public static void ConfigureEmbedDomain(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return;
        }

        var normalizedHost = host.Trim().ToLowerInvariant();

        lock (_embedDomainLock)
        {
            _runtimeEmbedDomain = normalizedHost;
            _twitchParentHosts = BuildParentHostSet(normalizedHost);
        }
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
            StreamPlatform.Twitch => GetTwitchEmbedUrl(streamerName),
            StreamPlatform.YouTube => $"https://www.youtube.com/embed/{streamerName}?rel=0",
            StreamPlatform.Kick => $"https://player.kick.com/{streamerName}?autoplay=false&muted=false",
            _ => string.Empty
        };
    }

    private static string GetTwitchEmbedUrl(string streamerName)
    {
        var parentQuery = BuildTwitchParentQueryString();
        return $"https://player.twitch.tv/?muted=true&channel={streamerName}&{parentQuery}&autoplay=false";
    }

    private static string GetChatUrl(StreamPlatform platform, string streamerName)
    {
        return platform switch
        {
            StreamPlatform.Twitch => GetTwitchChatUrl(streamerName),
            StreamPlatform.YouTube => GetYouTubeChatUrl(streamerName),
            StreamPlatform.Kick => $"https://kick.com/{streamerName}/chatroom",
            _ => string.Empty
        };
    }

    private static string GetTwitchChatUrl(string streamerName)
    {
        var parentQuery = BuildTwitchParentQueryString();
        return $"https://www.twitch.tv/embed/{streamerName}/chat?{parentQuery}";
    }

    private static string GetYouTubeChatUrl(string videoId)
    {
        return $"https://www.youtube.com/live_chat?v={videoId}&embed_domain={EmbedDomain}";
    }

    /// <summary>
    /// Determines the appropriate embed domain for YouTube chat based on deployment environment
    /// This is required by YouTube's embed policies
    /// </summary>
    private static string EmbedDomain
    {
        get
        {
            lock (_embedDomainLock)
            {
                if (!string.IsNullOrWhiteSpace(_runtimeEmbedDomain))
                {
                    return _runtimeEmbedDomain;
                }
            }

#if DEBUG
            return "localhost";
#else
            return "msv.ghp.magaoidh.pro";
#endif
        }
    }

    private static HashSet<string> BuildParentHostSet(string? primaryHost)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(primaryHost))
        {
            hosts.Add(primaryHost);

            // Always add www. version for non-localhost domains
            if (!primaryHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                !primaryHost.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
            {
                if (primaryHost.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                {
                    // If primary has www., also add without www.
                    hosts.Add(primaryHost.Substring(4));
                }
                else
                {
                    // If primary doesn't have www., also add with www.
                    hosts.Add($"www.{primaryHost}");
                }
            }

            if (primaryHost.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                hosts.Add("127.0.0.1");
            }
        }

        if (!hosts.Any())
        {
            hosts.Add("localhost");
        }

        return hosts;
    }

    private static IReadOnlyList<string> GetTwitchParentHosts()
    {
        lock (_embedDomainLock)
        {
            if (_twitchParentHosts is null)
            {
                _twitchParentHosts = BuildParentHostSet(EmbedDomain);
            }

            return _twitchParentHosts.ToList();
        }
    }

    private static string BuildTwitchParentQueryString()
    {
        var parents = GetTwitchParentHosts();
        return string.Join("&", parents.Select(parent => $"parent={parent}"));
    }
}
