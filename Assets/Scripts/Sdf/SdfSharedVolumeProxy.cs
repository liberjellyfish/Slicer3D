using System;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SdfPhase1Driver))]
[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(MeshFilter))]
public class SdfSharedVolumeProxy : MonoBehaviour
{
    [Header("Scene SDF Source")]
    [SerializeField] private bool autoFindSurfaceDrivers = true;
    [SerializeField] private SdfPhase1Driver[] surfaceDrivers = Array.Empty<SdfPhase1Driver>();
    [SerializeField] private bool forceSurfaceDriversToSurfaceOnly = true;

    [Header("Bounds")]
    [SerializeField] private bool autoFitBounds = true;
    [SerializeField] private Vector3 manualCenter = Vector3.zero;
    [SerializeField] private Vector3 manualSize = new Vector3(4.8f, 4.8f, 4.8f);
    [SerializeField] [Min(0.0f)] private float boundsPadding = 0.35f;
    [SerializeField] [Min(0.01f)] private float minBoundsSize = 0.5f;

    [Header("Rendering")]
    [SerializeField] private int sortingOrder = 100;
    [SerializeField] private bool drawBoundsGizmo = true;
    [SerializeField] private Color boundsGizmoColor = new Color(1.0f, 0.62f, 0.25f, 0.35f);

    private readonly SdfSceneDataBuffer sceneDataBuffer = new SdfSceneDataBuffer();
    private SdfPhase1Driver volumeDriver;
    private Renderer cachedRenderer;
    private MaterialPropertyBlock propertyBlock;

    public SdfPhase1Driver VolumeDriver => volumeDriver;
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
        RefreshSurfaceDriversIfNeeded();
        ApplySurfaceDriverOwnership();
        ApplyBounds();
        UploadSceneSdfData();
    }

    private void OnValidate()
    {
        manualSize = MaxComponents(manualSize, Vector3.one * minBoundsSize);
        CacheComponents();
        ConfigureVolumeDriver();
        ApplySurfaceDriverOwnership();
        ApplyBounds();
        UploadSceneSdfData();
    }

    private void LateUpdate()
    {
        CacheComponents();
        ConfigureVolumeDriver();
        RefreshSurfaceDriversIfNeeded();
        ApplySurfaceDriverOwnership();
        ApplyBounds();
        UploadSceneSdfData();
    }

    private void OnDisable()
    {
        ReleaseSceneData();
    }

    private void OnDestroy()
    {
        ReleaseSceneData();
    }

    public void SetSurfaceDrivers(SdfPhase1Driver[] drivers)
    {
        CacheComponents();
        surfaceDrivers = drivers ?? Array.Empty<SdfPhase1Driver>();
        ApplySurfaceDriverOwnership();
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

    private void ConfigureVolumeDriver()
    {
        if (volumeDriver != null)
        {
            volumeDriver.SetVolumeContributionMode(SdfPhase1Driver.VolumeContributionMode.VolumeOnly);
        }

        if (cachedRenderer != null)
        {
            cachedRenderer.sortingOrder = sortingOrder;
            cachedRenderer.shadowCastingMode = ShadowCastingMode.Off;
            cachedRenderer.receiveShadows = false;
        }
    }

    private void ApplySurfaceDriverOwnership()
    {
        if (!forceSurfaceDriversToSurfaceOnly || surfaceDrivers == null)
        {
            return;
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
    }

    private void RefreshSurfaceDriversIfNeeded()
    {
        if (!autoFindSurfaceDrivers)
        {
            return;
        }

        surfaceDrivers = SdfSceneDriverUtility.FindSurfaceDrivers(volumeDriver);
    }

    private void ApplyBounds()
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
        transform.position = volumeBounds.center;
        transform.rotation = Quaternion.identity;
        transform.localScale = safeSize;
    }

    private void UploadSceneSdfData()
    {
        CacheComponents();
        sceneDataBuffer.Upload(cachedRenderer, transform, surfaceDrivers, propertyBlock);
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
