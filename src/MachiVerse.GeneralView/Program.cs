using MachiVerse.GeneralView;
using MachiVerse.GeneralView.Protocol;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddSingleton<GatewayProtocolClient>();

await builder.Build().RunAsync();
