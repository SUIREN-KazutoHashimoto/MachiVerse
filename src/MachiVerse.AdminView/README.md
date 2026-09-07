# MachiVerse Administration View

ImplementationWorkId: `ADMIN-01`

Standalone Blazor WebAssembly implementation of the MachiVerse system-operator UI foundation.

## Contract boundary

- Target framework: `net10.0`
- Language: C# 14 / Razor
- Gateway transport: binary WebSocket
- Standard path: `/ws/v1/admin`
- Serialization: Protocol Buffers
- ProtocolId: `mv.gateway-admin-view`
- Protocol types are generated locally from `docs/protocols/schema/*.proto`.
- No Gateway/Core production DLL or shared compiled DTO dependency is used.
- General View Administrator permissions are not reused.

## Build

```bash
dotnet restore src/MachiVerse.AdminView/MachiVerse.AdminView.csproj
dotnet build src/MachiVerse.AdminView/MachiVerse.AdminView.csproj --no-restore
dotnet test tests/MachiVerse.AdminView.Tests/MachiVerse.AdminView.Tests.csproj
```

`global.json`, central package versions, and NuGet lock files provide the implementation/release dependency lock boundary.

## Config

The browser loads `wwwroot/admin-view.toml` as `config.admin-view / 1.0`.

Missing fields with schema defaults are filled in memory and reported by `AdminViewConfigLoadResult.DefaultedKeys`. Persisting normalized defaults is intentionally outside the browser-only loader; a deployment/config coordinator must perform durable write-back rather than giving browser JavaScript filesystem authority.

## Security

Production non-loopback use requires HTTPS/WSS. Access/refresh tokens are not handled by this client foundation; Gateway BFF owns browser session/token custody.
