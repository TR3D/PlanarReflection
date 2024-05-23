using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ReflectionBlur : ScriptableRendererFeature
{
    class ReflectionBlurPass : ScriptableRenderPass
    {
        private Settings settings;
        private ProfilingSampler _profilingSampler;
        private RTHandle rtReflection1, rtReflection2;
        private Material blitmaterial;

        private FilteringSettings filteringSettings;
        private List<ShaderTagId> shaderTagsList = new List<ShaderTagId>();
        private float cameraAspect;
        private RenderStateBlock m_RenderStateBlock;

        // (constructor, method name should match class name)
        public ReflectionBlurPass(Settings settings, string name)
        {
            // pass our settings class to the pass, so we can access them inside OnCameraSetup/Execute/etc
            this.settings = settings;
            // set up ProfilingSampler used in Execute method
            _profilingSampler = new ProfilingSampler(name);

            filteringSettings = new FilteringSettings(RenderQueueRange.all, settings.layerMask);
            // Use URP's default shader tags
            shaderTagsList.Add(new ShaderTagId("SRPDefaultUnlit"));
            shaderTagsList.Add(new ShaderTagId("UniversalForward"));
            shaderTagsList.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Initialize blit material
            if (blitmaterial == null)
            {
                blitmaterial = new Material(Shader.Find("Shader Graphs/ReflectionBlurShader"));
            }

            // Create color target
            var colorDesc = renderingData.cameraData.cameraTargetDescriptor;
            colorDesc.depthBufferBits = 0;// specifies a color target


            cameraAspect = (float)renderingData.cameraData.camera.pixelWidth / (float)renderingData.cameraData.camera.pixelHeight;
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

            if (settings.colorTargetDestinationID != null)
            {
                // RenderingUtils.ReAllocateIfNeeded(ref color, colorDesc, name: settings.colorTargetDestinationID);

                // Should only run once as opposed to ReAllocateIfNeeded
                if (rtReflection1 == null)
                {
                    rtReflection1 = RTHandles.Alloc(Vector2.one * settings.quality, colorDesc,
                        name: "_rtReflection1",
                        wrapMode: TextureWrapMode.Clamp);
                }
                if (rtReflection2 == null)
                {
                    rtReflection2 = RTHandles.Alloc(Vector2.one * settings.quality, colorDesc,
                        name: "_rtReflection2",
                        wrapMode: TextureWrapMode.Clamp);
                }


            }
            else
            {
                rtReflection1 = renderingData.cameraData.renderer.cameraColorTargetHandle;
            }

            // Configure the rendering target(s)
            ConfigureTarget(rtReflection1);
            ConfigureClear(ClearFlag.All, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                //Blitter.BlitCameraTexture(cmd, )

            }

            // Execute Command Buffer one last time and release it
            // (otherwise we get weird recursive list in Frame Debugger)
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // Better to release RTHandles in a specific method rather than in here
            // Releasing in here causes glitchy scene view camera rendering
        }

        public void ReleaseTargets()
        {
            rtReflection1?.Release();
        }
    }

    [System.Serializable]
    public class Settings
    {
        [Header("Settings")]
        public bool active = true;
        public LayerMask layerMask = 1;
        public string colorTargetDestinationID = "_PlanarReflection";
        [Range(0.01f, 1f)]
        public float quality = 0.5f;
    }

    public Settings settings;
    ReflectionBlurPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new ReflectionBlurPass(settings, "PlanarReflectionBlur");

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // skip pass if preview camera
        if (renderingData.cameraData.isPreviewCamera || !settings.active) return;

        renderer.EnqueuePass(m_ScriptablePass);
    }

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass.ReleaseTargets();
        base.Dispose(disposing);
    }
}


