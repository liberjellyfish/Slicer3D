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

    [Header("Driver")]
    [SerializeField] private bool syncEveryFrame = true;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int AmbientStrengthId = Shader.PropertyToID("_AmbientStrength");
    private static readonly int DiffuseStrengthId = Shader.PropertyToID("_DiffuseStrength");
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

        propertyBlock.Clear();
        propertyBlock.SetColor(BaseColorId, baseColor);
        propertyBlock.SetFloat(AmbientStrengthId, ambientStrength);
        propertyBlock.SetFloat(DiffuseStrengthId, diffuseStrength);
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
