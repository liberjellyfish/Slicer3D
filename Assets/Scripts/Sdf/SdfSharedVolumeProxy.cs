using System;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;

[ExecuteAlways]
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SdfPhase1Driver))]
[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(MeshFilter))]
public class SdfSharedVolumeProxy : MonoBehaviour
{
    private static readonly ProfilerMarker LateUpdateMarker = new ProfilerMarker("SDF.SharedVolumeProxy.LateUpdate");
    private static readonly ProfilerMarker UploadSceneDataMarker = new ProfilerMarker("SDF.SharedVolumeProxy.UploadSceneData");

    public enum ScreenSpaceVisibilityMode
    {
        PhysicalExtinction = 0,
        ScatteringOnly = 1
    }

    [Header("Scene SDF Source")]
    [SerializeField] private bool autoFindSurfaceDrivers = true;
    [SerializeField] [Min(1)] private int autoFindRefreshIntervalFrames = 30;
    [SerializeField] private SdfPhase1Driver[] surfaceDrivers = Array.Empty<SdfPhase1Driver>();
    [SerializeField] private bool forceSurfaceDriversToSurfaceOnly = true;

    [Header("Bounds")]
    [SerializeField] private bool autoFitBounds = true;
    [SerializeField] private Vector3 manualCenter = Vector3.zero;
    [SerializeField] private Vector3 manualSize = new Vector3(4.8f, 4.8f, 4.8f);
    [SerializeField] [Min(0.0f)] private float boundsPadding = 0.35f;
    [SerializeField] [Min(0.01f)] private float minBoundsSize = 0.5f;

    [Header("Rendering")]
    [SerializeField] private bool useScreenSpaceVolume = true;
    [SerializeField] private bool hideProxyRendererInScreenSpace = true;
    [SerializeField] private ScreenSpaceVisibilityMode screenSpaceVisibilityMode = ScreenSpaceVisibilityMode.ScatteringOnly;
    [SerializeField] private int sortingOrder = 100;
    [SerializeField] private bool drawBoundsGizmo = true;
    [SerializeField] private Color boundsGizmoColor = new Color(1.0f, 0.62f, 0.25f, 0.35f);

    [Header("Runtime Stats")]
    [SerializeField] private int currentSdfShapeCount;
    [SerializeField] private int currentCutPlaneCount;
    [SerializeField] private int currentVolumeSamples;
    [SerializeField] private int currentShadowSamples;
    [SerializeField] private bool currentScreenSpaceVolumeEnabled;
    [SerializeField] private int sceneDataVersion;
    [SerializeField] private bool lastSyncRebuiltSceneData;
    [SerializeField] private bool lastSyncUploadedBuffers;
    [SerializeField] private bool lastSyncBoundGlobals;
    [SerializeField] private bool lastSyncUploadedVolumeGlobals;

    private readonly SdfSceneDataBuffer sceneDataBuffer = new SdfSceneDataBuffer();
    private SdfPhase1Driver volumeDriver;
    private Renderer cachedRenderer;
    private MaterialPropertyBlock propertyBlock;
    private int nextAutoFindFrame;
    private int lastRendererConfigHash = int.MinValue;
    private int lastOwnershipHash = int.MinValue;
    private int lastBoundsHash = int.MinValue;

    public SdfPhase1Driver VolumeDriver => volumeDriver;
    public bool UseScreenSpaceVolume => useScreenSpaceVolume;
    public int CurrentSdfShapeCount => currentSdfShapeCount;
    public int CurrentCutPlaneCount => currentCutPlaneCount;
    public int CurrentVolumeSamples => currentVolumeSamples;
    public int CurrentShadowSamples => currentShadowSamples;
    public bool CurrentScreenSpaceVolumeEnabled => currentScreenSpaceVolumeEnabled;
    public int SceneDataVersion => sceneDataVersion;
    public bool TryGetSceneBuffers(
        out ComputeBuffer shapeBuffer,
        out ComputeBuffer cutPlaneBuffer,
        out int shapeCount,
        out int cutPlaneCount,
        out int version)
    {
        return sceneDataBuffer.TryGetBuffers(out shapeBuffer, out cutPlaneBuffer, out shapeCount, out cutPlaneCount, out version);
    }
    public bool AutoFitBounds
    {
        get => autoFitBounds;
        set => autoFitBounds = value;
    }

    public Vector3 ManualCenter
    {
        get => manualCenter;
        set => manualCenter = value;
    }

    public Vector3 ManualSize
    {
        get => manualSize;
        set => manualSize = value;
    }

    private void OnEnable()
    {
        CacheComponents();
        ConfigureVolumeDriver();
        RefreshSurfaceDriversIfNeeded(true);
        ApplySurfaceDriverOwnership(true);
        ApplyBounds(true);
        UploadSceneSdfData();
    }

    private void OnValidate()
    {
        manualSize = MaxComponents(manualSize, Vector3.one * minBoundsSize);
        CacheComponents();
        lastRendererConfigHash = int.MinValue;
        lastOwnershipHash = int.MinValue;
        lastBoundsHash = int.MinValue;
        ConfigureVolumeDriver();
        RefreshSurfaceDriversIfNeeded(true);
        ApplySurfaceDriverOwnership(true);
        ApplyBounds(true);
        UploadSceneSdfData();
    }

    private void LateUpdate()
    {
        using (LateUpdateMarker.Auto())
        {
            CacheComponents();
            ConfigureVolumeDriver();
            bool driversChanged = RefreshSurfaceDriversIfNeeded(false);
            ApplySurfaceDriverOwnership(driversChanged);
            ApplyBounds(false);
            UploadSceneSdfData();
        }
    }

    private void OnDisable()
    {
        SdfPhase1Driver.DisableScreenSpaceVolumeGlobals();
        ReleaseSceneData();
    }

    private void OnDestroy()
    {
        SdfPhase1Driver.DisableScreenSpaceVolumeGlobals();
        ReleaseSceneData();
    }

    public void SetSurfaceDrivers(SdfPhase1Driver[] drivers)
    {
        CacheComponents();
        surfaceDrivers = drivers ?? Array.Empty<SdfPhase1Driver>();
        lastOwnershipHash = int.MinValue;
        lastBoundsHash = int.MinValue;
        ApplySurfaceDriverOwnership(true);
        UploadSceneSdfData();
    }

    public void ApplyVolumePreset(SdfPhase1Driver.VolumePreset preset)
    {
        CacheComponents();
        if (volumeDriver == null)
        {
            return;
        }

        volumeDriver.ApplyVolumePreset(preset);
        volumeDriver.SetVolumeContributionMode(SdfPhase1Driver.VolumeContributionMode.VolumeOnly);
    }

    private void CacheComponents()
    {
        if (volumeDriver == null)
        {
            volumeDriver = GetComponent<SdfPhase1Driver>();
        }

        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private bool ConfigureVolumeDriver()
    {
        bool changed = false;
        if (volumeDriver != null)
        {
            volumeDriver.SetVolumeContributionMode(SdfPhase1Driver.VolumeContributionMode.VolumeOnly);
        }

        if (cachedRenderer != null)
        {
            int rendererConfigHash = CalculateRendererConfigHash();
            if (rendererConfigHash != lastRendererConfigHash)
            {
                cachedRenderer.forceRenderingOff = useScreenSpaceVolume && hideProxyRendererInScreenSpace;
                cachedRenderer.sortingOrder = sortingOrder;
                cachedRenderer.shadowCastingMode = ShadowCastingMode.Off;
                cachedRenderer.receiveShadows = false;
                lastRendererConfigHash = rendererConfigHash;
                changed = true;
            }
        }

        return changed;
    }

    private bool ApplySurfaceDriverOwnership(bool force)
    {
        if (!forceSurfaceDriversToSurfaceOnly || surfaceDrivers == null)
        {
            return false;
        }

        int ownershipHash = CalculateSurfaceDriversHash();
        if (!force && ownershipHash == lastOwnershipHash)
        {
            return false;
        }

        for (int i = 0; i < surfaceDrivers.Length; i++)
        {
            SdfPhase1Driver driver = surfaceDrivers[i];
            if (!SdfSceneDriverUtility.IsRenderableSurfaceDriver(driver, volumeDriver))
            {
                continue;
            }

            driver.SetVolumeContributionMode(SdfPhase1Driver.VolumeContributionMode.SurfaceOnly);
        }

        lastOwnershipHash = ownershipHash;
        return true;
    }

    private bool RefreshSurfaceDriversIfNeeded(bool force)
    {
        if (!autoFindSurfaceDrivers)
        {
            return false;
        }

        if (!force && !Application.isPlaying)
        {
            return false;
        }

        if (!force && Application.isPlaying && Time.frameCount < nextAutoFindFrame)
        {
            return false;
        }

        SdfPhase1Driver[] foundDrivers = SdfSceneDriverUtility.FindSurfaceDrivers(volumeDriver);
        nextAutoFindFrame = Application.isPlaying
            ? Time.frameCount + Mathf.Max(autoFindRefreshIntervalFrames, 1)
            : 0;
        if (SurfaceDriverArraysMatch(surfaceDrivers, foundDrivers))
        {
            return false;
        }

        surfaceDrivers = foundDrivers;
        lastOwnershipHash = int.MinValue;
        lastBoundsHash = int.MinValue;
        return true;
    }

    private bool ApplyBounds(bool force)
    {
        Bounds volumeBounds;
        if (autoFitBounds && SdfSceneDriverUtility.TryGetCombinedBounds(surfaceDrivers, out Bounds surfaceBounds))
        {
            volumeBounds = surfaceBounds;
            volumeBounds.Expand(boundsPadding * 2.0f);
        }
        else
        {
            volumeBounds = new Bounds(manualCenter, MaxComponents(manualSize, Vector3.one * minBoundsSize));
        }

        Vector3 safeSize = MaxComponents(volumeBounds.size, Vector3.one * minBoundsSize);
        int boundsHash = CalculateBoundsHash(volumeBounds.center, safeSize);
        if (!force && boundsHash == lastBoundsHash)
        {
            return false;
        }

        transform.position = volumeBounds.center;
        transform.rotation = Quaternion.identity;
        transform.localScale = safeSize;
        lastBoundsHash = boundsHash;
        return true;
    }

    private void UploadSceneSdfData()
    {
        using (UploadSceneDataMarker.Auto())
        {
            CacheComponents();
            bool sceneDataChanged;
            if (useScreenSpaceVolume)
            {
                // Screen-space volume rendering consumes the scene SDF as globals; the hidden proxy renderer does not own this pass.
                sceneDataChanged = sceneDataBuffer.UploadGlobals(transform, surfaceDrivers);
            }
            else
            {
                sceneDataChanged = sceneDataBuffer.Upload(cachedRenderer, transform, surfaceDrivers, propertyBlock);
            }

            bool volumeGlobalsChanged = false;
            if (useScreenSpaceVolume && volumeDriver != null)
            {
                volumeGlobalsChanged = volumeDriver.UploadScreenSpaceVolumeGlobals(
                    transform,
                    sceneDataBuffer.ShapeCount > 0,
                    (int)screenSpaceVisibilityMode);
            }
            else
            {
                SdfPhase1Driver.DisableScreenSpaceVolumeGlobals();
            }

            UpdateRuntimeStats(sceneDataChanged, volumeGlobalsChanged);
        }
    }

    private void ReleaseSceneData()
    {
        sceneDataBuffer.Dispose();
    }

    private static Vector3 MaxComponents(Vector3 value, Vector3 minValue)
    {
        return new Vector3(
            Mathf.Max(value.x, minValue.x),
            Mathf.Max(value.y, minValue.y),
            Mathf.Max(value.z, minValue.z));
    }

    private void UpdateRuntimeStats(bool sceneDataChanged, bool volumeGlobalsChanged)
    {
        currentSdfShapeCount = sceneDataBuffer.ShapeCount;
        currentCutPlaneCount = sceneDataBuffer.CutPlaneCount;
        currentVolumeSamples = volumeDriver != null ? volumeDriver.VolumeLightSampleCount : 0;
        currentShadowSamples = volumeDriver != null ? volumeDriver.VolumeShadowSampleCount : 0;
        currentScreenSpaceVolumeEnabled = useScreenSpaceVolume && volumeDriver != null;
        sceneDataVersion = sceneDataBuffer.DataVersion;
        lastSyncRebuiltSceneData = sceneDataChanged && sceneDataBuffer.LastSyncRebuiltData;
        lastSyncUploadedBuffers = sceneDataBuffer.LastSyncUploadedBuffers;
        lastSyncBoundGlobals = sceneDataBuffer.LastSyncBoundShadowGlobals;
        lastSyncUploadedVolumeGlobals = volumeGlobalsChanged;
    }

    private int CalculateRendererConfigHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (useScreenSpaceVolume ? 1 : 0);
            hash = hash * 31 + (hideProxyRendererInScreenSpace ? 1 : 0);
            hash = hash * 31 + sortingOrder;
            hash = hash * 31 + (volumeDriver != null ? volumeDriver.GetInstanceID() : 0);
            return hash;
        }
    }

    private int CalculateSurfaceDriversHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (forceSurfaceDriversToSurfaceOnly ? 1 : 0);
            hash = hash * 31 + (volumeDriver != null ? volumeDriver.GetInstanceID() : 0);
            hash = hash * 31 + (surfaceDrivers != null ? surfaceDrivers.Length : 0);
            if (surfaceDrivers == null)
            {
                return hash;
            }

            for (int i = 0; i < surfaceDrivers.Length; i++)
            {
                SdfPhase1Driver driver = surfaceDrivers[i];
                hash = hash * 31 + (driver != null ? driver.GetInstanceID() : 0);
            }

            return hash;
        }
    }

    private static int CalculateBoundsHash(Vector3 center, Vector3 size)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + center.x.GetHashCode();
            hash = hash * 31 + center.y.GetHashCode();
            hash = hash * 31 + center.z.GetHashCode();
            hash = hash * 31 + size.x.GetHashCode();
            hash = hash * 31 + size.y.GetHashCode();
            hash = hash * 31 + size.z.GetHashCode();
            return hash;
        }
    }

    private static bool SurfaceDriverArraysMatch(SdfPhase1Driver[] a, SdfPhase1Driver[] b)
    {
        int aLength = a != null ? a.Length : 0;
        int bLength = b != null ? b.Length : 0;
        if (aLength != bLength)
        {
            return false;
        }

        for (int i = 0; i < aLength; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawBoundsGizmo)
        {
            return;
        }

        Gizmos.color = boundsGizmoColor;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
