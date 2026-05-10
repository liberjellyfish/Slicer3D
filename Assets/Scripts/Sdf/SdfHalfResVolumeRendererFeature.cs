using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class SdfHalfResVolumeRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    [SerializeField] private Material volumeMaterial;
    [SerializeField] private Shader upsampleShader;
    [SerializeField] [Range(1, 4)] private int downsample = 2;
    [SerializeField] private bool bilateralUpsample = true;
    [SerializeField] [Min(0.001f)] private float depthSensitivity = 0.025f;
    [SerializeField] [Min(0.0f)] private float deltaClamp = 8.0f;
    [SerializeField] private bool includeSceneView = true;

    private HalfResVolumePass pass;
    private Material upsampleMaterial;

    public override void Create()
    {
        ResolveUpsampleMaterial();
        pass = new HalfResVolumePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraType cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
        {
            return;
        }

        if (!includeSceneView && cameraType == CameraType.SceneView)
        {
            return;
        }

        if (volumeMaterial == null)
        {
            Debug.LogWarning($"{nameof(SdfHalfResVolumeRendererFeature)} skipped because no volume material is assigned.");
            return;
        }

        if (!ResolveUpsampleMaterial())
        {
            Debug.LogWarning($"{nameof(SdfHalfResVolumeRendererFeature)} skipped because the upsample shader was not found.");
            return;
        }

        RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.msaaSamples = 1;
        descriptor.depthBufferBits = 0;
        descriptor.depthStencilFormat = GraphicsFormat.None;

        pass.renderPassEvent = renderPassEvent;
        pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        pass.Setup(
            descriptor,
            volumeMaterial,
            upsampleMaterial,
            Mathf.Max(downsample, 1),
            bilateralUpsample,
            depthSensitivity,
            deltaClamp);

        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        pass = null;
        CoreUtils.Destroy(upsampleMaterial);
        upsampleMaterial = null;
    }

    private bool ResolveUpsampleMaterial()
    {
        if (upsampleMaterial != null)
        {
            return true;
        }

        if (upsampleShader == null)
        {
            upsampleShader = Shader.Find("Hidden/Sdf/HalfResVolumeUpsample");
        }

        if (upsampleShader == null)
        {
            return false;
        }

        upsampleMaterial = CoreUtils.CreateEngineMaterial(upsampleShader);
        return upsampleMaterial != null;
    }

    private sealed class HalfResVolumePass : ScriptableRenderPass
    {
        private static readonly int HalfResVolumeTextureId = Shader.PropertyToID("_SdfHalfResVolumeTexture");
        private static readonly int HalfResVolumeTexelSizeId = Shader.PropertyToID("_SdfHalfResVolumeTexelSize");
        private static readonly int BilateralEnabledId = Shader.PropertyToID("_SdfHalfResVolumeBilateralEnabled");
        private static readonly int DepthSensitivityId = Shader.PropertyToID("_SdfHalfResVolumeDepthSensitivity");
        private static readonly int DeltaClampId = Shader.PropertyToID("_SdfHalfResVolumeDeltaClamp");
        private static readonly Vector4 FullScreenScaleBias = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);

        private readonly ProfilingSampler halfResProfilingSampler = new ProfilingSampler("SDF.HalfResScreenSpaceVolume");
        private RTHandle sourceColor;
        private RTHandle halfResVolume;
        private Material volumeMaterial;
        private Material upsampleMaterial;
        private RenderTextureDescriptor sourceDescriptor;
        private RenderTextureDescriptor halfDescriptor;
        private int downsample = 2;
        private bool bilateralUpsample = true;
        private float depthSensitivity = 0.025f;
        private float deltaClamp = 8.0f;

        public HalfResVolumePass()
        {
            requiresIntermediateTexture = true;
        }

        public void Setup(
            RenderTextureDescriptor cameraDescriptor,
            Material volumeMaterial,
            Material upsampleMaterial,
            int downsample,
            bool bilateralUpsample,
            float depthSensitivity,
            float deltaClamp)
        {
            this.volumeMaterial = volumeMaterial;
            this.upsampleMaterial = upsampleMaterial;
            this.downsample = Mathf.Max(downsample, 1);
            this.bilateralUpsample = bilateralUpsample;
            this.depthSensitivity = Mathf.Max(depthSensitivity, 0.001f);
            this.deltaClamp = Mathf.Max(deltaClamp, 0.0f);

            sourceDescriptor = cameraDescriptor;
            sourceDescriptor.msaaSamples = 1;
            sourceDescriptor.depthBufferBits = 0;
            sourceDescriptor.depthStencilFormat = GraphicsFormat.None;

            halfDescriptor = sourceDescriptor;
            halfDescriptor.width = Mathf.Max(1, sourceDescriptor.width / this.downsample);
            halfDescriptor.height = Mathf.Max(1, sourceDescriptor.height / this.downsample);
        }

#pragma warning disable 618, 672
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderingUtils.ReAllocateHandleIfNeeded(
                ref sourceColor,
                sourceDescriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_SdfVolumeSourceColor");

            RenderingUtils.ReAllocateHandleIfNeeded(
                ref halfResVolume,
                halfDescriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_SdfHalfResVolume");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (volumeMaterial == null || upsampleMaterial == null)
            {
                return;
            }

            RTHandle cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
            if (cameraColor == null || sourceColor == null || halfResVolume == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, halfResProfilingSampler))
            {
                Blitter.BlitCameraTexture(cmd, cameraColor, sourceColor);
                Blitter.BlitCameraTexture(cmd, sourceColor, halfResVolume, volumeMaterial, 0);

                Vector4 halfTexelSize = new Vector4(
                    1.0f / Mathf.Max(halfDescriptor.width, 1),
                    1.0f / Mathf.Max(halfDescriptor.height, 1),
                    halfDescriptor.width,
                    halfDescriptor.height);
                upsampleMaterial.SetTexture(HalfResVolumeTextureId, halfResVolume);
                upsampleMaterial.SetVector(HalfResVolumeTexelSizeId, halfTexelSize);
                upsampleMaterial.SetFloat(BilateralEnabledId, bilateralUpsample ? 1.0f : 0.0f);
                upsampleMaterial.SetFloat(DepthSensitivityId, depthSensitivity);
                upsampleMaterial.SetFloat(DeltaClampId, deltaClamp);

                Blitter.BlitCameraTexture(cmd, sourceColor, cameraColor, upsampleMaterial, 0);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#pragma warning restore 618, 672

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (volumeMaterial == null || upsampleMaterial == null)
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogWarning($"{nameof(SdfHalfResVolumeRendererFeature)} requires an intermediate color target when RenderGraph is enabled.");
                return;
            }

            TextureHandle activeColor = resourceData.activeColorTexture;
            if (!activeColor.IsValid())
            {
                return;
            }

            TextureHandle sourceCopy = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                sourceDescriptor,
                "_SdfVolumeSourceColor",
                false,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp);

            RenderGraphUtils.BlitMaterialParameters copyParameters = new(
                activeColor,
                sourceCopy,
                Blitter.GetBlitMaterial(TextureDimension.Tex2D),
                0);
            renderGraph.AddBlitPass(copyParameters, "SDF.CopyColorForHalfResVolume");

            TextureHandle halfVolume = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                halfDescriptor,
                "_SdfHalfResVolume",
                false,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp);

            TextureHandle cameraDepthTexture = resourceData.cameraDepthTexture;

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<VolumePassData>(
                       "SDF.RenderHalfResVolume",
                       out VolumePassData passData,
                       halfResProfilingSampler))
            {
                passData.sourceColor = sourceCopy;
                passData.depthTexture = cameraDepthTexture;
                passData.material = volumeMaterial;

                builder.UseTexture(passData.sourceColor, AccessFlags.Read);
                if (passData.depthTexture.IsValid())
                {
                    builder.UseTexture(passData.depthTexture, AccessFlags.Read);
                }

                builder.SetRenderAttachment(halfVolume, 0, AccessFlags.Write);
                builder.SetRenderFunc((VolumePassData data, RasterGraphContext context) => ExecuteVolumeRenderGraph(data, context));
            }

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<UpsamplePassData>(
                       "SDF.UpsampleHalfResVolume",
                       out UpsamplePassData passData,
                       halfResProfilingSampler))
            {
                passData.sourceColor = sourceCopy;
                passData.halfVolume = halfVolume;
                passData.depthTexture = cameraDepthTexture;
                passData.material = upsampleMaterial;
                passData.halfTexelSize = new Vector4(
                    1.0f / Mathf.Max(halfDescriptor.width, 1),
                    1.0f / Mathf.Max(halfDescriptor.height, 1),
                    halfDescriptor.width,
                    halfDescriptor.height);
                passData.bilateralUpsample = bilateralUpsample ? 1.0f : 0.0f;
                passData.depthSensitivity = depthSensitivity;
                passData.deltaClamp = deltaClamp;

                builder.UseTexture(passData.sourceColor, AccessFlags.Read);
                builder.UseTexture(passData.halfVolume, AccessFlags.Read);
                if (passData.depthTexture.IsValid())
                {
                    builder.UseTexture(passData.depthTexture, AccessFlags.Read);
                }

                builder.SetRenderAttachment(activeColor, 0, AccessFlags.Write);
                builder.SetRenderFunc((UpsamplePassData data, RasterGraphContext context) => ExecuteUpsampleRenderGraph(data, context));
            }
        }

        private static void ExecuteVolumeRenderGraph(VolumePassData data, RasterGraphContext context)
        {
            Blitter.BlitTexture(context.cmd, data.sourceColor, FullScreenScaleBias, data.material, 0);
        }

        private static void ExecuteUpsampleRenderGraph(UpsamplePassData data, RasterGraphContext context)
        {
            data.material.SetTexture(HalfResVolumeTextureId, data.halfVolume);
            data.material.SetVector(HalfResVolumeTexelSizeId, data.halfTexelSize);
            data.material.SetFloat(BilateralEnabledId, data.bilateralUpsample);
            data.material.SetFloat(DepthSensitivityId, data.depthSensitivity);
            data.material.SetFloat(DeltaClampId, data.deltaClamp);
            Blitter.BlitTexture(context.cmd, data.sourceColor, FullScreenScaleBias, data.material, 0);
        }

        private sealed class VolumePassData
        {
            public TextureHandle sourceColor;
            public TextureHandle depthTexture;
            public Material material;
        }

        private sealed class UpsamplePassData
        {
            public TextureHandle sourceColor;
            public TextureHandle halfVolume;
            public TextureHandle depthTexture;
            public Material material;
            public Vector4 halfTexelSize;
            public float bilateralUpsample;
            public float depthSensitivity;
            public float deltaClamp;
        }

        public void Dispose()
        {
            sourceColor?.Release();
            sourceColor = null;
            halfResVolume?.Release();
            halfResVolume = null;
        }
    }
}
