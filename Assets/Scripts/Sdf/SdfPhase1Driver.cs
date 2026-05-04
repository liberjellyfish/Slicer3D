using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(MeshFilter))]
public class SdfPhase1Driver : MonoBehaviour
{
    public enum ShapeMode
    {
        Sphere = 0,
        ClippedBox = 1,
        Union = 2
    }

    public enum DebugViewMode
    {
        Lighting = 0,
        WorldNormal = 1,
        CutMask = 2,
        CutDominance = 3,
        MainLightShadow = 4,
        SdfSoftShadow = 5,
        TotalShadow = 6,
        VolumeDensity = 7,
        VolumeTransmittance = 8,
        VolumeShadow = 9,
        VolumeLight = 10,
        VolumeGeometryShadow = 11,
        VolumeMediaTransmittance = 12
    }

    public enum VolumePreset
    {
        Balanced = 0,
        CinematicWarm = 1,
        Performance = 2,
        DiagnosticDense = 3
    }

    [Header("Shape")]
    [SerializeField] private ShapeMode shapeMode = ShapeMode.Union;
    [SerializeField] private Vector3 sphereCenter = Vector3.zero;
    [SerializeField] [Min(0.01f)] private float sphereRadius = 0.33f;
    [SerializeField] private Vector3 boxExtents = new Vector3(0.28f, 0.28f, 0.28f);
    [SerializeField] private Vector3 cutPlaneNormal = new Vector3(0.8f, 0.2f, 0.0f);
    [SerializeField] private float cutPlaneOffset = 0.0f;

    [Header("Ray Marching")]
    [SerializeField] [Range(8, 256)] private int maxSteps = 96;
    [SerializeField] [Min(0.1f)] private float maxDistance = 8.0f;
    [SerializeField] [Min(0.0001f)] private float hitEpsilon = 0.001f;
    [SerializeField] [Min(0.0001f)] private float normalEpsilon = 0.002f;

    [Header("Lighting")]
    [SerializeField] private Color baseColor = new Color(1.0f, 0.82f, 0.68f, 1.0f);
    [SerializeField] [Range(0.0f, 1.0f)] private float ambientStrength = 0.15f;
    [SerializeField] [Range(0.0f, 2.0f)] private float diffuseStrength = 1.0f;

    [Header("Shadowing")]
    [SerializeField] [Range(0.0f, 1.0f)] private float receiveMainLightShadowStrength = 1.0f;
    [SerializeField] [Range(0.0f, 1.0f)] private float sdfSoftShadowStrength = 0.85f;
    [SerializeField] [Range(1.0f, 64.0f)] private float sdfSoftShadowSharpness = 16.0f;
    [SerializeField] [Range(4, 64)] private int sdfSoftShadowSteps = 24;
    [SerializeField] [Min(0.0005f)] private float sdfSoftShadowStart = 0.01f;
    [SerializeField] [Min(0.01f)] private float sdfSoftShadowDistance = 1.5f;
    [SerializeField] [Min(0.0001f)] private float sdfSoftShadowNormalBias = 0.005f;
    [SerializeField] [Range(0.25f, 4.0f)] private float sdfSoftShadowMinStepScale = 1.0f;
    [SerializeField] [Range(0.02f, 0.5f)] private float sdfSoftShadowMaxStepFraction = 0.2f;
    [SerializeField] [Range(0.0f, 1.0f)] private float sdfSoftShadowDistanceFadeStart = 0.7f;

    [Header("Cut Surface")]
    [SerializeField] private Color cutFaceColor = new Color(0.97f, 0.43f, 0.31f, 1.0f);
    [SerializeField] [Range(0.0f, 1.0f)] private float cutFaceBlend = 0.85f;
    [SerializeField] [Min(0.0005f)] private float cutFaceDominanceSoftness = 0.01f;
    [SerializeField] [Range(0.0f, 1.0f)] private float cutFaceOcclusionStrength = 0.45f;
    [SerializeField] [Min(0.01f)] private float cutFaceOcclusionDistance = 0.2f;
    [SerializeField] [Min(0.0005f)] private float cutFaceBandSoftness = 0.01f;
    [SerializeField] [Min(0.0005f)] private float cutFaceEdgeWidth = 0.04f;
    [SerializeField] [Range(0.0f, 2.0f)] private float cutFaceEdgeBoost = 0.35f;
    [SerializeField] [Range(0.0f, 2.0f)] private float cutFaceFreshnessBoost = 0.3f;

    [Header("Volume Lighting")]
    [SerializeField] private bool volumeLightEnabled = false;
    [SerializeField] [Range(0.0f, 8.0f)] private float volumeLightIntensity = 3.0f;
    [SerializeField] [Range(0.0f, 8.0f)] private float volumeLightDensity = 1.4f;
    [SerializeField] [Range(-0.8f, 0.8f)] private float volumeLightAnisotropy = 0.15f;
    [SerializeField] [Range(4, 32)] private int volumeLightSamples = 20;
    [SerializeField] [Min(0.05f)] private float volumeLightMaxDistance = 1.2f;
    [SerializeField] [Min(0.005f)] private float volumeLightMaxStepLength = 0.06f;
    [SerializeField] [Range(0.0f, 1.0f)] private float volumeLightShadowStrength = 0.75f;
    [SerializeField] [Min(0.0001f)] private float volumeLightShadowBias = 0.01f;
    [SerializeField] [Min(0.01f)] private float volumeLightSurfaceFadeDistance = 0.22f;
    [SerializeField] [Min(0.01f)] private float volumeLightPlaneBand = 0.16f;
    [SerializeField] [Min(0.01f)] private float volumeLightRemovedDepth = 0.28f;
    [SerializeField] [Min(0.01f)] private float volumeLightShapeDepth = 0.24f;
    [SerializeField] [Min(0.1f)] private float volumeLightNoiseScale = 4.0f;
    [SerializeField] [Range(0.0f, 1.0f)] private float volumeLightNoiseStrength = 0.18f;
    [SerializeField] [Min(0.0f)] private float volumeLightNoiseDrift = 0.2f;

    [Header("Volume Medium")]
    [SerializeField] [Range(0.0f, 0.08f)] private float volumeBaseFogDensity = 0.002f;
    [SerializeField] [Range(0.0f, 0.5f)] private float volumeHeightFogStrength = 0.08f;
    [SerializeField] [Range(0.0f, 4.0f)] private float volumeCutFogBoost = 1.4f;
    [SerializeField] [Range(0.25f, 4.0f)] private float volumeNoiseContrast = 1.25f;

    [Header("Volume Shadow")]
    [SerializeField] [Range(4, 64)] private int volumeShadowSamples = 16;
    [SerializeField] [Min(0.05f)] private float volumeShadowMaxDistance = 2.0f;

    [Header("Volume Point Light")]
    [SerializeField] private bool volumePointLightEnabled = true;
    [SerializeField] private Vector3 volumePointLightPositionWS = new Vector3(1.4f, 1.6f, -2.2f);
    [SerializeField] private Color volumePointLightColor = new Color(1.0f, 0.76f, 0.48f, 1.0f);
    [SerializeField] [Range(0.0f, 64.0f)] private float volumePointLightIntensity = 18.0f;
    [SerializeField] [Min(0.05f)] private float volumePointLightRange = 6.0f;

    [Header("Volume Display")]
    [SerializeField] [Range(0.1f, 4.0f)] private float volumeExposure = 1.15f;
    [SerializeField] private Color volumeColorTint = new Color(1.0f, 0.82f, 0.62f, 1.0f);

    [Header("Debug")]
    [SerializeField] private DebugViewMode debugView = DebugViewMode.Lighting;

    [Header("Driver")]
    [SerializeField] private bool syncEveryFrame = true;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int AmbientStrengthId = Shader.PropertyToID("_AmbientStrength");
    private static readonly int DiffuseStrengthId = Shader.PropertyToID("_DiffuseStrength");
    private static readonly int ReceiveMainLightShadowStrengthId = Shader.PropertyToID("_ReceiveMainLightShadowStrength");
    private static readonly int SdfSoftShadowStrengthId = Shader.PropertyToID("_SdfSoftShadowStrength");
    private static readonly int SdfSoftShadowSharpnessId = Shader.PropertyToID("_SdfSoftShadowSharpness");
    private static readonly int SdfSoftShadowStepsId = Shader.PropertyToID("_SdfSoftShadowSteps");
    private static readonly int SdfSoftShadowStartId = Shader.PropertyToID("_SdfSoftShadowStart");
    private static readonly int SdfSoftShadowDistanceId = Shader.PropertyToID("_SdfSoftShadowDistance");
    private static readonly int SdfSoftShadowNormalBiasId = Shader.PropertyToID("_SdfSoftShadowNormalBias");
    private static readonly int SdfSoftShadowMinStepScaleId = Shader.PropertyToID("_SdfSoftShadowMinStepScale");
    private static readonly int SdfSoftShadowMaxStepFractionId = Shader.PropertyToID("_SdfSoftShadowMaxStepFraction");
    private static readonly int SdfSoftShadowDistanceFadeStartId = Shader.PropertyToID("_SdfSoftShadowDistanceFadeStart");
    private static readonly int CutFaceColorId = Shader.PropertyToID("_CutFaceColor");
    private static readonly int CutFaceBlendId = Shader.PropertyToID("_CutFaceBlend");
    private static readonly int CutFaceDominanceSoftnessId = Shader.PropertyToID("_CutFaceDominanceSoftness");
    private static readonly int CutFaceOcclusionStrengthId = Shader.PropertyToID("_CutFaceOcclusionStrength");
    private static readonly int CutFaceOcclusionDistanceId = Shader.PropertyToID("_CutFaceOcclusionDistance");
    private static readonly int CutFaceBandSoftnessId = Shader.PropertyToID("_CutFaceBandSoftness");
    private static readonly int CutFaceEdgeWidthId = Shader.PropertyToID("_CutFaceEdgeWidth");
    private static readonly int CutFaceEdgeBoostId = Shader.PropertyToID("_CutFaceEdgeBoost");
    private static readonly int CutFaceFreshnessBoostId = Shader.PropertyToID("_CutFaceFreshnessBoost");
    private static readonly int VolumeLightEnabledId = Shader.PropertyToID("_VolumeLightEnabled");
    private static readonly int VolumeLightIntensityId = Shader.PropertyToID("_VolumeLightIntensity");
    private static readonly int VolumeLightDensityId = Shader.PropertyToID("_VolumeLightDensity");
    private static readonly int VolumeLightAnisotropyId = Shader.PropertyToID("_VolumeLightAnisotropy");
    private static readonly int VolumeLightSamplesId = Shader.PropertyToID("_VolumeLightSamples");
    private static readonly int VolumeLightMaxDistanceId = Shader.PropertyToID("_VolumeLightMaxDistance");
    private static readonly int VolumeLightMaxStepLengthId = Shader.PropertyToID("_VolumeLightMaxStepLength");
    private static readonly int VolumeLightShadowStrengthId = Shader.PropertyToID("_VolumeLightShadowStrength");
    private static readonly int VolumeLightShadowBiasId = Shader.PropertyToID("_VolumeLightShadowBias");
    private static readonly int VolumeLightSurfaceFadeDistanceId = Shader.PropertyToID("_VolumeLightSurfaceFadeDistance");
    private static readonly int VolumeLightPlaneBandId = Shader.PropertyToID("_VolumeLightPlaneBand");
    private static readonly int VolumeLightRemovedDepthId = Shader.PropertyToID("_VolumeLightRemovedDepth");
    private static readonly int VolumeLightShapeDepthId = Shader.PropertyToID("_VolumeLightShapeDepth");
    private static readonly int VolumeLightNoiseScaleId = Shader.PropertyToID("_VolumeLightNoiseScale");
    private static readonly int VolumeLightNoiseStrengthId = Shader.PropertyToID("_VolumeLightNoiseStrength");
    private static readonly int VolumeLightNoiseDriftId = Shader.PropertyToID("_VolumeLightNoiseDrift");
    private static readonly int VolumeBaseFogDensityId = Shader.PropertyToID("_VolumeBaseFogDensity");
    private static readonly int VolumeHeightFogStrengthId = Shader.PropertyToID("_VolumeHeightFogStrength");
    private static readonly int VolumeCutFogBoostId = Shader.PropertyToID("_VolumeCutFogBoost");
    private static readonly int VolumeNoiseContrastId = Shader.PropertyToID("_VolumeNoiseContrast");
    private static readonly int VolumeShadowSamplesId = Shader.PropertyToID("_VolumeShadowSamples");
    private static readonly int VolumeShadowMaxDistanceId = Shader.PropertyToID("_VolumeShadowMaxDistance");
    private static readonly int VolumePointLightEnabledId = Shader.PropertyToID("_VolumePointLightEnabled");
    private static readonly int VolumePointLightPositionWSId = Shader.PropertyToID("_VolumePointLightPositionWS");
    private static readonly int VolumePointLightColorId = Shader.PropertyToID("_VolumePointLightColor");
    private static readonly int VolumePointLightIntensityId = Shader.PropertyToID("_VolumePointLightIntensity");
    private static readonly int VolumePointLightRangeId = Shader.PropertyToID("_VolumePointLightRange");
    private static readonly int VolumeExposureId = Shader.PropertyToID("_VolumeExposure");
    private static readonly int VolumeColorTintId = Shader.PropertyToID("_VolumeColorTint");
    private static readonly int MaxStepsId = Shader.PropertyToID("_MaxSteps");
    private static readonly int MaxDistanceId = Shader.PropertyToID("_MaxDistance");
    private static readonly int HitEpsilonId = Shader.PropertyToID("_HitEpsilon");
    private static readonly int NormalEpsilonId = Shader.PropertyToID("_NormalEpsilon");
    private static readonly int ShapeModeId = Shader.PropertyToID("_ShapeMode");
    private static readonly int SphereCenterId = Shader.PropertyToID("_SphereCenter");
    private static readonly int SphereRadiusId = Shader.PropertyToID("_SphereRadius");
    private static readonly int BoxExtentsId = Shader.PropertyToID("_BoxExtents");
    private static readonly int CutPlaneNormalId = Shader.PropertyToID("_CutPlaneNormal");
    private static readonly int CutPlaneOffsetId = Shader.PropertyToID("_CutPlaneOffset");
    private static readonly int ProxyBoundsMinId = Shader.PropertyToID("_ProxyBoundsMin");
    private static readonly int ProxyBoundsMaxId = Shader.PropertyToID("_ProxyBoundsMax");
    private static readonly int DebugViewId = Shader.PropertyToID("_DebugView");

    private Renderer cachedRenderer;
    private MeshFilter cachedMeshFilter;
    private MaterialPropertyBlock propertyBlock;

    private void OnEnable()
    {
        CacheComponents();
        ApplyProperties();
    }

    private void OnValidate()
    {
        CacheComponents();
        ApplyProperties();
    }

    private void Update()
    {
        if (!syncEveryFrame && Application.isPlaying)
        {
            return;
        }

        ApplyProperties();
    }

    private void CacheComponents()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (cachedMeshFilter == null)
        {
            cachedMeshFilter = GetComponent<MeshFilter>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    public void SetDebugView(DebugViewMode mode)
    {
        debugView = mode;
        CacheComponents();
        ApplyProperties();
    }

    public void SetVolumePointLight(bool enabled, Vector3 positionWS, Color color, float intensity, float range)
    {
        volumePointLightEnabled = enabled;
        volumePointLightPositionWS = positionWS;
        volumePointLightColor = color;
        volumePointLightIntensity = Mathf.Max(0.0f, intensity);
        volumePointLightRange = Mathf.Max(0.05f, range);
        CacheComponents();
        ApplyProperties();
    }

    public void ApplyVolumePreset(VolumePreset preset)
    {
        volumeLightEnabled = true;

        switch (preset)
        {
            case VolumePreset.Performance:
                volumeLightIntensity = 2.6f;
                volumeLightDensity = 0.65f;
                volumeLightSamples = 8;
                volumeLightMaxDistance = 0.65f;
                volumeLightMaxStepLength = 0.12f;
                volumeLightShadowStrength = 0.45f;
                volumeBaseFogDensity = 0.0015f;
                volumeHeightFogStrength = 0.05f;
                volumeCutFogBoost = 1.1f;
                volumeNoiseContrast = 1.0f;
                volumeShadowSamples = 8;
                volumeShadowMaxDistance = 1.3f;
                volumePointLightEnabled = true;
                volumePointLightIntensity = 12.0f;
                volumePointLightRange = 5.0f;
                volumeExposure = 1.05f;
                volumeColorTint = new Color(1.0f, 0.82f, 0.66f, 1.0f);
                break;

            case VolumePreset.DiagnosticDense:
                volumeLightIntensity = 3.4f;
                volumeLightDensity = 1.1f;
                volumeLightSamples = 14;
                volumeLightMaxDistance = 0.95f;
                volumeLightMaxStepLength = 0.08f;
                volumeLightShadowStrength = 0.85f;
                volumeBaseFogDensity = 0.008f;
                volumeHeightFogStrength = 0.16f;
                volumeCutFogBoost = 1.8f;
                volumeNoiseContrast = 1.6f;
                volumeShadowSamples = 20;
                volumeShadowMaxDistance = 2.4f;
                volumePointLightEnabled = true;
                volumePointLightIntensity = 20.0f;
                volumePointLightRange = 6.5f;
                volumeExposure = 1.2f;
                volumeColorTint = new Color(1.0f, 0.78f, 0.52f, 1.0f);
                break;

            case VolumePreset.CinematicWarm:
                volumeLightIntensity = 4.0f;
                volumeLightDensity = 0.85f;
                volumeLightSamples = 14;
                volumeLightMaxDistance = 0.9f;
                volumeLightMaxStepLength = 0.09f;
                volumeLightShadowStrength = 0.78f;
                volumeBaseFogDensity = 0.003f;
                volumeHeightFogStrength = 0.11f;
                volumeCutFogBoost = 1.55f;
                volumeNoiseContrast = 1.35f;
                volumeShadowSamples = 18;
                volumeShadowMaxDistance = 2.1f;
                volumePointLightEnabled = true;
                volumePointLightIntensity = 18.0f;
                volumePointLightRange = 6.0f;
                volumeExposure = 1.25f;
                volumeColorTint = new Color(1.0f, 0.78f, 0.54f, 1.0f);
                break;

            default:
                volumeLightIntensity = 3.0f;
                volumeLightDensity = 0.8f;
                volumeLightSamples = 12;
                volumeLightMaxDistance = 0.8f;
                volumeLightMaxStepLength = 0.1f;
                volumeLightShadowStrength = 0.7f;
                volumeBaseFogDensity = 0.002f;
                volumeHeightFogStrength = 0.08f;
                volumeCutFogBoost = 1.4f;
                volumeNoiseContrast = 1.25f;
                volumeShadowSamples = 16;
                volumeShadowMaxDistance = 2.0f;
                volumePointLightEnabled = true;
                volumePointLightIntensity = 16.0f;
                volumePointLightRange = 5.8f;
                volumeExposure = 1.15f;
                volumeColorTint = new Color(1.0f, 0.82f, 0.62f, 1.0f);
                break;
        }

        CacheComponents();
        ApplyProperties();
    }

    private void ApplyProperties()
    {
        if (cachedRenderer == null || cachedMeshFilter == null)
        {
            return;
        }

        Mesh sharedMesh = cachedMeshFilter.sharedMesh;
        if (sharedMesh == null)
        {
            return;
        }

        Bounds meshBounds = sharedMesh.bounds;

        Vector3 normalizedPlaneNormal = cutPlaneNormal.sqrMagnitude > 1e-6f
            ? cutPlaneNormal.normalized
            : Vector3.up;

        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, baseColor);
        propertyBlock.SetFloat(AmbientStrengthId, ambientStrength);
        propertyBlock.SetFloat(DiffuseStrengthId, diffuseStrength);
        propertyBlock.SetFloat(ReceiveMainLightShadowStrengthId, receiveMainLightShadowStrength);
        propertyBlock.SetFloat(SdfSoftShadowStrengthId, sdfSoftShadowStrength);
        propertyBlock.SetFloat(SdfSoftShadowSharpnessId, sdfSoftShadowSharpness);
        propertyBlock.SetFloat(SdfSoftShadowStepsId, sdfSoftShadowSteps);
        propertyBlock.SetFloat(SdfSoftShadowStartId, sdfSoftShadowStart);
        propertyBlock.SetFloat(SdfSoftShadowDistanceId, sdfSoftShadowDistance);
        propertyBlock.SetFloat(SdfSoftShadowNormalBiasId, sdfSoftShadowNormalBias);
        propertyBlock.SetFloat(SdfSoftShadowMinStepScaleId, sdfSoftShadowMinStepScale);
        propertyBlock.SetFloat(SdfSoftShadowMaxStepFractionId, sdfSoftShadowMaxStepFraction);
        propertyBlock.SetFloat(SdfSoftShadowDistanceFadeStartId, sdfSoftShadowDistanceFadeStart);
        propertyBlock.SetColor(CutFaceColorId, cutFaceColor);
        propertyBlock.SetFloat(CutFaceBlendId, cutFaceBlend);
        propertyBlock.SetFloat(CutFaceDominanceSoftnessId, cutFaceDominanceSoftness);
        propertyBlock.SetFloat(CutFaceOcclusionStrengthId, cutFaceOcclusionStrength);
        propertyBlock.SetFloat(CutFaceOcclusionDistanceId, cutFaceOcclusionDistance);
        propertyBlock.SetFloat(CutFaceBandSoftnessId, cutFaceBandSoftness);
        propertyBlock.SetFloat(CutFaceEdgeWidthId, cutFaceEdgeWidth);
        propertyBlock.SetFloat(CutFaceEdgeBoostId, cutFaceEdgeBoost);
        propertyBlock.SetFloat(CutFaceFreshnessBoostId, cutFaceFreshnessBoost);
        propertyBlock.SetFloat(VolumeLightEnabledId, volumeLightEnabled ? 1.0f : 0.0f);
        propertyBlock.SetFloat(VolumeLightIntensityId, volumeLightIntensity);
        propertyBlock.SetFloat(VolumeLightDensityId, volumeLightDensity);
        propertyBlock.SetFloat(VolumeLightAnisotropyId, volumeLightAnisotropy);
        propertyBlock.SetFloat(VolumeLightSamplesId, volumeLightSamples);
        propertyBlock.SetFloat(VolumeLightMaxDistanceId, volumeLightMaxDistance);
        propertyBlock.SetFloat(VolumeLightMaxStepLengthId, volumeLightMaxStepLength);
        propertyBlock.SetFloat(VolumeLightShadowStrengthId, volumeLightShadowStrength);
        propertyBlock.SetFloat(VolumeLightShadowBiasId, volumeLightShadowBias);
        propertyBlock.SetFloat(VolumeLightSurfaceFadeDistanceId, volumeLightSurfaceFadeDistance);
        propertyBlock.SetFloat(VolumeLightPlaneBandId, volumeLightPlaneBand);
        propertyBlock.SetFloat(VolumeLightRemovedDepthId, volumeLightRemovedDepth);
        propertyBlock.SetFloat(VolumeLightShapeDepthId, volumeLightShapeDepth);
        propertyBlock.SetFloat(VolumeLightNoiseScaleId, volumeLightNoiseScale);
        propertyBlock.SetFloat(VolumeLightNoiseStrengthId, volumeLightNoiseStrength);
        propertyBlock.SetFloat(VolumeLightNoiseDriftId, volumeLightNoiseDrift);
        propertyBlock.SetFloat(VolumeBaseFogDensityId, volumeBaseFogDensity);
        propertyBlock.SetFloat(VolumeHeightFogStrengthId, volumeHeightFogStrength);
        propertyBlock.SetFloat(VolumeCutFogBoostId, volumeCutFogBoost);
        propertyBlock.SetFloat(VolumeNoiseContrastId, volumeNoiseContrast);
        propertyBlock.SetFloat(VolumeShadowSamplesId, volumeShadowSamples);
        propertyBlock.SetFloat(VolumeShadowMaxDistanceId, volumeShadowMaxDistance);
        propertyBlock.SetFloat(VolumePointLightEnabledId, volumePointLightEnabled ? 1.0f : 0.0f);
        propertyBlock.SetVector(
            VolumePointLightPositionWSId,
            new Vector4(volumePointLightPositionWS.x, volumePointLightPositionWS.y, volumePointLightPositionWS.z, 1.0f));
        propertyBlock.SetColor(VolumePointLightColorId, volumePointLightColor);
        propertyBlock.SetFloat(VolumePointLightIntensityId, volumePointLightIntensity);
        propertyBlock.SetFloat(VolumePointLightRangeId, volumePointLightRange);
        propertyBlock.SetFloat(VolumeExposureId, volumeExposure);
        propertyBlock.SetColor(VolumeColorTintId, volumeColorTint);
        propertyBlock.SetFloat(DebugViewId, (float)debugView);
        propertyBlock.SetFloat(MaxStepsId, maxSteps);
        propertyBlock.SetFloat(MaxDistanceId, maxDistance);
        propertyBlock.SetFloat(HitEpsilonId, hitEpsilon);
        propertyBlock.SetFloat(NormalEpsilonId, normalEpsilon);
        propertyBlock.SetFloat(ShapeModeId, (float)shapeMode);
        propertyBlock.SetVector(SphereCenterId, sphereCenter);
        propertyBlock.SetFloat(SphereRadiusId, sphereRadius);
        propertyBlock.SetVector(BoxExtentsId, boxExtents);
        propertyBlock.SetVector(CutPlaneNormalId, normalizedPlaneNormal);
        propertyBlock.SetFloat(CutPlaneOffsetId, cutPlaneOffset);
        propertyBlock.SetVector(ProxyBoundsMinId, meshBounds.min);
        propertyBlock.SetVector(ProxyBoundsMaxId, meshBounds.max);

        cachedRenderer.SetPropertyBlock(propertyBlock);
    }
}
