using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(MeshFilter))]
public class SdfRaymarchDriver : MonoBehaviour
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
        VolumeMediaTransmittance = 12,
        VolumeSigmaA = 13,
        VolumeSigmaS = 14,
        VolumeSigmaT = 15,
        SdfSoftShadowReadable = 16,
        VolumeShapeMask = 17,
        SurfaceAmbientOcclusion = 18
    }

    public enum VolumePreset
    {
        Balanced = 0,
        CinematicWarm = 1,
        Performance = 2,
        DiagnosticDense = 3
    }

    public enum VolumeContributionMode
    {
        Full = 0,
        SurfaceOnly = 1,
        Disabled = 2,
        VolumeOnly = 3
    }

    public enum VolumeFogShapeMode
    {
        ProxyBox = 0,
        Ellipsoid = 1,
        CapsuleY = 2,
        NoiseErodedEllipsoid = 3
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
    [SerializeField] [Range(0.0f, 1.0f)] private float sdfSoftShadowSceneStrength = 0.55f;
    [SerializeField] [Min(0.0f)] private float sdfSoftShadowSelfIgnoreDistance = 0.035f;
    [SerializeField] [Range(0.0f, 1.0f)] private float sdfAmbientOcclusionStrength = 0.45f;
    [SerializeField] [Min(0.001f)] private float sdfAmbientOcclusionRadius = 0.18f;
    [SerializeField] [Range(1, 8)] private int sdfAmbientOcclusionSteps = 4;
    [SerializeField] [Min(0.0f)] private float sdfAmbientOcclusionBias = 0.004f;

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
    [SerializeField] private VolumeContributionMode volumeContributionMode = VolumeContributionMode.Full;
    [SerializeField] [Range(0.0f, 8.0f)] private float volumeLightIntensity = 3.0f;
    [SerializeField] [Range(0.0f, 8.0f)] private float volumeLightDensity = 0.8f;
    [SerializeField] [Range(-0.8f, 0.8f)] private float volumeLightAnisotropy = 0.15f;
    [SerializeField] [Range(4, 32)] private int volumeLightSamples = 20;
    [SerializeField] [Range(0.0f, 1.0f)] private float volumeSampleJitterStrength = 0.85f;
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
    [SerializeField] [Range(0.0f, 0.08f)] private float volumeBaseFogDensity = 0.0f;
    [SerializeField] [Range(0.0f, 0.5f)] private float volumeHeightFogStrength = 0.008f;
    [SerializeField] [Range(0.0f, 4.0f)] private float volumeCutFogBoost = 0.95f;
    [SerializeField] [Range(0.25f, 4.0f)] private float volumeNoiseContrast = 1.25f;
    [SerializeField] [Range(0.0f, 8.0f)] private float volumeAbsorptionDensity = 0.18f;
    [SerializeField] [Range(0.0f, 0.2f)] private float volumeDensityThreshold = 0.045f;
    [SerializeField] [Range(0.0f, 0.05f)] private float volumeAlphaClipThreshold = 0.012f;
    [SerializeField] [Range(0.0f, 4.0f)] private float volumeEmissionIntensity = 0.0f;
    [SerializeField] private Color volumeEmissionColor = Color.black;

    [Header("Volume Ambient Mist")]
    [SerializeField] private bool volumeAmbientMistEnabled = false;
    [SerializeField] [Range(0.0f, 0.04f)] private float volumeAmbientMistDensity = 0.0f;
    [SerializeField] [Range(0.0f, 1.0f)] private float volumeAmbientMistHeightFalloff = 0.35f;
    [SerializeField] [Range(0.02f, 2.0f)] private float volumeMovingFogMaxDensity = 0.45f;
    [SerializeField] [Range(0.0f, 1.0f)] private float volumeMovingFogCompression = 0.0f;

    [Header("Volume Shape")]
    [SerializeField] private VolumeFogShapeMode volumeFogShapeMode = VolumeFogShapeMode.NoiseErodedEllipsoid;
    [SerializeField] private Vector3 volumeFogShapeCenter = Vector3.zero;
    [SerializeField] private Vector3 volumeFogShapeExtents = new Vector3(0.48f, 0.42f, 0.48f);
    [SerializeField] [Min(0.01f)] private float volumeFogShapeRadius = 0.46f;
    [SerializeField] [Min(0.01f)] private float volumeFogShapeHeight = 0.92f;
    [SerializeField] [Min(0.001f)] private float volumeFogShapeEdgeSoftness = 0.12f;
    [SerializeField] [Range(0.0f, 1.0f)] private float volumeFogShapeNoiseErosion = 0.22f;
    [SerializeField] [Min(0.1f)] private float volumeFogShapeNoiseScale = 2.2f;
    [SerializeField] [Range(0.0f, 1.0f)] private float volumeCloudCoverage = 0.42f;
    [SerializeField] [Range(0.01f, 1.0f)] private float volumeCloudSoftness = 0.28f;
    [SerializeField] [Range(0.0f, 1.0f)] private float volumeCloudDetailStrength = 0.32f;
    [SerializeField] [Min(0.1f)] private float volumeCloudDetailScale = 7.0f;
    [SerializeField] [Range(0.0f, 1.5f)] private float volumeCloudWarpStrength = 0.35f;
    [SerializeField] [Range(1, 24)] private int volumeCloudLobeCount = 12;
    [SerializeField] private Vector3 volumeCloudLobeSpread = new Vector3(0.95f, 0.62f, 0.9f);
    [SerializeField] [Range(0.05f, 1.0f)] private float volumeCloudLobeRadius = 0.34f;
    [SerializeField] [Range(0.0f, 4.0f)] private float volumeCloudDensityBoost = 0.95f;

    [Header("Volume Shadow")]
    [SerializeField] [Range(4, 64)] private int volumeShadowSamples = 16;
    [SerializeField] [Min(0.05f)] private float volumeShadowMaxDistance = 2.0f;
    [SerializeField] [Range(1.0f, 64.0f)] private float volumeGeometryShadowSharpness = 12.0f;
    [SerializeField] [Range(0.25f, 4.0f)] private float volumeGeometryShadowMinStepScale = 1.0f;
    [SerializeField] [Range(0.0f, 1.0f)] private float volumeSurfaceOcclusionStrength = 0.28f;
    [SerializeField] [Min(0.001f)] private float volumeSurfaceOcclusionRadius = 0.22f;

    [Header("Volume Point Light")]
    [SerializeField] private bool volumePointLightEnabled = true;
    [SerializeField] private Vector3 volumePointLightPositionWS = new Vector3(1.4f, 1.6f, -2.2f);
    [SerializeField] private Color volumePointLightColor = new Color(1.0f, 0.76f, 0.48f, 1.0f);
    [SerializeField] [Range(0.0f, 64.0f)] private float volumePointLightIntensity = 12.0f;
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
    private static readonly int SdfSoftShadowSceneStrengthId = Shader.PropertyToID("_SdfSoftShadowSceneStrength");
    private static readonly int SdfSoftShadowSelfIgnoreDistanceId = Shader.PropertyToID("_SdfSoftShadowSelfIgnoreDistance");
    private static readonly int SdfAmbientOcclusionStrengthId = Shader.PropertyToID("_SdfAmbientOcclusionStrength");
    private static readonly int SdfAmbientOcclusionRadiusId = Shader.PropertyToID("_SdfAmbientOcclusionRadius");
    private static readonly int SdfAmbientOcclusionStepsId = Shader.PropertyToID("_SdfAmbientOcclusionSteps");
    private static readonly int SdfAmbientOcclusionBiasId = Shader.PropertyToID("_SdfAmbientOcclusionBias");
    private static readonly int CutFaceColorId = Shader.PropertyToID("_CutFaceColor");
    private static readonly int CutFaceBlendId = Shader.PropertyToID("_CutFaceBlend");
    private static readonly int CutFaceDominanceSoftnessId = Shader.PropertyToID("_CutFaceDominanceSoftness");
    private static readonly int CutFaceOcclusionStrengthId = Shader.PropertyToID("_CutFaceOcclusionStrength");
    private static readonly int CutFaceOcclusionDistanceId = Shader.PropertyToID("_CutFaceOcclusionDistance");
    private static readonly int CutFaceBandSoftnessId = Shader.PropertyToID("_CutFaceBandSoftness");
    private static readonly int CutFaceEdgeWidthId = Shader.PropertyToID("_CutFaceEdgeWidth");
    private static readonly int CutFaceEdgeBoostId = Shader.PropertyToID("_CutFaceEdgeBoost");
    private static readonly int CutFaceFreshnessBoostId = Shader.PropertyToID("_CutFaceFreshnessBoost");
    private static readonly int SdfSurfaceContributionId = Shader.PropertyToID("_SdfSurfaceContribution");
    private static readonly int UseSceneSdfId = Shader.PropertyToID("_UseSceneSdf");
    private static readonly int VolumeLightEnabledId = Shader.PropertyToID("_VolumeLightEnabled");
    private static readonly int VolumeSurfaceContributionId = Shader.PropertyToID("_VolumeSurfaceContribution");
    private static readonly int VolumeBackgroundContributionId = Shader.PropertyToID("_VolumeBackgroundContribution");
    private static readonly int VolumeLightIntensityId = Shader.PropertyToID("_VolumeLightIntensity");
    private static readonly int VolumeLightDensityId = Shader.PropertyToID("_VolumeLightDensity");
    private static readonly int VolumeLightAnisotropyId = Shader.PropertyToID("_VolumeLightAnisotropy");
    private static readonly int VolumeLightSamplesId = Shader.PropertyToID("_VolumeLightSamples");
    private static readonly int VolumeSampleJitterStrengthId = Shader.PropertyToID("_VolumeSampleJitterStrength");
    private static readonly int VolumeLightMaxDistanceId = Shader.PropertyToID("_VolumeLightMaxDistance");
    private static readonly int VolumeLightMaxStepLengthId = Shader.PropertyToID("_VolumeLightMaxStepLength");
    private static readonly int VolumeLightShadowStrengthId = Shader.PropertyToID("_VolumeLightShadowStrength");
    private static readonly int VolumeLightShadowBiasId = Shader.PropertyToID("_VolumeLightShadowBias");
    private static readonly int VolumeLightSurfaceFadeDistanceId = Shader.PropertyToID("_VolumeLightSurfaceFadeDistance");
    private static readonly int VolumeSurfaceOcclusionStrengthId = Shader.PropertyToID("_VolumeSurfaceOcclusionStrength");
    private static readonly int VolumeSurfaceOcclusionRadiusId = Shader.PropertyToID("_VolumeSurfaceOcclusionRadius");
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
    private static readonly int VolumeAbsorptionDensityId = Shader.PropertyToID("_VolumeAbsorptionDensity");
    private static readonly int VolumeDensityThresholdId = Shader.PropertyToID("_VolumeDensityThreshold");
    private static readonly int VolumeAlphaClipThresholdId = Shader.PropertyToID("_VolumeAlphaClipThreshold");
    private static readonly int VolumeEmissionIntensityId = Shader.PropertyToID("_VolumeEmissionIntensity");
    private static readonly int VolumeEmissionColorId = Shader.PropertyToID("_VolumeEmissionColor");
    private static readonly int VolumeAmbientMistEnabledId = Shader.PropertyToID("_VolumeAmbientMistEnabled");
    private static readonly int VolumeAmbientMistDensityId = Shader.PropertyToID("_VolumeAmbientMistDensity");
    private static readonly int VolumeAmbientMistHeightFalloffId = Shader.PropertyToID("_VolumeAmbientMistHeightFalloff");
    private static readonly int VolumeMovingFogMaxDensityId = Shader.PropertyToID("_VolumeMovingFogMaxDensity");
    private static readonly int VolumeMovingFogCompressionId = Shader.PropertyToID("_VolumeMovingFogCompression");
    private static readonly int VolumeFogShapeModeId = Shader.PropertyToID("_VolumeFogShapeMode");
    private static readonly int VolumeFogShapeCenterId = Shader.PropertyToID("_VolumeFogShapeCenter");
    private static readonly int VolumeFogShapeExtentsId = Shader.PropertyToID("_VolumeFogShapeExtents");
    private static readonly int VolumeFogShapeRadiusId = Shader.PropertyToID("_VolumeFogShapeRadius");
    private static readonly int VolumeFogShapeHeightId = Shader.PropertyToID("_VolumeFogShapeHeight");
    private static readonly int VolumeFogShapeEdgeSoftnessId = Shader.PropertyToID("_VolumeFogShapeEdgeSoftness");
    private static readonly int VolumeFogShapeNoiseErosionId = Shader.PropertyToID("_VolumeFogShapeNoiseErosion");
    private static readonly int VolumeFogShapeNoiseScaleId = Shader.PropertyToID("_VolumeFogShapeNoiseScale");
    private static readonly int VolumeCloudCoverageId = Shader.PropertyToID("_VolumeCloudCoverage");
    private static readonly int VolumeCloudSoftnessId = Shader.PropertyToID("_VolumeCloudSoftness");
    private static readonly int VolumeCloudDetailStrengthId = Shader.PropertyToID("_VolumeCloudDetailStrength");
    private static readonly int VolumeCloudDetailScaleId = Shader.PropertyToID("_VolumeCloudDetailScale");
    private static readonly int VolumeCloudWarpStrengthId = Shader.PropertyToID("_VolumeCloudWarpStrength");
    private static readonly int VolumeCloudLobeCountId = Shader.PropertyToID("_VolumeCloudLobeCount");
    private static readonly int VolumeCloudLobeSpreadId = Shader.PropertyToID("_VolumeCloudLobeSpread");
    private static readonly int VolumeCloudLobeRadiusId = Shader.PropertyToID("_VolumeCloudLobeRadius");
    private static readonly int VolumeCloudDensityBoostId = Shader.PropertyToID("_VolumeCloudDensityBoost");
    private static readonly int VolumeShadowSamplesId = Shader.PropertyToID("_VolumeShadowSamples");
    private static readonly int VolumeShadowMaxDistanceId = Shader.PropertyToID("_VolumeShadowMaxDistance");
    private static readonly int VolumeGeometryShadowSharpnessId = Shader.PropertyToID("_VolumeGeometryShadowSharpness");
    private static readonly int VolumeGeometryShadowMinStepScaleId = Shader.PropertyToID("_VolumeGeometryShadowMinStepScale");
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
    private static readonly int SdfSceneShapeCountId = Shader.PropertyToID("_SdfSceneShapeCount");
    private static readonly int SdfScreenSpaceVolumeEnabledId = Shader.PropertyToID("_SdfScreenSpaceVolumeEnabled");
    private static readonly int SdfVolumeWorldToLocalId = Shader.PropertyToID("_SdfVolumeWorldToLocal");
    private static readonly int SdfVolumeLocalToWorldId = Shader.PropertyToID("_SdfVolumeLocalToWorld");
    private static readonly int SdfVolumeVisibilityModeId = Shader.PropertyToID("_SdfVolumeVisibilityMode");

    private Renderer cachedRenderer;
    private MeshFilter cachedMeshFilter;
    private MaterialPropertyBlock propertyBlock;
    [SerializeField, HideInInspector] private int sceneDataVersion = 1;
    [SerializeField, HideInInspector] private int materialPropertyUploadVersion;
    [SerializeField, HideInInspector] private int screenSpaceGlobalUploadVersion;
    private int lastSceneDataHash = int.MinValue;
    private int lastMaterialPropertyHash = int.MinValue;
    private int lastScreenSpaceGlobalHash = int.MinValue;
    private static bool screenSpaceVolumeGlobalDisabled = true;

    private void OnEnable()
    {
        CacheComponents();
        lastMaterialPropertyHash = int.MinValue;
        lastScreenSpaceGlobalHash = int.MinValue;
        RefreshSceneDataVersion();
        SdfSceneRegistry.Register(this);
        ApplyProperties();
    }

    private void OnDisable()
    {
        SdfSceneRegistry.Unregister(this);
    }

    private void OnValidate()
    {
        CacheComponents();
        RefreshSceneDataVersion();
        SdfSceneRegistry.MarkDirty(this);
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

    public void ConfigureShapeSettings(
        ShapeMode mode,
        Vector3 sphereCenter,
        float sphereRadius,
        Vector3 boxExtents,
        Vector3 baseCutPlaneNormal,
        float baseCutPlaneOffset)
    {
        shapeMode = mode;
        this.sphereCenter = sphereCenter;
        this.sphereRadius = Mathf.Max(0.01f, sphereRadius);
        this.boxExtents = new Vector3(
            Mathf.Max(0.01f, boxExtents.x),
            Mathf.Max(0.01f, boxExtents.y),
            Mathf.Max(0.01f, boxExtents.z));
        cutPlaneNormal = baseCutPlaneNormal.sqrMagnitude > 1e-6f ? baseCutPlaneNormal.normalized : Vector3.up;
        cutPlaneOffset = baseCutPlaneOffset;
        CacheComponents();
        RefreshSceneDataVersion();
        SdfSceneRegistry.MarkDirty(this);
        ApplyProperties();
    }

    public void SetVolumeContributionMode(VolumeContributionMode mode)
    {
        if (volumeContributionMode == mode)
        {
            return;
        }

        volumeContributionMode = mode;
        CacheComponents();
        ApplyProperties();
    }

    public Bounds GetWorldBounds()
    {
        CacheComponents();
        return cachedRenderer != null ? cachedRenderer.bounds : new Bounds(transform.position, Vector3.zero);
    }

    public Material GetSharedMaterial()
    {
        CacheComponents();
        return cachedRenderer != null ? cachedRenderer.sharedMaterial : null;
    }

    public ShapeMode CurrentShapeMode => shapeMode;
    public Vector3 SphereCenter => sphereCenter;
    public float SphereRadius => sphereRadius;
    public Vector3 BoxExtents => boxExtents;
    public Vector3 BaseCutPlaneNormal => cutPlaneNormal.sqrMagnitude > 1e-6f ? cutPlaneNormal.normalized : Vector3.up;
    public float BaseCutPlaneOffset => cutPlaneOffset;
    public int SceneDataVersion
    {
        get
        {
            RefreshSceneDataVersion();
            return sceneDataVersion;
        }
    }

    public int VolumeLightSampleCount => volumeLightSamples;
    public int VolumeShadowSampleCount => volumeShadowSamples;
    public int MaterialPropertyUploadVersion => materialPropertyUploadVersion;
    public int ScreenSpaceGlobalUploadVersion => screenSpaceGlobalUploadVersion;

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

    public void SetVolumeMediumSettings(
        float lightIntensity,
        float lightDensity,
        float baseFogDensity,
        float heightFogStrength,
        float cutFogBoost,
        float absorptionDensity,
        float exposure,
        Color colorTint)
    {
        volumeLightIntensity = Mathf.Max(0.0f, lightIntensity);
        volumeLightDensity = Mathf.Max(0.0f, lightDensity);
        volumeBaseFogDensity = Mathf.Clamp(baseFogDensity, 0.0f, 0.08f);
        volumeHeightFogStrength = Mathf.Clamp(heightFogStrength, 0.0f, 0.5f);
        volumeCutFogBoost = Mathf.Clamp(cutFogBoost, 0.0f, 4.0f);
        volumeAbsorptionDensity = Mathf.Max(0.0f, absorptionDensity);
        volumeExposure = Mathf.Max(0.1f, exposure);
        volumeColorTint = colorTint;
        CacheComponents();
        ApplyProperties();
    }

    public void SetVolumeEmissionSettings(float intensity, Color color)
    {
        volumeEmissionIntensity = Mathf.Max(0.0f, intensity);
        volumeEmissionColor = color;
        CacheComponents();
        ApplyProperties();
    }

    public void SetVolumeAmbientMistSettings(
        bool enabled,
        float ambientMistDensity,
        float ambientMistHeightFalloff,
        float movingFogMaxDensity,
        float movingFogCompression)
    {
        volumeAmbientMistEnabled = enabled;
        volumeAmbientMistDensity = Mathf.Clamp(ambientMistDensity, 0.0f, 0.04f);
        volumeAmbientMistHeightFalloff = Mathf.Clamp01(ambientMistHeightFalloff);
        volumeMovingFogMaxDensity = Mathf.Clamp(movingFogMaxDensity, 0.02f, 2.0f);
        volumeMovingFogCompression = Mathf.Clamp01(movingFogCompression);
        CacheComponents();
        ApplyProperties();
    }

    public void SetVolumeVisibilitySettings(float densityThreshold, float alphaClipThreshold)
    {
        volumeDensityThreshold = Mathf.Clamp(densityThreshold, 0.0f, 0.2f);
        volumeAlphaClipThreshold = Mathf.Clamp(alphaClipThreshold, 0.0f, 0.05f);
        CacheComponents();
        ApplyProperties();
    }

    public bool UploadScreenSpaceVolumeGlobals(Transform volumeTransform, bool hasSceneSdf, int visibilityMode)
    {
        if (volumeTransform == null)
        {
            return false;
        }

        Vector3 safeVolumeFogShapeExtents = new Vector3(
            Mathf.Max(volumeFogShapeExtents.x, 0.01f),
            Mathf.Max(volumeFogShapeExtents.y, 0.01f),
            Mathf.Max(volumeFogShapeExtents.z, 0.01f));
        bool volumeModeCanRender = volumeContributionMode == VolumeContributionMode.Full
            || volumeContributionMode == VolumeContributionMode.VolumeOnly;
        bool volumeEnabled = volumeLightEnabled && volumeModeCanRender;
        int globalHash = CalculateScreenSpaceGlobalHash(
            volumeTransform,
            hasSceneSdf,
            visibilityMode,
            safeVolumeFogShapeExtents,
            volumeEnabled);
        if (globalHash == lastScreenSpaceGlobalHash && screenSpaceVolumeGlobalDisabled == !volumeEnabled)
        {
            return false;
        }

        Shader.SetGlobalFloat(SdfScreenSpaceVolumeEnabledId, volumeEnabled ? 1.0f : 0.0f);
        Shader.SetGlobalMatrix(SdfVolumeWorldToLocalId, volumeTransform.worldToLocalMatrix);
        Shader.SetGlobalMatrix(SdfVolumeLocalToWorldId, volumeTransform.localToWorldMatrix);
        Shader.SetGlobalFloat(SdfVolumeVisibilityModeId, Mathf.Clamp(visibilityMode, 0, 1));
        Shader.SetGlobalFloat(UseSceneSdfId, hasSceneSdf ? 1.0f : 0.0f);
        Shader.SetGlobalFloat(VolumeLightEnabledId, volumeEnabled ? 1.0f : 0.0f);
        Shader.SetGlobalFloat(VolumeBackgroundContributionId, volumeEnabled ? 1.0f : 0.0f);
        Shader.SetGlobalFloat(VolumeLightIntensityId, volumeLightIntensity);
        Shader.SetGlobalFloat(VolumeLightDensityId, volumeLightDensity);
        Shader.SetGlobalFloat(VolumeLightAnisotropyId, volumeLightAnisotropy);
        Shader.SetGlobalFloat(VolumeLightSamplesId, volumeLightSamples);
        Shader.SetGlobalFloat(VolumeSampleJitterStrengthId, volumeSampleJitterStrength);
        Shader.SetGlobalFloat(VolumeLightMaxDistanceId, volumeLightMaxDistance);
        Shader.SetGlobalFloat(VolumeLightMaxStepLengthId, volumeLightMaxStepLength);
        Shader.SetGlobalFloat(VolumeLightShadowStrengthId, volumeLightShadowStrength);
        Shader.SetGlobalFloat(VolumeLightShadowBiasId, volumeLightShadowBias);
        Shader.SetGlobalFloat(VolumeLightSurfaceFadeDistanceId, volumeLightSurfaceFadeDistance);
        Shader.SetGlobalFloat(VolumeSurfaceOcclusionStrengthId, volumeSurfaceOcclusionStrength);
        Shader.SetGlobalFloat(VolumeSurfaceOcclusionRadiusId, volumeSurfaceOcclusionRadius);
        Shader.SetGlobalFloat(VolumeLightPlaneBandId, volumeLightPlaneBand);
        Shader.SetGlobalFloat(VolumeLightRemovedDepthId, volumeLightRemovedDepth);
        Shader.SetGlobalFloat(VolumeLightShapeDepthId, volumeLightShapeDepth);
        Shader.SetGlobalFloat(VolumeLightNoiseScaleId, volumeLightNoiseScale);
        Shader.SetGlobalFloat(VolumeLightNoiseStrengthId, volumeLightNoiseStrength);
        Shader.SetGlobalFloat(VolumeLightNoiseDriftId, volumeLightNoiseDrift);
        Shader.SetGlobalFloat(VolumeBaseFogDensityId, volumeBaseFogDensity);
        Shader.SetGlobalFloat(VolumeHeightFogStrengthId, volumeHeightFogStrength);
        Shader.SetGlobalFloat(VolumeCutFogBoostId, volumeCutFogBoost);
        Shader.SetGlobalFloat(VolumeNoiseContrastId, volumeNoiseContrast);
        Shader.SetGlobalFloat(VolumeAbsorptionDensityId, volumeAbsorptionDensity);
        Shader.SetGlobalFloat(VolumeDensityThresholdId, volumeDensityThreshold);
        Shader.SetGlobalFloat(VolumeAlphaClipThresholdId, volumeAlphaClipThreshold);
        Shader.SetGlobalFloat(VolumeEmissionIntensityId, volumeEmissionIntensity);
        Shader.SetGlobalColor(VolumeEmissionColorId, volumeEmissionColor);
        Shader.SetGlobalFloat(VolumeAmbientMistEnabledId, volumeAmbientMistEnabled ? 1.0f : 0.0f);
        Shader.SetGlobalFloat(VolumeAmbientMistDensityId, volumeAmbientMistDensity);
        Shader.SetGlobalFloat(VolumeAmbientMistHeightFalloffId, volumeAmbientMistHeightFalloff);
        Shader.SetGlobalFloat(VolumeMovingFogMaxDensityId, volumeMovingFogMaxDensity);
        Shader.SetGlobalFloat(VolumeMovingFogCompressionId, volumeMovingFogCompression);
        Shader.SetGlobalFloat(VolumeFogShapeModeId, (float)volumeFogShapeMode);
        Shader.SetGlobalVector(VolumeFogShapeCenterId, volumeFogShapeCenter);
        Shader.SetGlobalVector(VolumeFogShapeExtentsId, safeVolumeFogShapeExtents);
        Shader.SetGlobalFloat(VolumeFogShapeRadiusId, volumeFogShapeRadius);
        Shader.SetGlobalFloat(VolumeFogShapeHeightId, volumeFogShapeHeight);
        Shader.SetGlobalFloat(VolumeFogShapeEdgeSoftnessId, volumeFogShapeEdgeSoftness);
        Shader.SetGlobalFloat(VolumeFogShapeNoiseErosionId, volumeFogShapeNoiseErosion);
        Shader.SetGlobalFloat(VolumeFogShapeNoiseScaleId, volumeFogShapeNoiseScale);
        Shader.SetGlobalFloat(VolumeCloudCoverageId, volumeCloudCoverage);
        Shader.SetGlobalFloat(VolumeCloudSoftnessId, volumeCloudSoftness);
        Shader.SetGlobalFloat(VolumeCloudDetailStrengthId, volumeCloudDetailStrength);
        Shader.SetGlobalFloat(VolumeCloudDetailScaleId, volumeCloudDetailScale);
        Shader.SetGlobalFloat(VolumeCloudWarpStrengthId, volumeCloudWarpStrength);
        Shader.SetGlobalFloat(VolumeCloudLobeCountId, volumeCloudLobeCount);
        Shader.SetGlobalVector(VolumeCloudLobeSpreadId, volumeCloudLobeSpread);
        Shader.SetGlobalFloat(VolumeCloudLobeRadiusId, volumeCloudLobeRadius);
        Shader.SetGlobalFloat(VolumeCloudDensityBoostId, volumeCloudDensityBoost);
        Shader.SetGlobalFloat(VolumeShadowSamplesId, volumeShadowSamples);
        Shader.SetGlobalFloat(VolumeShadowMaxDistanceId, volumeShadowMaxDistance);
        Shader.SetGlobalFloat(VolumeGeometryShadowSharpnessId, volumeGeometryShadowSharpness);
        Shader.SetGlobalFloat(VolumeGeometryShadowMinStepScaleId, volumeGeometryShadowMinStepScale);
        Shader.SetGlobalFloat(VolumePointLightEnabledId, volumePointLightEnabled ? 1.0f : 0.0f);
        Shader.SetGlobalVector(
            VolumePointLightPositionWSId,
            new Vector4(volumePointLightPositionWS.x, volumePointLightPositionWS.y, volumePointLightPositionWS.z, 1.0f));
        Shader.SetGlobalColor(VolumePointLightColorId, volumePointLightColor);
        Shader.SetGlobalFloat(VolumePointLightIntensityId, volumePointLightIntensity);
        Shader.SetGlobalFloat(VolumePointLightRangeId, volumePointLightRange);
        Shader.SetGlobalFloat(VolumeExposureId, volumeExposure);
        Shader.SetGlobalColor(VolumeColorTintId, volumeColorTint);
        Shader.SetGlobalFloat(DebugViewId, (float)debugView);
        Shader.SetGlobalFloat(MaxDistanceId, maxDistance);
        Shader.SetGlobalFloat(HitEpsilonId, hitEpsilon);
        Shader.SetGlobalVector(ProxyBoundsMinId, new Vector4(-0.5f, -0.5f, -0.5f, 0.0f));
        Shader.SetGlobalVector(ProxyBoundsMaxId, new Vector4(0.5f, 0.5f, 0.5f, 0.0f));
        lastScreenSpaceGlobalHash = globalHash;
        screenSpaceVolumeGlobalDisabled = !volumeEnabled;
        unchecked
        {
            screenSpaceGlobalUploadVersion++;
        }

        return true;
    }

    public static void DisableScreenSpaceVolumeGlobals()
    {
        if (screenSpaceVolumeGlobalDisabled)
        {
            return;
        }

        Shader.SetGlobalFloat(SdfScreenSpaceVolumeEnabledId, 0.0f);
        screenSpaceVolumeGlobalDisabled = true;
    }

    public void SetCloudShapeSettings(
        VolumeFogShapeMode shapeMode,
        Vector3 shapeExtents,
        float edgeSoftness,
        float noiseErosion,
        float noiseScale,
        float coverage,
        float softness,
        float detailStrength,
        float detailScale,
        float warpStrength,
        int lobeCount,
        Vector3 lobeSpread,
        float lobeRadius,
        float densityBoost)
    {
        volumeFogShapeMode = shapeMode;
        volumeFogShapeExtents = new Vector3(
            Mathf.Max(shapeExtents.x, 0.01f),
            Mathf.Max(shapeExtents.y, 0.01f),
            Mathf.Max(shapeExtents.z, 0.01f));
        volumeFogShapeEdgeSoftness = Mathf.Max(edgeSoftness, 0.001f);
        volumeFogShapeNoiseErosion = Mathf.Clamp01(noiseErosion);
        volumeFogShapeNoiseScale = Mathf.Max(noiseScale, 0.1f);
        volumeCloudCoverage = Mathf.Clamp01(coverage);
        volumeCloudSoftness = Mathf.Clamp(softness, 0.01f, 1.0f);
        volumeCloudDetailStrength = Mathf.Clamp01(detailStrength);
        volumeCloudDetailScale = Mathf.Max(detailScale, 0.1f);
        volumeCloudWarpStrength = Mathf.Clamp(warpStrength, 0.0f, 1.5f);
        volumeCloudLobeCount = Mathf.Clamp(lobeCount, 1, 24);
        volumeCloudLobeSpread = new Vector3(
            Mathf.Max(lobeSpread.x, 0.0f),
            Mathf.Max(lobeSpread.y, 0.0f),
            Mathf.Max(lobeSpread.z, 0.0f));
        volumeCloudLobeRadius = Mathf.Clamp(lobeRadius, 0.05f, 1.0f);
        volumeCloudDensityBoost = Mathf.Clamp(densityBoost, 0.0f, 4.0f);
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
                volumeLightDensity = 0.48f;
                volumeLightSamples = 8;
                volumeLightMaxDistance = 0.65f;
                volumeLightMaxStepLength = 0.12f;
                volumeLightShadowStrength = 0.45f;
                volumeBaseFogDensity = 0.0f;
                volumeHeightFogStrength = 0.004f;
                volumeCutFogBoost = 0.65f;
                volumeNoiseContrast = 1.0f;
                volumeAbsorptionDensity = 0.08f;
                volumeDensityThreshold = 0.05f;
                volumeAlphaClipThreshold = 0.014f;
                volumeEmissionIntensity = 0.0f;
                volumeEmissionColor = Color.black;
                volumeFogShapeMode = VolumeFogShapeMode.Ellipsoid;
                volumeFogShapeExtents = new Vector3(0.5f, 0.38f, 0.5f);
                volumeFogShapeEdgeSoftness = 0.13f;
                volumeFogShapeNoiseErosion = 0.1f;
                volumeCloudCoverage = 0.46f;
                volumeCloudSoftness = 0.38f;
                volumeCloudDetailStrength = 0.18f;
                volumeCloudDetailScale = 5.0f;
                volumeCloudWarpStrength = 0.15f;
                volumeCloudLobeCount = 8;
                volumeCloudLobeSpread = new Vector3(0.9f, 0.5f, 0.85f);
                volumeCloudLobeRadius = 0.38f;
                volumeCloudDensityBoost = 0.55f;
                volumeShadowSamples = 8;
                volumeShadowMaxDistance = 1.3f;
                volumePointLightEnabled = true;
                volumePointLightIntensity = 8.0f;
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
                volumeAbsorptionDensity = 0.28f;
                volumeDensityThreshold = 0.0f;
                volumeAlphaClipThreshold = 0.001f;
                volumeEmissionIntensity = 0.0f;
                volumeEmissionColor = Color.black;
                volumeFogShapeMode = VolumeFogShapeMode.ProxyBox;
                volumeFogShapeExtents = new Vector3(0.5f, 0.5f, 0.5f);
                volumeFogShapeEdgeSoftness = 0.08f;
                volumeFogShapeNoiseErosion = 0.0f;
                volumeCloudCoverage = 0.15f;
                volumeCloudSoftness = 0.18f;
                volumeCloudDetailStrength = 0.0f;
                volumeCloudDetailScale = 7.0f;
                volumeCloudWarpStrength = 0.0f;
                volumeCloudLobeCount = 1;
                volumeCloudLobeSpread = new Vector3(0.0f, 0.0f, 0.0f);
                volumeCloudLobeRadius = 0.7f;
                volumeCloudDensityBoost = 0.0f;
                volumeShadowSamples = 20;
                volumeShadowMaxDistance = 2.4f;
                volumePointLightEnabled = true;
                volumePointLightIntensity = 20.0f;
                volumePointLightRange = 6.5f;
                volumeExposure = 1.2f;
                volumeColorTint = new Color(1.0f, 0.78f, 0.52f, 1.0f);
                break;

            case VolumePreset.CinematicWarm:
                volumeLightIntensity = 2.8f;
                volumeLightDensity = 0.82f;
                volumeLightSamples = 14;
                volumeLightMaxDistance = 0.9f;
                volumeLightMaxStepLength = 0.09f;
                volumeLightShadowStrength = 0.78f;
                volumeBaseFogDensity = 0.0f;
                volumeHeightFogStrength = 0.008f;
                volumeCutFogBoost = 0.95f;
                volumeNoiseContrast = 1.35f;
                volumeAbsorptionDensity = 0.18f;
                volumeDensityThreshold = 0.045f;
                volumeAlphaClipThreshold = 0.012f;
                volumeEmissionIntensity = 0.0f;
                volumeEmissionColor = Color.black;
                volumeFogShapeMode = VolumeFogShapeMode.NoiseErodedEllipsoid;
                volumeFogShapeExtents = new Vector3(0.5f, 0.42f, 0.5f);
                volumeFogShapeEdgeSoftness = 0.15f;
                volumeFogShapeNoiseErosion = 0.24f;
                volumeCloudCoverage = 0.48f;
                volumeCloudSoftness = 0.26f;
                volumeCloudDetailStrength = 0.64f;
                volumeCloudDetailScale = 8.0f;
                volumeCloudWarpStrength = 0.42f;
                volumeCloudLobeCount = 14;
                volumeCloudLobeSpread = new Vector3(1.0f, 0.64f, 0.92f);
                volumeCloudLobeRadius = 0.36f;
                volumeCloudDensityBoost = 0.95f;
                volumeShadowSamples = 18;
                volumeShadowMaxDistance = 2.1f;
                volumePointLightEnabled = true;
                volumePointLightIntensity = 18.0f;
                volumePointLightRange = 6.0f;
                volumeExposure = 1.2f;
                volumeColorTint = new Color(1.0f, 0.78f, 0.54f, 1.0f);
                break;

            default:
                volumeLightIntensity = 3.0f;
                volumeLightDensity = 0.72f;
                volumeLightSamples = 12;
                volumeLightMaxDistance = 0.8f;
                volumeLightMaxStepLength = 0.1f;
                volumeLightShadowStrength = 0.7f;
                volumeBaseFogDensity = 0.0f;
                volumeHeightFogStrength = 0.006f;
                volumeCutFogBoost = 0.85f;
                volumeNoiseContrast = 1.25f;
                volumeAbsorptionDensity = 0.14f;
                volumeDensityThreshold = 0.045f;
                volumeAlphaClipThreshold = 0.012f;
                volumeEmissionIntensity = 0.0f;
                volumeEmissionColor = Color.black;
                volumeFogShapeMode = VolumeFogShapeMode.NoiseErodedEllipsoid;
                volumeFogShapeExtents = new Vector3(0.48f, 0.42f, 0.48f);
                volumeFogShapeEdgeSoftness = 0.12f;
                volumeFogShapeNoiseErosion = 0.18f;
                volumeCloudCoverage = 0.42f;
                volumeCloudSoftness = 0.28f;
                volumeCloudDetailStrength = 0.32f;
                volumeCloudDetailScale = 7.0f;
                volumeCloudWarpStrength = 0.35f;
                volumeCloudLobeCount = 12;
                volumeCloudLobeSpread = new Vector3(0.95f, 0.62f, 0.9f);
                volumeCloudLobeRadius = 0.34f;
                volumeCloudDensityBoost = 0.85f;
                volumeShadowSamples = 16;
                volumeShadowMaxDistance = 2.0f;
                volumePointLightEnabled = true;
                volumePointLightIntensity = 12.0f;
                volumePointLightRange = 5.8f;
                volumeExposure = 1.15f;
                volumeColorTint = new Color(1.0f, 0.82f, 0.62f, 1.0f);
                break;
        }

        CacheComponents();
        ApplyProperties();
    }

    private void RefreshSceneDataVersion()
    {
        int sceneDataHash = CalculateSceneDataHash();
        if (sceneDataHash == lastSceneDataHash)
        {
            return;
        }

        lastSceneDataHash = sceneDataHash;
        unchecked
        {
            sceneDataVersion++;
            if (sceneDataVersion == 0)
            {
                sceneDataVersion = 1;
            }
        }
    }

    private int CalculateSceneDataHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (int)shapeMode;
            AppendHash(ref hash, sphereCenter);
            hash = hash * 31 + sphereRadius.GetHashCode();
            AppendHash(ref hash, boxExtents);
            AppendHash(ref hash, BaseCutPlaneNormal);
            hash = hash * 31 + cutPlaneOffset.GetHashCode();
            return hash;
        }
    }

    private int CalculateMaterialPropertyHash(
        Bounds meshBounds,
        Vector3 safeVolumeFogShapeExtents,
        Vector3 normalizedPlaneNormal,
        bool volumeEnabled,
        float sdfSurfaceContribution,
        float surfaceContribution,
        float backgroundContribution)
    {
        unchecked
        {
            int hash = 17;
            AppendHash(ref hash, baseColor);
            hash = hash * 31 + ambientStrength.GetHashCode();
            hash = hash * 31 + diffuseStrength.GetHashCode();
            hash = hash * 31 + receiveMainLightShadowStrength.GetHashCode();
            hash = hash * 31 + sdfSoftShadowStrength.GetHashCode();
            hash = hash * 31 + sdfSoftShadowSharpness.GetHashCode();
            hash = hash * 31 + sdfSoftShadowSteps;
            hash = hash * 31 + sdfSoftShadowStart.GetHashCode();
            hash = hash * 31 + sdfSoftShadowDistance.GetHashCode();
            hash = hash * 31 + sdfSoftShadowNormalBias.GetHashCode();
            hash = hash * 31 + sdfSoftShadowMinStepScale.GetHashCode();
            hash = hash * 31 + sdfSoftShadowMaxStepFraction.GetHashCode();
            hash = hash * 31 + sdfSoftShadowDistanceFadeStart.GetHashCode();
            hash = hash * 31 + sdfSoftShadowSceneStrength.GetHashCode();
            hash = hash * 31 + sdfSoftShadowSelfIgnoreDistance.GetHashCode();
            hash = hash * 31 + sdfAmbientOcclusionStrength.GetHashCode();
            hash = hash * 31 + sdfAmbientOcclusionRadius.GetHashCode();
            hash = hash * 31 + sdfAmbientOcclusionSteps;
            hash = hash * 31 + sdfAmbientOcclusionBias.GetHashCode();
            AppendHash(ref hash, cutFaceColor);
            hash = hash * 31 + cutFaceBlend.GetHashCode();
            hash = hash * 31 + cutFaceDominanceSoftness.GetHashCode();
            hash = hash * 31 + cutFaceOcclusionStrength.GetHashCode();
            hash = hash * 31 + cutFaceOcclusionDistance.GetHashCode();
            hash = hash * 31 + cutFaceBandSoftness.GetHashCode();
            hash = hash * 31 + cutFaceEdgeWidth.GetHashCode();
            hash = hash * 31 + cutFaceEdgeBoost.GetHashCode();
            hash = hash * 31 + cutFaceFreshnessBoost.GetHashCode();
            hash = hash * 31 + (volumeEnabled ? 1 : 0);
            hash = hash * 31 + sdfSurfaceContribution.GetHashCode();
            hash = hash * 31 + surfaceContribution.GetHashCode();
            hash = hash * 31 + backgroundContribution.GetHashCode();
            AppendVolumeSettingsHash(ref hash, safeVolumeFogShapeExtents);
            hash = hash * 31 + maxSteps;
            hash = hash * 31 + maxDistance.GetHashCode();
            hash = hash * 31 + hitEpsilon.GetHashCode();
            hash = hash * 31 + normalEpsilon.GetHashCode();
            hash = hash * 31 + (int)shapeMode;
            AppendHash(ref hash, sphereCenter);
            hash = hash * 31 + sphereRadius.GetHashCode();
            AppendHash(ref hash, boxExtents);
            AppendHash(ref hash, normalizedPlaneNormal);
            hash = hash * 31 + cutPlaneOffset.GetHashCode();
            AppendHash(ref hash, meshBounds.min);
            AppendHash(ref hash, meshBounds.max);
            hash = hash * 31 + (int)debugView;
            return hash;
        }
    }

    private int CalculateScreenSpaceGlobalHash(
        Transform volumeTransform,
        bool hasSceneSdf,
        int visibilityMode,
        Vector3 safeVolumeFogShapeExtents,
        bool volumeEnabled)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (volumeEnabled ? 1 : 0);
            AppendHash(ref hash, volumeTransform.worldToLocalMatrix);
            AppendHash(ref hash, volumeTransform.localToWorldMatrix);
            hash = hash * 31 + Mathf.Clamp(visibilityMode, 0, 1);
            hash = hash * 31 + (hasSceneSdf ? 1 : 0);
            AppendVolumeSettingsHash(ref hash, safeVolumeFogShapeExtents);
            hash = hash * 31 + (int)debugView;
            hash = hash * 31 + maxDistance.GetHashCode();
            hash = hash * 31 + hitEpsilon.GetHashCode();
            return hash;
        }
    }

    private void AppendVolumeSettingsHash(ref int hash, Vector3 safeVolumeFogShapeExtents)
    {
        unchecked
        {
            hash = hash * 31 + volumeLightIntensity.GetHashCode();
            hash = hash * 31 + volumeLightDensity.GetHashCode();
            hash = hash * 31 + volumeLightAnisotropy.GetHashCode();
            hash = hash * 31 + volumeLightSamples;
            hash = hash * 31 + volumeSampleJitterStrength.GetHashCode();
            hash = hash * 31 + volumeLightMaxDistance.GetHashCode();
            hash = hash * 31 + volumeLightMaxStepLength.GetHashCode();
            hash = hash * 31 + volumeLightShadowStrength.GetHashCode();
            hash = hash * 31 + volumeLightShadowBias.GetHashCode();
            hash = hash * 31 + volumeLightSurfaceFadeDistance.GetHashCode();
            hash = hash * 31 + volumeSurfaceOcclusionStrength.GetHashCode();
            hash = hash * 31 + volumeSurfaceOcclusionRadius.GetHashCode();
            hash = hash * 31 + volumeLightPlaneBand.GetHashCode();
            hash = hash * 31 + volumeLightRemovedDepth.GetHashCode();
            hash = hash * 31 + volumeLightShapeDepth.GetHashCode();
            hash = hash * 31 + volumeLightNoiseScale.GetHashCode();
            hash = hash * 31 + volumeLightNoiseStrength.GetHashCode();
            hash = hash * 31 + volumeLightNoiseDrift.GetHashCode();
            hash = hash * 31 + volumeBaseFogDensity.GetHashCode();
            hash = hash * 31 + volumeHeightFogStrength.GetHashCode();
            hash = hash * 31 + volumeCutFogBoost.GetHashCode();
            hash = hash * 31 + volumeNoiseContrast.GetHashCode();
            hash = hash * 31 + volumeAbsorptionDensity.GetHashCode();
            hash = hash * 31 + volumeDensityThreshold.GetHashCode();
            hash = hash * 31 + volumeAlphaClipThreshold.GetHashCode();
            hash = hash * 31 + volumeEmissionIntensity.GetHashCode();
            AppendHash(ref hash, volumeEmissionColor);
            hash = hash * 31 + (volumeAmbientMistEnabled ? 1 : 0);
            hash = hash * 31 + volumeAmbientMistDensity.GetHashCode();
            hash = hash * 31 + volumeAmbientMistHeightFalloff.GetHashCode();
            hash = hash * 31 + volumeMovingFogMaxDensity.GetHashCode();
            hash = hash * 31 + volumeMovingFogCompression.GetHashCode();
            hash = hash * 31 + (int)volumeFogShapeMode;
            AppendHash(ref hash, volumeFogShapeCenter);
            AppendHash(ref hash, safeVolumeFogShapeExtents);
            hash = hash * 31 + volumeFogShapeRadius.GetHashCode();
            hash = hash * 31 + volumeFogShapeHeight.GetHashCode();
            hash = hash * 31 + volumeFogShapeEdgeSoftness.GetHashCode();
            hash = hash * 31 + volumeFogShapeNoiseErosion.GetHashCode();
            hash = hash * 31 + volumeFogShapeNoiseScale.GetHashCode();
            hash = hash * 31 + volumeCloudCoverage.GetHashCode();
            hash = hash * 31 + volumeCloudSoftness.GetHashCode();
            hash = hash * 31 + volumeCloudDetailStrength.GetHashCode();
            hash = hash * 31 + volumeCloudDetailScale.GetHashCode();
            hash = hash * 31 + volumeCloudWarpStrength.GetHashCode();
            hash = hash * 31 + volumeCloudLobeCount;
            AppendHash(ref hash, volumeCloudLobeSpread);
            hash = hash * 31 + volumeCloudLobeRadius.GetHashCode();
            hash = hash * 31 + volumeCloudDensityBoost.GetHashCode();
            hash = hash * 31 + volumeShadowSamples;
            hash = hash * 31 + volumeShadowMaxDistance.GetHashCode();
            hash = hash * 31 + volumeGeometryShadowSharpness.GetHashCode();
            hash = hash * 31 + volumeGeometryShadowMinStepScale.GetHashCode();
            hash = hash * 31 + (volumePointLightEnabled ? 1 : 0);
            AppendHash(ref hash, volumePointLightPositionWS);
            AppendHash(ref hash, volumePointLightColor);
            hash = hash * 31 + volumePointLightIntensity.GetHashCode();
            hash = hash * 31 + volumePointLightRange.GetHashCode();
            hash = hash * 31 + volumeExposure.GetHashCode();
            AppendHash(ref hash, volumeColorTint);
        }
    }

    private static void AppendHash(ref int hash, Vector3 value)
    {
        unchecked
        {
            hash = hash * 31 + value.x.GetHashCode();
            hash = hash * 31 + value.y.GetHashCode();
            hash = hash * 31 + value.z.GetHashCode();
        }
    }

    private static void AppendHash(ref int hash, Color value)
    {
        unchecked
        {
            hash = hash * 31 + value.r.GetHashCode();
            hash = hash * 31 + value.g.GetHashCode();
            hash = hash * 31 + value.b.GetHashCode();
            hash = hash * 31 + value.a.GetHashCode();
        }
    }

    private static void AppendHash(ref int hash, Matrix4x4 value)
    {
        unchecked
        {
            for (int i = 0; i < 16; i++)
            {
                hash = hash * 31 + value[i].GetHashCode();
            }
        }
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
        Vector3 safeVolumeFogShapeExtents = new Vector3(
            Mathf.Max(volumeFogShapeExtents.x, 0.01f),
            Mathf.Max(volumeFogShapeExtents.y, 0.01f),
            Mathf.Max(volumeFogShapeExtents.z, 0.01f));

        Vector3 normalizedPlaneNormal = cutPlaneNormal.sqrMagnitude > 1e-6f
            ? cutPlaneNormal.normalized
            : Vector3.up;

        bool volumeModeCanRender = volumeContributionMode == VolumeContributionMode.Full
            || volumeContributionMode == VolumeContributionMode.VolumeOnly;
        bool volumeEnabled = volumeLightEnabled && volumeModeCanRender;
        float sdfSurfaceContribution = volumeContributionMode == VolumeContributionMode.VolumeOnly || volumeContributionMode == VolumeContributionMode.Disabled ? 0.0f : 1.0f;
        float surfaceContribution = volumeEnabled && volumeContributionMode == VolumeContributionMode.Full ? 1.0f : 0.0f;
        float backgroundContribution = volumeEnabled && (volumeContributionMode == VolumeContributionMode.Full || volumeContributionMode == VolumeContributionMode.VolumeOnly) ? 1.0f : 0.0f;
        int materialPropertyHash = CalculateMaterialPropertyHash(
            meshBounds,
            safeVolumeFogShapeExtents,
            normalizedPlaneNormal,
            volumeEnabled,
            sdfSurfaceContribution,
            surfaceContribution,
            backgroundContribution);
        if (materialPropertyHash == lastMaterialPropertyHash)
        {
            return;
        }

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
        propertyBlock.SetFloat(SdfSoftShadowSceneStrengthId, sdfSoftShadowSceneStrength);
        propertyBlock.SetFloat(SdfSoftShadowSelfIgnoreDistanceId, sdfSoftShadowSelfIgnoreDistance);
        propertyBlock.SetFloat(SdfAmbientOcclusionStrengthId, sdfAmbientOcclusionStrength);
        propertyBlock.SetFloat(SdfAmbientOcclusionRadiusId, sdfAmbientOcclusionRadius);
        propertyBlock.SetFloat(SdfAmbientOcclusionStepsId, sdfAmbientOcclusionSteps);
        propertyBlock.SetFloat(SdfAmbientOcclusionBiasId, sdfAmbientOcclusionBias);
        propertyBlock.SetColor(CutFaceColorId, cutFaceColor);
        propertyBlock.SetFloat(CutFaceBlendId, cutFaceBlend);
        propertyBlock.SetFloat(CutFaceDominanceSoftnessId, cutFaceDominanceSoftness);
        propertyBlock.SetFloat(CutFaceOcclusionStrengthId, cutFaceOcclusionStrength);
        propertyBlock.SetFloat(CutFaceOcclusionDistanceId, cutFaceOcclusionDistance);
        propertyBlock.SetFloat(CutFaceBandSoftnessId, cutFaceBandSoftness);
        propertyBlock.SetFloat(CutFaceEdgeWidthId, cutFaceEdgeWidth);
        propertyBlock.SetFloat(CutFaceEdgeBoostId, cutFaceEdgeBoost);
        propertyBlock.SetFloat(CutFaceFreshnessBoostId, cutFaceFreshnessBoost);
        propertyBlock.SetFloat(SdfSurfaceContributionId, sdfSurfaceContribution);
        propertyBlock.SetFloat(UseSceneSdfId, 0.0f);
        propertyBlock.SetFloat(VolumeLightEnabledId, volumeEnabled ? 1.0f : 0.0f);
        propertyBlock.SetFloat(VolumeSurfaceContributionId, surfaceContribution);
        propertyBlock.SetFloat(VolumeBackgroundContributionId, backgroundContribution);
        propertyBlock.SetFloat(VolumeLightIntensityId, volumeLightIntensity);
        propertyBlock.SetFloat(VolumeLightDensityId, volumeLightDensity);
        propertyBlock.SetFloat(VolumeLightAnisotropyId, volumeLightAnisotropy);
        propertyBlock.SetFloat(VolumeLightSamplesId, volumeLightSamples);
        propertyBlock.SetFloat(VolumeSampleJitterStrengthId, volumeSampleJitterStrength);
        propertyBlock.SetFloat(VolumeLightMaxDistanceId, volumeLightMaxDistance);
        propertyBlock.SetFloat(VolumeLightMaxStepLengthId, volumeLightMaxStepLength);
        propertyBlock.SetFloat(VolumeLightShadowStrengthId, volumeLightShadowStrength);
        propertyBlock.SetFloat(VolumeLightShadowBiasId, volumeLightShadowBias);
        propertyBlock.SetFloat(VolumeLightSurfaceFadeDistanceId, volumeLightSurfaceFadeDistance);
        propertyBlock.SetFloat(VolumeSurfaceOcclusionStrengthId, volumeSurfaceOcclusionStrength);
        propertyBlock.SetFloat(VolumeSurfaceOcclusionRadiusId, volumeSurfaceOcclusionRadius);
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
        propertyBlock.SetFloat(VolumeAbsorptionDensityId, volumeAbsorptionDensity);
        propertyBlock.SetFloat(VolumeDensityThresholdId, volumeDensityThreshold);
        propertyBlock.SetFloat(VolumeAlphaClipThresholdId, volumeAlphaClipThreshold);
        propertyBlock.SetFloat(VolumeEmissionIntensityId, volumeEmissionIntensity);
        propertyBlock.SetColor(VolumeEmissionColorId, volumeEmissionColor);
        propertyBlock.SetFloat(VolumeAmbientMistEnabledId, volumeAmbientMistEnabled ? 1.0f : 0.0f);
        propertyBlock.SetFloat(VolumeAmbientMistDensityId, volumeAmbientMistDensity);
        propertyBlock.SetFloat(VolumeAmbientMistHeightFalloffId, volumeAmbientMistHeightFalloff);
        propertyBlock.SetFloat(VolumeMovingFogMaxDensityId, volumeMovingFogMaxDensity);
        propertyBlock.SetFloat(VolumeMovingFogCompressionId, volumeMovingFogCompression);
        propertyBlock.SetFloat(VolumeFogShapeModeId, (float)volumeFogShapeMode);
        propertyBlock.SetVector(VolumeFogShapeCenterId, volumeFogShapeCenter);
        propertyBlock.SetVector(VolumeFogShapeExtentsId, safeVolumeFogShapeExtents);
        propertyBlock.SetFloat(VolumeFogShapeRadiusId, volumeFogShapeRadius);
        propertyBlock.SetFloat(VolumeFogShapeHeightId, volumeFogShapeHeight);
        propertyBlock.SetFloat(VolumeFogShapeEdgeSoftnessId, volumeFogShapeEdgeSoftness);
        propertyBlock.SetFloat(VolumeFogShapeNoiseErosionId, volumeFogShapeNoiseErosion);
        propertyBlock.SetFloat(VolumeFogShapeNoiseScaleId, volumeFogShapeNoiseScale);
        propertyBlock.SetFloat(VolumeCloudCoverageId, volumeCloudCoverage);
        propertyBlock.SetFloat(VolumeCloudSoftnessId, volumeCloudSoftness);
        propertyBlock.SetFloat(VolumeCloudDetailStrengthId, volumeCloudDetailStrength);
        propertyBlock.SetFloat(VolumeCloudDetailScaleId, volumeCloudDetailScale);
        propertyBlock.SetFloat(VolumeCloudWarpStrengthId, volumeCloudWarpStrength);
        propertyBlock.SetFloat(VolumeCloudLobeCountId, volumeCloudLobeCount);
        propertyBlock.SetVector(VolumeCloudLobeSpreadId, volumeCloudLobeSpread);
        propertyBlock.SetFloat(VolumeCloudLobeRadiusId, volumeCloudLobeRadius);
        propertyBlock.SetFloat(VolumeCloudDensityBoostId, volumeCloudDensityBoost);
        propertyBlock.SetFloat(VolumeShadowSamplesId, volumeShadowSamples);
        propertyBlock.SetFloat(VolumeShadowMaxDistanceId, volumeShadowMaxDistance);
        propertyBlock.SetFloat(VolumeGeometryShadowSharpnessId, volumeGeometryShadowSharpness);
        propertyBlock.SetFloat(VolumeGeometryShadowMinStepScaleId, volumeGeometryShadowMinStepScale);
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
        propertyBlock.SetInt(SdfSceneShapeCountId, 0);

        cachedRenderer.SetPropertyBlock(propertyBlock);
        lastMaterialPropertyHash = materialPropertyHash;
        unchecked
        {
            materialPropertyUploadVersion++;
        }
    }
}
