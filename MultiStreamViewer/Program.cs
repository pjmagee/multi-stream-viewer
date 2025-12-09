using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using MultiStreamViewer;
using MultiStreamViewer.Services;
using MultiStreamViewer.Models;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddFluentUIComponents();
builder.Services.AddSingleton<StreamService>();
builder.Services.AddSingleton<AppInfoService>();

// Build first to access JS runtime, then detect the true browser host.
var host = builder.Build();

try
{
	var js = host.Services.GetRequiredService<IJSRuntime>();
	// Read window.location.host directly to match Twitch parent requirements exactly.
	var locationHost = await js.InvokeAsync<string>("eval", "window.location.host");
	if (!string.IsNullOrWhiteSpace(locationHost))
	{
		StreamInfo.ConfigureEmbedDomain(locationHost);
	}
}
catch
{
	// If JS interop is unavailable, StreamInfo falls back to defaults
}

await host.RunAsync();
