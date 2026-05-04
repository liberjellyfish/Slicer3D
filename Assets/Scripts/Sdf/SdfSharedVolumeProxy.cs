using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

    private static readonly int UseSceneSdfId = Shader.PropertyToID("_UseSceneSdf");
    private static readonly int SdfSceneShapeCountId = Shader.PropertyToID("_SdfSceneShapeCount");
    private static readonly int SdfSceneShapesId = Shader.PropertyToID("_SdfSceneShapes");
    private static readonly int SdfSceneCutPlanesId = Shader.PropertyToID("_SdfSceneCutPlanes");

    private SdfPhase1Driver volumeDriver;
    private Renderer cachedRenderer;
    private MaterialPropertyBlock propertyBlock;
    private ComputeBuffer sceneShapeBuffer;
    private ComputeBuffer sceneCutPlaneBuffer;

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
        ApplyBounds();
        UploadSceneSdfData();
    }

    private void OnValidate()
    {
        manualSize = MaxComponents(manualSize, Vector3.one * minBoundsSize);
        CacheComponents();
        ConfigureVolumeDriver();
        ApplyBounds();
        UploadSceneSdfData();
    }

    private void LateUpdate()
    {
        CacheComponents();
        ConfigureVolumeDriver();
        RefreshSurfaceDriversIfNeeded();
        ApplyBounds();
        UploadSceneSdfData();
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }

    public void SetSurfaceDrivers(SdfPhase1Driver[] drivers)
    {
        surfaceDrivers = drivers ?? Array.Empty<SdfPhase1Driver>();
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

    private void RefreshSurfaceDriversIfNeeded()
    {
        if (!autoFindSurfaceDrivers)
        {
            return;
        }

        SdfPhase1Driver[] foundDrivers = FindObjectsByType<SdfPhase1Driver>(FindObjectsSortMode.None);
        List<SdfPhase1Driver> filteredDrivers = new List<SdfPhase1Driver>(foundDrivers.Length);
        for (int i = 0; i < foundDrivers.Length; i++)
        {
            SdfPhase1Driver driver = foundDrivers[i];
            if (driver == null || driver == volumeDriver)
            {
                continue;
            }

            if (driver.GetComponent<SdfSharedVolumeProxy>() != null)
            {
                continue;
            }

            filteredDrivers.Add(driver);
        }

        surfaceDrivers = filteredDrivers.ToArray();
    }

    private void ApplyBounds()
    {
        Bounds volumeBounds;
        if (autoFitBounds && TryGetSurfaceBounds(out Bounds surfaceBounds))
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

    private bool TryGetSurfaceBounds(out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool hasBounds = false;
        if (surfaceDrivers == null)
        {
            return false;
        }

        for (int i = 0; i < surfaceDrivers.Length; i++)
        {
            SdfPhase1Driver driver = surfaceDrivers[i];
            if (driver == null)
            {
                continue;
            }

            Bounds driverBounds = driver.GetWorldBounds();
            if (!hasBounds)
            {
                combinedBounds = driverBounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(driverBounds);
        }

        return hasBounds;
    }

    private void UploadSceneSdfData()
    {
        if (cachedRenderer == null)
        {
            return;
        }

        List<SdfSceneShapeGpu> shapeData = new List<SdfSceneShapeGpu>();
        List<CutPlaneData> cutPlaneData = new List<CutPlaneData>();
        float volumeScale = EstimateUniformScale(transform.lossyScale);

        if (surfaceDrivers != null)
        {
            for (int i = 0; i < surfaceDrivers.Length; i++)
            {
                SdfPhase1Driver driver = surfaceDrivers[i];
                if (driver == null)
                {
                    continue;
                }

                CutPlaneData[] cuts = GetCutPlanes(driver);
                int cutStart = cutPlaneData.Count;
                cutPlaneData.AddRange(cuts);

                float driverScale = EstimateUniformScale(driver.transform.lossyScale);
                float distanceScale = driverScale / Mathf.Max(volumeScale, 1e-4f);
                Matrix4x4 worldToObject = driver.transform.worldToLocalMatrix;
                shapeData.Add(new SdfSceneShapeGpu
                {
                    worldToObjectRow0 = worldToObject.GetRow(0),
                    worldToObjectRow1 = worldToObject.GetRow(1),
                    worldToObjectRow2 = worldToObject.GetRow(2),
                    sphereCenterRadius = new Vector4(
                        driver.SphereCenter.x,
                        driver.SphereCenter.y,
                        driver.SphereCenter.z,
                        driver.SphereRadius),
                    boxExtentsShapeMode = new Vector4(
                        driver.BoxExtents.x,
                        driver.BoxExtents.y,
                        driver.BoxExtents.z,
                        (float)driver.CurrentShapeMode),
                    baseCutPlane = new Vector4(
                        driver.BaseCutPlaneNormal.x,
                        driver.BaseCutPlaneNormal.y,
                        driver.BaseCutPlaneNormal.z,
                        driver.BaseCutPlaneOffset),
                    cutRangeAndDistanceScale = new Vector4(cutStart, cuts.Length, distanceScale, 0.0f)
                });
            }
        }

        EnsureBuffer(ref sceneShapeBuffer, Mathf.Max(shapeData.Count, 1), SdfSceneShapeGpu.Stride);
        EnsureBuffer(ref sceneCutPlaneBuffer, Mathf.Max(cutPlaneData.Count, 1), CutPlaneData.Stride);

        if (shapeData.Count > 0)
        {
            sceneShapeBuffer.SetData(shapeData.ToArray());
        }

        if (cutPlaneData.Count > 0)
        {
            sceneCutPlaneBuffer.SetData(cutPlaneData.ToArray());
        }

        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(UseSceneSdfId, shapeData.Count > 0 ? 1.0f : 0.0f);
        propertyBlock.SetInt(SdfSceneShapeCountId, shapeData.Count);
        if (shapeData.Count > 0)
        {
            propertyBlock.SetBuffer(SdfSceneShapesId, sceneShapeBuffer);
            propertyBlock.SetBuffer(SdfSceneCutPlanesId, sceneCutPlaneBuffer);
        }

        cachedRenderer.SetPropertyBlock(propertyBlock);
    }

    private static CutPlaneData[] GetCutPlanes(SdfPhase1Driver driver)
    {
        SdfCutPlaneBufferController cutPlaneBufferController = driver.GetComponent<SdfCutPlaneBufferController>();
        return cutPlaneBufferController != null
            ? cutPlaneBufferController.GetUploadedPlanesCopy()
            : Array.Empty<CutPlaneData>();
    }

    private static void EnsureBuffer(ref ComputeBuffer buffer, int count, int stride)
    {
        if (buffer != null && buffer.count >= count && buffer.stride == stride)
        {
            return;
        }

        if (buffer != null)
        {
            buffer.Release();
        }

        buffer = new ComputeBuffer(count, stride, ComputeBufferType.Structured);
    }

    private void ReleaseBuffers()
    {
        if (sceneShapeBuffer != null)
        {
            sceneShapeBuffer.Release();
            sceneShapeBuffer = null;
        }

        if (sceneCutPlaneBuffer != null)
        {
            sceneCutPlaneBuffer.Release();
            sceneCutPlaneBuffer = null;
        }
    }

    private static float EstimateUniformScale(Vector3 scale)
    {
        return Mathf.Max((Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3.0f, 1e-4f);
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

    [StructLayout(LayoutKind.Sequential)]
    private struct SdfSceneShapeGpu
    {
        public Vector4 worldToObjectRow0;
        public Vector4 worldToObjectRow1;
        public Vector4 worldToObjectRow2;
        public Vector4 sphereCenterRadius;
        public Vector4 boxExtentsShapeMode;
        public Vector4 baseCutPlane;
        public Vector4 cutRangeAndDistanceScale;

        public const int Stride = 112;
    }
}
