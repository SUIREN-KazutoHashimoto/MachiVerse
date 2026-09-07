namespace MachiVerse.Gateway.Configuration;

public interface IGatewayConfigLoader
{
    ValueTask<GatewayConfigDocument> LoadAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class GatewayConfigLoader : IGatewayConfigLoader
{
    public async ValueTask<GatewayConfigDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var content = await File.ReadAllTextAsync(path, cancellationToken);

        // QA-01/GW-01 follow-up will bind a TOML 1.0 parser and exact config.gateway/1.0 field schema.
        // Do not substitute JSON or IConfiguration semantics for the canonical TOML contract.
        return new GatewayConfigDocument(path, content);
    }
}

public sealed record GatewayConfigDocument(string Path, string RawToml);
