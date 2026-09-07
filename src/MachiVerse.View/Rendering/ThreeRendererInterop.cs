using Microsoft.JSInterop;

namespace MachiVerse.View.Rendering;

public sealed class ThreeRendererInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async ValueTask InitializeHostAsync(string canvasId, CancellationToken cancellationToken = default)
    {
        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, "./js/three-renderer.js");
        await _module.InvokeVoidAsync("initializeRendererBoundary", cancellationToken, canvasId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null) await _module.DisposeAsync();
    }
}
