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
    private static readonly ProfilerMarker CutTileCullingMarker = new ProfilerMarker("SDF.CutTileCulling.Dispatch");
    private const int CutTileSize = 16;

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

    [Header("Cut Tile Culling")]
    [SerializeField] private bool enableCutTileCulling = true;
    [SerializeField] private ComputeShader cutTileCullingCompute;
    [SerializeField] [Range(8, 256)] private int maxCutIndicesPerTile = 64;
    [SerializeField] [Min(0)] private int cutTilePaddingPixels = 2;

    [Header("Runtime Stats")]
    [SerializeField] private int currentSdfShapeCount;
    [SerializeField] private int currentCutPlaneCount;
    [SerializeField] private int currentVolumeSamples;
    [SerializeField] private int currentShadowSamples;
    [SerializeField] private bool currentScreenSpaceVolumeEnabled;
    [SerializeField] private bool currentCutTileCullingEnabled;
    [SerializeField] private int currentCutTileGridWidth;
    [SerializeField] private int currentCutTileGridHeight;
    [SerializeField] private int currentCutTileIndexCapacity;
    [SerializeField] private int cutTileDispatchVersion;
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
    private int cutTileKernel = -1;
    private int allocatedCutTileCount;
    private int allocatedCutTileIndexCount;
    private int lastCutTileDispatchHash = int.MinValue;
    private ComputeBuffer cutTileRangeBuffer;
    private ComputeBuffer cutTileIndexBuffer;
    private ComputeBuffer dummyCutTileRangeBuffer;
    private ComputeBuffer dummyCutTileIndexBuffer;

    private static readonly int SdfCutInfluencesId = Shader.PropertyToID("_SdfCutInfluences");
    private static readonly int SdfCutInfluenceCountId = Shader.PropertyToID("_SdfCutInfluenceCount");
    private static readonly int SdfCutTileRangesId = Shader.PropertyToID("_SdfCutTileRanges");
    private static readonly int SdfCutTileIndicesId = Shader.PropertyToID("_SdfCutTileIndices");
    private static readonly int SdfCutTileEnabledId = Shader.PropertyToID("_SdfCutTileEnabled");
    private static readonly int SdfCutTileGridWidthId = Shader.PropertyToID("_SdfCutTileGridWidth");
    private static readonly int SdfCutTileGridHeightId = Shader.PropertyToID("_SdfCutTileGridHeight");
    private static readonly int SdfCutTileMaxIndicesPerTileId = Shader.PropertyToID("_SdfCutTileMaxIndicesPerTile");
    private static readonly int SdfCutTilePaddingPixelsId = Shader.PropertyToID("_SdfCutTilePaddingPixels");
    private static readonly int SdfCutTileScreenSizeId = Shader.PropertyToID("_SdfCutTileScreenSize");
    private static readonly int SdfCutTileWorldToClipId = Shader.PropertyToID("_SdfCutTileWorldToClip");

    public SdfPhase1Driver VolumeDriver => volumeDriver;
    public bool UseScreenSpaceVolume => useScreenSpaceVolume;
    public int CurrentSdfShapeCount => currentSdfShapeCount;
    public int CurrentCutPlaneCount => currentCutPlaneCount;
    public int CurrentVolumeSamples => currentVolumeSamples;
    public int CurrentShadowSamples => currentShadowSamples;
    public bool CurrentScreenSpaceVolumeEnabled => currentScreenSpaceVolumeEnabled;
    public bool CurrentCutTileCullingEnabled => currentCutTileCullingEnabled;
    public int SceneDataVersion => sceneDataVersion;
    public bool TryGetSceneBuffers(
        out ComputeBuffer shapeBuffer,
        out ComputeBuffer cutPlaneBuffer,
        out ComputeBuffer cutInfluenceBuffer,
        out int shapeCount,
        out int cutPlaneCount,
        out int version)
    {
        return sceneDataBuffer.TryGetBuffers(out shapeBuffer, out cutPlaneBuffer, out cutInfluenceBuffer, out shapeCount, out cutPlaneCount, out version);
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
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
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
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        DisableCutTileCullingGlobals();
        SdfPhase1Driver.DisableScreenSpaceVolumeGlobals();
        ReleaseCutTileBuffers();
        ReleaseSceneData();
    }

    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
        DisableCutTileCullingGlobals();
        SdfPhase1Driver.DisableScreenSpaceVolumeGlobals();
        ReleaseCutTileBuffers();
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

    private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!isActiveAndEnabled || camera == null)
        {
            return;
        }

        DispatchCutTileCulling(camera);
    }

    private void DispatchCutTileCulling(Camera camera)
    {
        using (CutTileCullingMarker.Auto())
        {
            if (!enableCutTileCulling || !ResolveCutTileCompute())
            {
                DisableCutTileCullingGlobals();
                return;
            }

            if (!sceneDataBuffer.TryGetBuffers(
                    out _,
                    out _,
                    out ComputeBuffer cutInfluenceBuffer,
                    out _,
                    out int cutPlaneCount,
                    out int sceneVersion) ||
                cutPlaneCount <= 0)
            {
                DisableCutTileCullingGlobals();
                return;
            }

            int screenWidth = Mathf.Max(camera.pixelWidth, 1);
            int screenHeight = Mathf.Max(camera.pixelHeight, 1);
            int gridWidth = Mathf.CeilToInt(screenWidth / (float)CutTileSize);
            int gridHeight = Mathf.CeilToInt(screenHeight / (float)CutTileSize);
            int tileCount = Mathf.Max(gridWidth * gridHeight, 1);
            int safeMaxIndicesPerTile = Mathf.Max(maxCutIndicesPerTile, 1);
            int indexCapacity = tileCount * safeMaxIndicesPerTile;
            Matrix4x4 worldToClip = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix;
            int dispatchHash = CalculateCutTileDispatchHash(
                camera,
                screenWidth,
                screenHeight,
                gridWidth,
                gridHeight,
                safeMaxIndicesPerTile,
                sceneVersion,
                worldToClip);

            EnsureCutTileBuffers(tileCount, indexCapacity);
            BindCutTileGlobals(true, gridWidth, gridHeight, safeMaxIndicesPerTile, screenWidth, screenHeight);
            if (dispatchHash == lastCutTileDispatchHash)
            {
                currentCutTileCullingEnabled = true;
                return;
            }

            cutTileCullingCompute.SetInt(SdfCutInfluenceCountId, cutPlaneCount);
            cutTileCullingCompute.SetInt(SdfCutTileGridWidthId, gridWidth);
            cutTileCullingCompute.SetInt(SdfCutTileGridHeightId, gridHeight);
            cutTileCullingCompute.SetInt(SdfCutTileMaxIndicesPerTileId, safeMaxIndicesPerTile);
            cutTileCullingCompute.SetInt(SdfCutTilePaddingPixelsId, Mathf.Max(cutTilePaddingPixels, 0));
            cutTileCullingCompute.SetVector(SdfCutTileScreenSizeId, new Vector4(screenWidth, screenHeight, 1.0f / screenWidth, 1.0f / screenHeight));
            cutTileCullingCompute.SetMatrix(SdfCutTileWorldToClipId, worldToClip);
            cutTileCullingCompute.SetBuffer(cutTileKernel, SdfCutInfluencesId, cutInfluenceBuffer);
            cutTileCullingCompute.SetBuffer(cutTileKernel, SdfCutTileRangesId, cutTileRangeBuffer);
            cutTileCullingCompute.SetBuffer(cutTileKernel, SdfCutTileIndicesId, cutTileIndexBuffer);

            int groupX = Mathf.CeilToInt(gridWidth / 8.0f);
            int groupY = Mathf.CeilToInt(gridHeight / 8.0f);
            cutTileCullingCompute.Dispatch(cutTileKernel, groupX, groupY, 1);

            lastCutTileDispatchHash = dispatchHash;
            currentCutTileCullingEnabled = true;
            currentCutTileGridWidth = gridWidth;
            currentCutTileGridHeight = gridHeight;
            currentCutTileIndexCapacity = indexCapacity;
            unchecked
            {
                cutTileDispatchVersion++;
            }
        }
    }

    private bool ResolveCutTileCompute()
    {
        if (cutTileCullingCompute == null)
        {
            cutTileCullingCompute = Resources.Load<ComputeShader>("Sdf/SdfCutTileCulling");
        }

        if (cutTileCullingCompute == null)
        {
            return false;
        }

        if (cutTileKernel < 0)
        {
            cutTileKernel = cutTileCullingCompute.FindKernel("BuildCutTileLists");
        }

        return cutTileKernel >= 0;
    }

    private void EnsureCutTileBuffers(int tileCount, int indexCapacity)
    {
        if (cutTileRangeBuffer == null || allocatedCutTileCount != tileCount)
        {
            ReleaseBuffer(ref cutTileRangeBuffer);
            cutTileRangeBuffer = new ComputeBuffer(tileCount, sizeof(uint) * 2, ComputeBufferType.Structured);
            allocatedCutTileCount = tileCount;
            lastCutTileDispatchHash = int.MinValue;
        }

        if (cutTileIndexBuffer == null || allocatedCutTileIndexCount != indexCapacity)
        {
            ReleaseBuffer(ref cutTileIndexBuffer);
            cutTileIndexBuffer = new ComputeBuffer(indexCapacity, sizeof(int), ComputeBufferType.Structured);
            allocatedCutTileIndexCount = indexCapacity;
            lastCutTileDispatchHash = int.MinValue;
        }
    }

    private void EnsureDummyCutTileBuffers()
    {
        if (dummyCutTileRangeBuffer == null)
        {
            dummyCutTileRangeBuffer = new ComputeBuffer(1, sizeof(uint) * 2, ComputeBufferType.Structured);
        }

        if (dummyCutTileIndexBuffer == null)
        {
            dummyCutTileIndexBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
        }
    }

    private void BindCutTileGlobals(bool enabled, int gridWidth, int gridHeight, int maxIndicesPerTile, int screenWidth, int screenHeight)
    {
        if (enabled && cutTileRangeBuffer != null && cutTileIndexBuffer != null)
        {
            Shader.SetGlobalBuffer(SdfCutTileRangesId, cutTileRangeBuffer);
            Shader.SetGlobalBuffer(SdfCutTileIndicesId, cutTileIndexBuffer);
        }
        else
        {
            EnsureDummyCutTileBuffers();
            Shader.SetGlobalBuffer(SdfCutTileRangesId, dummyCutTileRangeBuffer);
            Shader.SetGlobalBuffer(SdfCutTileIndicesId, dummyCutTileIndexBuffer);
        }

        Shader.SetGlobalInt(SdfCutTileEnabledId, enabled ? 1 : 0);
        Shader.SetGlobalInt(SdfCutTileGridWidthId, Mathf.Max(gridWidth, 1));
        Shader.SetGlobalInt(SdfCutTileGridHeightId, Mathf.Max(gridHeight, 1));
        Shader.SetGlobalInt(SdfCutTileMaxIndicesPerTileId, Mathf.Max(maxIndicesPerTile, 1));
        Shader.SetGlobalVector(SdfCutTileScreenSizeId, new Vector4(Mathf.Max(screenWidth, 1), Mathf.Max(screenHeight, 1), 0.0f, 0.0f));
    }

    private void DisableCutTileCullingGlobals()
    {
        BindCutTileGlobals(false, 1, 1, 1, 1, 1);
        currentCutTileCullingEnabled = false;
        currentCutTileGridWidth = 0;
        currentCutTileGridHeight = 0;
        currentCutTileIndexCapacity = 0;
        lastCutTileDispatchHash = int.MinValue;
    }

    private void ReleaseCutTileBuffers()
    {
        ReleaseBuffer(ref cutTileRangeBuffer);
        ReleaseBuffer(ref cutTileIndexBuffer);
        ReleaseBuffer(ref dummyCutTileRangeBuffer);
        ReleaseBuffer(ref dummyCutTileIndexBuffer);
        allocatedCutTileCount = 0;
        allocatedCutTileIndexCount = 0;
        lastCutTileDispatchHash = int.MinValue;
    }

    private static void ReleaseBuffer(ref ComputeBuffer buffer)
    {
        if (buffer == null)
        {
            return;
        }

        buffer.Release();
        buffer = null;
    }

    private int CalculateCutTileDispatchHash(
        Camera camera,
        int screenWidth,
        int screenHeight,
        int gridWidth,
        int gridHeight,
        int safeMaxIndicesPerTile,
        int sceneVersion,
        Matrix4x4 worldToClip)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + camera.GetInstanceID();
            hash = hash * 31 + screenWidth;
            hash = hash * 31 + screenHeight;
            hash = hash * 31 + gridWidth;
            hash = hash * 31 + gridHeight;
            hash = hash * 31 + safeMaxIndicesPerTile;
            hash = hash * 31 + Mathf.Max(cutTilePaddingPixels, 0);
            hash = hash * 31 + sceneVersion;
            AppendHash(ref hash, worldToClip);
            return hash;
        }
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
