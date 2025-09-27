using System.Reflection;

namespace MultiStreamViewer.Services;

public sealed class AppInfoService
{
    public string Version { get; }

    public AppInfoService()
    {
        Version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "dev";
    }
}
