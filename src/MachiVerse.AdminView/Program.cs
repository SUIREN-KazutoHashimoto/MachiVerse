using MachiVerse.AdminView.Configuration;
using MachiVerse.AdminView.Lifecycle;
using MachiVerse.AdminView.Presentation;
using MachiVerse.AdminView.Protocol;
using MachiVerse.AdminView.Session;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace MachiVerse.AdminView;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddScoped(_ => new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
        });

        builder.Services.AddSingleton<ProtocolEnvelopeValidator>();
        builder.Services.AddSingleton<AdminSessionState>();
        builder.Services.AddSingleton<AdminRequestStore>();
        builder.Services.AddScoped<AdminViewConfigLoader>();
        builder.Services.AddScoped<AdminGatewayClient>();
        builder.Services.AddScoped<AdminLifecycle>();

        await builder.Build().RunAsync();
    }
}
