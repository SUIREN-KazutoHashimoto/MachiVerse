import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.185.1/build/three.webgpu.js";

let renderer = null;
let scene = null;
let camera = null;
let projectionRoot = null;
let canvas = null;
let resizeObserver = null;
let contextLostHandler = null;
let currentProjection = null;
let currentOptions = null;

function requireCanvas(canvasId) {
    const element = document.getElementById(canvasId);
    if (!(element instanceof HTMLCanvasElement)) {
        throw new Error(`Renderer canvas not found: ${canvasId}`);
    }
    return element;
}

function resizeRenderer() {
    if (!renderer || !camera || !canvas) return;
    const width = Math.max(1, canvas.clientWidth || canvas.parentElement?.clientWidth || 1);
    const height = Math.max(1, canvas.clientHeight || 1);
    const pixelRatio = Math.min(window.devicePixelRatio || 1, currentOptions.maxPixelRatio);
    renderer.setPixelRatio(pixelRatio);
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
}

function backendMode(forceWebGl) {
    if (forceWebGl) return "webgl2-forced";
    return navigator.gpu ? "webgpu-preferred" : "webgl2-fallback";
}

function status() {
    return {
        initialized: renderer !== null,
        backendMode: currentOptions?.backendMode ?? "uninitialized",
        threeRevision: THREE.REVISION,
        basisStep: currentProjection?.basisStep ?? null,
        projectionRecordCount: currentProjection?.records?.length ?? 0
    };
}

export async function initializeRendererBoundary(canvasId, maxPixelRatio, forceWebGl = false) {
    if (!Number.isFinite(maxPixelRatio) || maxPixelRatio <= 0) {
        throw new Error("maxPixelRatio must be a positive finite number.");
    }

    await disposeRendererBoundary();
    canvas = requireCanvas(canvasId);
    currentOptions = {
        canvasId,
        maxPixelRatio,
        forceWebGl,
        backendMode: backendMode(forceWebGl)
    };

    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x101216);

    camera = new THREE.PerspectiveCamera(60, 1, 0.05, 1000000);
    camera.position.set(0, 4, 8);
    camera.lookAt(0, 0, 0);

    projectionRoot = new THREE.Group();
    projectionRoot.name = "machiverse-scene-projection";
    scene.add(projectionRoot);

    scene.add(new THREE.HemisphereLight(0xffffff, 0x222233, 1.5));

    renderer = new THREE.WebGPURenderer({
        canvas,
        antialias: true,
        forceWebGL: forceWebGl
    });
    await renderer.init();

    resizeObserver = new ResizeObserver(resizeRenderer);
    resizeObserver.observe(canvas);
    window.addEventListener("resize", resizeRenderer, { passive: true });
    resizeRenderer();

    contextLostHandler = event => {
        event.preventDefault();
        canvas.dataset.rendererState = "lost";
        renderer?.setAnimationLoop(null);
    };
    canvas.addEventListener("webglcontextlost", contextLostHandler, false);

    renderer.setAnimationLoop(() => {
        if (renderer && scene && camera) renderer.render(scene, camera);
    });

    canvas.dataset.rendererBoundary = "ready";
    canvas.dataset.rendererState = "ready";
    canvas.dataset.rendererBackend = currentOptions.backendMode;
    canvas.dataset.threeRevision = THREE.REVISION;
    return status();
}

export function applySceneProjection(projection) {
    if (!renderer || !projectionRoot || !canvas) {
        throw new Error("Renderer has not been initialized.");
    }
    if (!projection || !Number.isInteger(projection.basisStep) || projection.basisStep < 0) {
        throw new Error("Invalid SceneProjectionModel basis step.");
    }
    if (!Array.isArray(projection.records)) {
        throw new Error("Invalid SceneProjectionModel records.");
    }

    // VIEW-03 keeps protocol/domain payload interpretation out of Three.js itself.
    // Concrete projection-schema adapters will populate projectionRoot; until then
    // only confirmed projection identity/revision metadata crosses this boundary.
    currentProjection = projection;
    projectionRoot.userData.basisStep = projection.basisStep;
    projectionRoot.userData.continuityTokenHex = projection.continuityTokenHex;
    projectionRoot.userData.projectionSchemaDigestHex = projection.projectionSchemaDigestHex;
    projectionRoot.userData.records = projection.records.map(record => ({
        recordSchemaId: record.recordSchemaId,
        recordIdHex: record.recordIdHex,
        recordRevision: record.recordRevision
    }));

    canvas.dataset.confirmedBasisStep = String(projection.basisStep);
    canvas.dataset.projectionRecordCount = String(projection.records.length);
}

export async function reinitializeRendererBoundary(canvasId, maxPixelRatio, forceWebGl = false) {
    return initializeRendererBoundary(canvasId, maxPixelRatio, forceWebGl);
}

export function getRendererStatus() {
    return status();
}

export async function disposeRendererBoundary() {
    if (renderer) renderer.setAnimationLoop(null);
    if (resizeObserver) {
        resizeObserver.disconnect();
        resizeObserver = null;
    }
    window.removeEventListener("resize", resizeRenderer);
    if (canvas && contextLostHandler) {
        canvas.removeEventListener("webglcontextlost", contextLostHandler, false);
    }
    contextLostHandler = null;

    if (projectionRoot) {
        projectionRoot.traverse(object => {
            object.geometry?.dispose?.();
            if (Array.isArray(object.material)) object.material.forEach(material => material.dispose?.());
            else object.material?.dispose?.();
        });
    }

    renderer?.dispose();
    renderer = null;
    scene = null;
    camera = null;
    projectionRoot = null;
    currentProjection = null;
    currentOptions = null;
    if (canvas) {
        canvas.dataset.rendererState = "disposed";
        delete canvas.dataset.rendererBackend;
    }
    canvas = null;
}
