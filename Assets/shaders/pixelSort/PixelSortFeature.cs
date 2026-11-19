using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Renderer Feature (Render Graph) for the pixel-sort death effect.
/// Per frame it can: capture the camera colour into a snapshot texture (once),
/// run N sort steps ping-ponging between two textures, and draw the result over the camera.
/// It does nothing, and costs nothing, while PixelSortController is idle.
/// Add it to the BOTTOM of the Renderer's feature list so it captures the other effects' output.
/// </summary>
public class PixelSortFeature : ScriptableRendererFeature
{
    [SerializeField] Material material;   // material using the Hidden/PixelSort shader
    [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

    PixelSortPass pass;

    public override void Create()
    {
        pass = new PixelSortPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var ctrl = PixelSortController.Instance;
        if (material == null || ctrl == null || !ctrl.IsActive) return;

        // only the player's screen camera, never cameras that render into a render texture (the monitor)
        Camera cam = renderingData.cameraData.camera;
        if (cam.targetTexture != null || !cam.CompareTag("MainCamera")) return;

        pass.Setup(material, injectionPoint);
        renderer.EnqueuePass(pass);
    }

    class PixelSortPass : ScriptableRenderPass
    {
        const int PassCopy = 0;
        const int PassComposite = 3;

        class PassData
        {
            public TextureHandle source;
            public Material material;
            public int shaderPass;
        }

        Material material;

        public PixelSortPass()
        {
            // We read the camera colour, so URP must not render straight into the backbuffer.
            requiresIntermediateTexture = true;
        }

        public void Setup(Material mat, RenderPassEvent evt)
        {
            material = mat;
            renderPassEvent = evt;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {

            var ctrl = PixelSortController.Instance;
            if (ctrl == null || !ctrl.IsActive || material == null) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.cameraType != CameraType.Game || cameraData.renderType != CameraRenderType.Base)
                return;
            
            if (cameraData.camera.targetTexture != null || !cameraData.camera.CompareTag("MainCamera"))
                return;

            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
            {
                Debug.LogWarning("PixelSort: camera colour is the backbuffer, can't read it. " +
                                 "Check the injection point / post-processing setup.");
                return;
            }

            TextureHandle cameraColor = resources.activeColorTexture;

            var desc = cameraData.cameraTargetDescriptor;
            ctrl.EnsureTargets(desc.width, desc.height);
            ctrl.ApplyToMaterial(material);   // constant for every draw this frame

            TextureHandle a = renderGraph.ImportTexture(ctrl.HandleA);
            TextureHandle b = renderGraph.ImportTexture(ctrl.HandleB);

            // 1) Snapshot (first frame only)
            if (ctrl.NeedsCapture)
            {
                AddBlit(renderGraph, "PixelSort Capture", cameraColor, a, PassCopy, false);
                ctrl.MarkCaptured();
            }

            // 2) Sort steps, ping-pong between the two textures
            int steps = ctrl.ConsumePasses();
            bool latestIsA = ctrl.LatestIsA;
            for (int i = 0; i < steps; i++)
            {
                TextureHandle src = latestIsA ? a : b;
                TextureHandle dst = latestIsA ? b : a;
                AddBlit(renderGraph, "PixelSort Step", src, dst, ctrl.NextSortShaderPass(), false);
                latestIsA = !latestIsA;
            }
            ctrl.LatestIsA = latestIsA;

            // 3) Draw the sorted image over the camera colour (blended by opacity)
            AddBlit(renderGraph, "PixelSort Composite", latestIsA ? a : b, cameraColor, PassComposite, true);
        }

        void AddBlit(RenderGraph renderGraph, string name, TextureHandle source,
                     TextureHandle destination, int shaderPass, bool preserveDestination)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(name, out var data))
            {
                data.source = source;
                data.material = material;
                data.shaderPass = shaderPass;

                builder.UseTexture(source, AccessFlags.Read);
                // Compositing blends onto what is already there, so it must keep the old contents.
                builder.SetRenderAttachment(destination, 0,
                    preserveDestination ? AccessFlags.ReadWrite : AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, d.shaderPass);
                });
            }
        }
    }
}
