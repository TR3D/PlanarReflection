using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlanarReflection : ScriptableRendererFeature
{
    class PlanarReflectionPass : ScriptableRenderPass
    {
        private Settings settings;
        private ProfilingSampler _profilingSampler;
        private RTHandle rtColorHandle, rtDepthHandle;

        private FilteringSettings filteringSettings;
        private List<ShaderTagId> shaderTagsList = new List<ShaderTagId>();
        private float cameraAspect;
        private RenderStateBlock m_RenderStateBlock;

        // (constructor, method name should match class name)
        public PlanarReflectionPass(Settings settings, string name)
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
            // Create color target
            var colorDesc = renderingData.cameraData.cameraTargetDescriptor;
            colorDesc.depthBufferBits= 0;// specifies a color target
            

            // Create depth target
            var depthDest = colorDesc;
            depthDest.depthBufferBits = 32;

            cameraAspect = (float)renderingData.cameraData.camera.pixelWidth / (float)renderingData.cameraData.camera.pixelHeight;
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

            if (settings.colorTargetDestinationID != null)
            {
                // RenderingUtils.ReAllocateIfNeeded(ref color, colorDesc, name: settings.colorTargetDestinationID);

                // Should only run once as opposed to ReAllocateIfNeeded
                if (rtColorHandle == null)
                {
                    rtColorHandle = RTHandles.Alloc(Vector2.one * settings.quality, colorDesc,
                        name: settings.colorTargetDestinationID, 
                        wrapMode: TextureWrapMode.Clamp);
                }

                if (rtDepthHandle == null)
                {
                    rtDepthHandle = RTHandles.Alloc(Vector2.one * settings.quality, depthDest,
                        name: settings.colorTargetDestinationID + "_depth", 
                        wrapMode: TextureWrapMode.Clamp);
                }
            } 
            else
            {
                rtColorHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;
                rtDepthHandle = renderingData.cameraData.renderer.cameraDepthTargetHandle;
            }

            // Configure the rendering target(s)
            ConfigureTarget(rtColorHandle, rtDepthHandle);
            ConfigureClear(ClearFlag.All, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, _profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
     
                ref CameraData cameraData = ref renderingData.cameraData;
                Camera camera = cameraData.camera;


                Matrix4x4 projectionMatrix = Matrix4x4.Perspective(camera.fieldOfView, cameraAspect,
                    camera.nearClipPlane, camera.farClipPlane);
                projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, cameraData.IsCameraProjectionMatrixFlipped());

                Matrix4x4 viewMatrix = cameraData.GetViewMatrix();
                Vector4 cameraTranslation = viewMatrix.GetColumn(3);
                Vector3 CameraDirection = camera.transform.forward;


                // Construct simple reflection matrix
                // vertex order is flipped, need to compensate that
                // https://archive.gamedev.net/archive/reference/articles/article2138.html
                Matrix4x4 Mf = new Matrix4x4(new Vector4(1,0,0,0),  // XAxis
                                            new Vector4(0,-1,0,0),  // YAxis
                                            new Vector4(0,0,1,0),   // ZAxis
                                            new Vector4(0,0,0,1));  // Translation and Scale?

                RenderingUtils.SetViewAndProjectionMatrices(cmd, viewMatrix * Mf, projectionMatrix, false);

                //invert meshes to compensate them being flipped in the mirror            
                cmd.SetInvertCulling(true);

                // Ensure we flush our command-buffer before we render...
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();               
               

                // Draw Renderers to current Render Target (set in OnCameraSetup)
                SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                DrawingSettings drawingSettings = CreateDrawingSettings(shaderTagsList, ref renderingData, sortingCriteria);

                // Render the objects...
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref m_RenderStateBlock);

                         
                RTHandle rtCamera = renderingData.cameraData.renderer.cameraColorTargetHandle;

                //Pass the global shader texture
                cmd.SetGlobalTexture(settings.colorTargetDestinationID, rtColorHandle);

                // reset the matrices
                RenderingUtils.SetViewAndProjectionMatrices(cmd, cameraData.GetViewMatrix(), cameraData.GetGPUProjectionMatrix(), false);
            }

            //reset mesh inversion          
            cmd.SetInvertCulling(false);

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
            rtColorHandle?.Release();
            rtDepthHandle?.Release();
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
    PlanarReflectionPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new PlanarReflectionPass(settings, "PlanarReflections");

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


