using System.Web;
using MultiStreamViewer.Models;

namespace MultiStreamViewer.Services;

/// <summary>
/// Parses a single stream reference from either a full platform URL
/// (twitch.tv / youtube.com / youtu.be / kick.com) or the shorthand
/// patterns used throughout the app (t/user, y/videoid, k/user).
/// Used by the universal input and the per-card "replace" form.
/// </summary>
public static class StreamInputParser
{
    public static bool TryParse(string? input, out StreamPlatform platform, out string identifier)
    {
        platform = default;
        identifier = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        input = input.Trim();

        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(input, UriKind.Absolute, out var uri)
                && TryParseUrl(uri, out platform, out identifier);
        }

        // Shorthand: t/user, y/videoid, k/user — take the first pair only.
        var parts = input.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var identifierCandidate = parts[1].Trim();
            var mapped = parts[0].Trim().ToLowerInvariant() switch
            {
                "t" or "twitch" => (StreamPlatform?)StreamPlatform.Twitch,
                "y" or "youtube" => StreamPlatform.YouTube,
                "k" or "kick" => StreamPlatform.Kick,
                _ => null,
            };

            if (mapped.HasValue && !string.IsNullOrEmpty(identifierCandidate))
            {
                platform = mapped.Value;
                identifier = identifierCandidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryParseUrl(Uri uri, out StreamPlatform platform, out string identifier)
    {
        platform = default;
        identifier = string.Empty;

        var host = uri.Host.ToLowerInvariant();

        if (host.Contains("twitch.tv"))
        {
            var username = uri.AbsolutePath.Trim('/');
            if (!string.IsNullOrEmpty(username))
            {
                platform = StreamPlatform.Twitch;
                identifier = username;
                return true;
            }
        }
        else if (host.Contains("youtube.com") || host.Contains("youtu.be"))
        {
            string? videoId = host.Contains("youtu.be")
                ? uri.AbsolutePath.Trim('/')
                : HttpUtility.ParseQueryString(uri.Query)["v"];

            if (!string.IsNullOrEmpty(videoId))
            {
                platform = StreamPlatform.YouTube;
                identifier = videoId;
                return true;
            }
        }
        else if (host.Contains("kick.com"))
        {
            var username = uri.AbsolutePath.Trim('/');
            if (!string.IsNullOrEmpty(username))
            {
                platform = StreamPlatform.Kick;
                identifier = username;
                return true;
            }
        }

        return false;
    }
}
