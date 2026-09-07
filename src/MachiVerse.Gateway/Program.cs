using MachiVerse.Gateway.Configuration;
using MachiVerse.Gateway.Protocol;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGrpc();
builder.Services.AddSingleton<EnvelopeValidator>();
builder.Services.AddSingleton<IGatewayConfigLoader, GatewayConfigLoader>();

var app = builder.Build();
app.UseWebSockets();

app.MapGet("/healthz", () => Results.Ok(new { component = "gateway", status = "starting" }));

app.Run();
