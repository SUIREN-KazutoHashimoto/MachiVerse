export function initializeRendererBoundary(canvasId) {
    const canvas = document.getElementById(canvasId);
    if (!(canvas instanceof HTMLCanvasElement)) {
        throw new Error(`Renderer canvas not found: ${canvasId}`);
    }

    // VIEW-01 owns only the Blazor/ECMAScript presentation boundary.
    // THREE.WebGPURenderer scene lifecycle is implemented by VIEW-03.
    canvas.dataset.rendererBoundary = "ready";
}
