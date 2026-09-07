using MachiVerse.AdminView;
using MachiVerse.AdminView.Protocol;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddSingleton<GatewayAdminProtocolClient>();

await builder.Build().RunAsync();
