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
        VolumeLight = 10
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
    [SerializeField] [Range(0.0f, 1.0f)] private float volumeLightShadowStrength = 0.75f;
    [SerializeField] [Min(0.0001f)] private float volumeLightShadowBias = 0.01f;
    [SerializeField] [Min(0.01f)] private float volumeLightSurfaceFadeDistance = 0.16f;
    [SerializeField] [Min(0.01f)] private float volumeLightPlaneBand = 0.16f;
    [SerializeField] [Min(0.01f)] private float volumeLightRemovedDepth = 0.28f;
    [SerializeField] [Min(0.01f)] private float volumeLightShapeDepth = 0.24f;
    [SerializeField] [Min(0.1f)] private float volumeLightNoiseScale = 4.0f;
    [SerializeField] [Range(0.0f, 1.0f)] private float volumeLightNoiseStrength = 0.18f;
    [SerializeField] [Min(0.0f)] private float volumeLightNoiseDrift = 0.2f;

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
    private static readonly int VolumeLightShadowStrengthId = Shader.PropertyToID("_VolumeLightShadowStrength");
    private static readonly int VolumeLightShadowBiasId = Shader.PropertyToID("_VolumeLightShadowBias");
    private static readonly int VolumeLightSurfaceFadeDistanceId = Shader.PropertyToID("_VolumeLightSurfaceFadeDistance");
    private static readonly int VolumeLightPlaneBandId = Shader.PropertyToID("_VolumeLightPlaneBand");
    private static readonly int VolumeLightRemovedDepthId = Shader.PropertyToID("_VolumeLightRemovedDepth");
    private static readonly int VolumeLightShapeDepthId = Shader.PropertyToID("_VolumeLightShapeDepth");
    private static readonly int VolumeLightNoiseScaleId = Shader.PropertyToID("_VolumeLightNoiseScale");
    private static readonly int VolumeLightNoiseStrengthId = Shader.PropertyToID("_VolumeLightNoiseStrength");
    private static readonly int VolumeLightNoiseDriftId = Shader.PropertyToID("_VolumeLightNoiseDrift");
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
        propertyBlock.SetFloat(VolumeLightShadowStrengthId, volumeLightShadowStrength);
        propertyBlock.SetFloat(VolumeLightShadowBiasId, volumeLightShadowBias);
        propertyBlock.SetFloat(VolumeLightSurfaceFadeDistanceId, volumeLightSurfaceFadeDistance);
        propertyBlock.SetFloat(VolumeLightPlaneBandId, volumeLightPlaneBand);
        propertyBlock.SetFloat(VolumeLightRemovedDepthId, volumeLightRemovedDepth);
        propertyBlock.SetFloat(VolumeLightShapeDepthId, volumeLightShapeDepth);
        propertyBlock.SetFloat(VolumeLightNoiseScaleId, volumeLightNoiseScale);
        propertyBlock.SetFloat(VolumeLightNoiseStrengthId, volumeLightNoiseStrength);
        propertyBlock.SetFloat(VolumeLightNoiseDriftId, volumeLightNoiseDrift);
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
