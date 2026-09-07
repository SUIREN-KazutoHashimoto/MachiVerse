using MachiVerse.Administration.View;
using MachiVerse.Administration.View.Configuration;
using MachiVerse.Administration.View.Modules.Monitoring;
using MachiVerse.Administration.View.Protocol;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var bootstrapClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
var configText = await bootstrapClient.GetStringAsync("config/admin-view.toml");
var adminConfig = AdminViewConfigLoader.LoadText(configText);

builder.Services.AddSingleton(adminConfig);
builder.Services.AddSingleton<MonitoringProjectionStore>();
builder.Services.AddSingleton<IMonitoringModuleBoundary>(static services => services.GetRequiredService<MonitoringProjectionStore>());
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<AdminGatewayProtocolClient>();

await builder.Build().RunAsync();
