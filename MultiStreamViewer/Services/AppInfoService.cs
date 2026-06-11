using System.Reflection;

namespace MultiStreamViewer.Services;

public sealed class AppInfoService
{
    /// <summary>Full informational version, e.g. "0.2.0+&lt;commit sha&gt;".</summary>
    public string Version { get; }

    /// <summary>Just the SemVer part, e.g. "0.2.0".</summary>
    public string DisplayVersion { get; }

    /// <summary>Short commit hash from the informational version, or null if absent.</summary>
    public string? CommitHash { get; }

    public AppInfoService()
    {
        Version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "dev";

        // The SDK appends "+<full git sha>" to InformationalVersion; split it so we
        // can show a compact "0.2.0" with the commit available on hover.
        var plus = Version.IndexOf('+');
        if (plus >= 0)
        {
            DisplayVersion = Version[..plus];
            var hash = Version[(plus + 1)..];
            CommitHash = hash.Length >= 7 ? hash[..7] : (hash.Length > 0 ? hash : null);
        }
        else
        {
            DisplayVersion = Version;
            CommitHash = null;
        }
    }
}
