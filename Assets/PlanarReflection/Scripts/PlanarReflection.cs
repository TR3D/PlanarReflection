using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static System.Net.WebRequestMethods;

public class PlanarReflection : ScriptableRendererFeature
{
    class PlanarReflectionPass : ScriptableRenderPass
    {
        private Settings settings;
        private ProfilingSampler _profilingSampler;
        private ProfilingSampler _profilingSamplerBlur;
        private RTHandle rtColorHandle, rtDepthHandle;
        private RTHandle[] rtDownSample = new RTHandle[6];
        private RTHandle[] rtUpSample = new RTHandle[6];
        private RenderTextureDescriptor blurDesc;
        private string colorTargetDestinationID = "_PlanarReflection";

        private FilteringSettings filteringSettings;
        private List<ShaderTagId> shaderTagsList = new List<ShaderTagId>();
        private float cameraAspect;
        private RenderStateBlock m_RenderStateBlock;

        private Material blurMaterial;

        public PlanarReflectionPass(Settings settings, string name)
        {
            // pass our settings class to the pass, so we can access them inside OnCameraSetup/Execute/etc
            this.settings = settings;
            // set up ProfilingSampler used in Execute method
            _profilingSampler = new ProfilingSampler(name);
            _profilingSamplerBlur = new ProfilingSampler("ReflectionBlur");

            filteringSettings = new FilteringSettings(RenderQueueRange.all, settings.layerMask);
            // Use URP's default shader tags
            shaderTagsList.Add(new ShaderTagId("SRPDefaultUnlit"));
            shaderTagsList.Add(new ShaderTagId("UniversalForward"));
            shaderTagsList.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Create color target descriptor
            var colorDesc = renderingData.cameraData.cameraTargetDescriptor;
            colorDesc.depthBufferBits= 0; // specifies a color target 
                       
            // Create depth target descriptor
            var depthDest = colorDesc;
            depthDest.depthBufferBits = 32;
        
            cameraAspect = (float)renderingData.cameraData.camera.pixelWidth / (float)renderingData.cameraData.camera.pixelHeight;
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

            if (colorTargetDestinationID != null)
            {
                // Should only run once as opposed to ReAllocateIfNeeded
                if (rtColorHandle == null)
                {
                    rtColorHandle = RTHandles.Alloc(Vector2.one / ((int)settings.resolution), colorDesc,
                        name: colorTargetDestinationID, 
                        wrapMode: TextureWrapMode.Clamp);
                }

                if (rtDepthHandle == null)
                {
                    rtDepthHandle = RTHandles.Alloc(Vector2.one / ((int)settings.resolution), depthDest,
                        name: colorTargetDestinationID + "_depth", 
                        wrapMode: TextureWrapMode.Clamp);
                }
            } 
            else
            {
                rtColorHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;
                rtDepthHandle = renderingData.cameraData.renderer.cameraDepthTargetHandle;
            }

            // Configure blur settings

            // Blur target descriptor (at whatever resolution we choose in settings)
            blurDesc = colorDesc;
            blurDesc.width = Mathf.Max(1, blurDesc.width >> ((int)settings.resolution - 1));
            blurDesc.height = Mathf.Max(1, blurDesc.height >> ((int)settings.resolution - 1));

            if (settings.applyBlur)
            {
                var downDesc = blurDesc;
                var upDesc = downDesc;
                upDesc.width *= 2;
                upDesc.height *= 2;

                for (int i = 0; i < rtDownSample.Length ;i++)
                {
                    RenderingUtils.ReAllocateIfNeeded(ref rtDownSample[i], downDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "rtDownSample_"+i);
                    RenderingUtils.ReAllocateIfNeeded(ref rtUpSample[i], upDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "rtUpSample_" + i);
                    
                    downDesc.width = Mathf.Max(1, downDesc.width >> 1);
                    downDesc.height = Mathf.Max(1, downDesc.height >> 1);
                    upDesc.width = Mathf.Max(1, upDesc.width >> 1); 
                    upDesc.height = Mathf.Max(1, upDesc.height >> 1); 
                }

                if (blurMaterial== null) blurMaterial = CoreUtils.CreateEngineMaterial("Hidden/ReflectionBlur"); 
            }

            // Configure the rendering targets
            ConfigureTarget(rtColorHandle, rtDepthHandle);
            ConfigureClear(ClearFlag.All, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ///////////////////////////
            // Obtain Reflection
            ///////////////////////////    
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
                Vector3 CameraDirection = camera.transform.forward;


               
                // Construct simple reflection matrix
                // vertex order is flipped, need to compensate that
                // https://archive.gamedev.net/archive/reference/articles/article2138.html
                Matrix4x4 Mf = new Matrix4x4(new Vector4(1, 0, 0, 0),  // XAxis
                                            new Vector4(0, -1, 0, 0),  // YAxis
                                            new Vector4(0, 0, 1, 0),   // ZAxis
                                            new Vector4(0, settings.planeYPos, 0, 1));  // Translation and Scale?

                RenderingUtils.SetViewAndProjectionMatrices(cmd, viewMatrix * Mf, projectionMatrix, false);

                #region // Custom culling 
                camera.TryGetCullingParameters(out var cullingParameters);
                // Create culling matrix from reflection matrices
                cullingParameters.cullingMatrix = projectionMatrix * viewMatrix * Mf;

                var planes = GeometryUtility.CalculateFrustumPlanes(cullingParameters.cullingMatrix);
                for (int i = 0; i < 6; i++)
                {
                    cullingParameters.SetCullingPlane(i, planes[i]);
                }
                var cullResults = context.Cull(ref cullingParameters);
                #endregion


                //invert meshes to compensate them being flipped in the mirror            
                cmd.SetInvertCulling(true);

                // Ensure we flush our command-buffer before we render...
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Draw Renderers to current Render Target
                SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                DrawingSettings drawingSettings = CreateDrawingSettings(shaderTagsList, ref renderingData, sortingCriteria);

                // Render the objects...
                context.DrawRenderers(cullResults, ref drawingSettings, ref filteringSettings, ref m_RenderStateBlock);

                //Pass the global shader texture
                cmd.SetGlobalTexture(colorTargetDestinationID, rtColorHandle);

                // reset the matrices
                RenderingUtils.SetViewAndProjectionMatrices(cmd, cameraData.GetViewMatrix(), cameraData.GetGPUProjectionMatrix(), false);
            }


            ///////////////////////////
            // Blur Reflection
            ///////////////////////////
            using (new ProfilingScope(cmd, _profilingSamplerBlur))
            {                
                if (settings.applyBlur)
                {
                    // Downsample
                    var rtTmp = rtColorHandle;                 
                    for (int i = 0; i < settings.iterations ; i++)
                    {
                        Blitter.BlitCameraTexture(cmd, rtTmp, rtDownSample[i], blurMaterial, 0);
                        rtTmp = rtDownSample[i];
                    }

                    Blitter.BlitCameraTexture(cmd, rtTmp, rtUpSample[settings.iterations - 1], 0, true);


                    // Upsample
                    for (int i = settings.iterations - 1; i > 0; i--)
                    {
                        blurMaterial.SetFloat("_Offset", settings.offset);

                        Blitter.BlitCameraTexture(cmd, rtUpSample[i], rtUpSample[i - 1], blurMaterial, 1);
                    }

                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    //Pass the global shader texture
                    cmd.SetGlobalTexture(colorTargetDestinationID, rtUpSample[0]);
                }
            }


            //reset mesh inversion          
            cmd.SetInvertCulling(false);


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
            for (int i = 0; i < rtDownSample.Length; i++)
            {
                rtDownSample[i]?.Release();
                rtUpSample[i]?.Release();
            }
        }
    }

    public enum QualitySettings : int
    {
        Full = 1,
        Half = 2,
        Quarter = 4,
        Eight = 8,
    }

    [System.Serializable]
    public class Settings
    {
        [Tooltip("Should the render texture be updated?")]
        public bool active = true;
        [Tooltip("World position of the reflection plane")]
        public float planeYPos = 0.0f;
        [Header("Render Texture Settings")]
        public QualitySettings resolution = QualitySettings.Half;
        public LayerMask layerMask = 1;
        [Header("Blur")]
        public bool applyBlur = true;
        [Range(2, 5)]
        public int iterations = 3;
        [Range(1.0f, 4.0f)]
        public float offset = 0.0f;
    }

    public Settings settings;
    PlanarReflectionPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new PlanarReflectionPass(settings, "PlanarReflections");
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
    }

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


