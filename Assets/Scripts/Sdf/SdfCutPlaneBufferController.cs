using System;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Phase 2A / 2B：
/// 1. 维护对象空间的切割平面数据。
/// 2. 将连续的非托管数组上传到 GPU ComputeBuffer。
/// 3. 为下一步 Shader 暴力遍历与鼠标划线切割预留统一入口。
///
/// 当前阶段还不负责：
/// - Shader 中真正应用切割平面
/// - 复杂输入交互
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class SdfCutPlaneBufferController : MonoBehaviour
{
    [Header("Debug Source")]
    [SerializeField] private CutPlaneData[] debugPlanes = Array.Empty<CutPlaneData>();
    [SerializeField] private bool uploadDebugPlanesOnEnable = true;

    [Header("Gizmo")]
    [SerializeField] private bool drawPlaneGizmos = true;
    [SerializeField] private bool previewDebugPlanesWhenNotUploaded = false;
    [SerializeField] [Min(0.1f)] private float gizmoSize = 1.5f;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.9f, 1.0f, 0.85f);

    [Header("Runtime State")]
    [SerializeField] private int uploadedPlaneCount;
    [SerializeField] private int bufferCapacity;
    [SerializeField] private bool hasUploadedPlanes;

    private const string CutPlaneBufferName = "_CutPlanes";
    private const string CutPlaneCountName = "_CutPlaneCount";

    private Renderer cachedRenderer;
    private ComputeBuffer cutPlaneBuffer;
    private NativeArray<CutPlaneData> uploadCache;
    private MaterialPropertyBlock propertyBlock;

    public int UploadedPlaneCount => uploadedPlaneCount;
    public bool HasUploadedPlanes => hasUploadedPlanes;

    private void OnEnable()
    {
        CacheComponents();

        if (uploadDebugPlanesOnEnable)
        {
            SetPlanes(debugPlanes);
        }
        else
        {
            hasUploadedPlanes = false;
            BindPlaneCountOnly(0);
        }
    }

    private void OnDisable()
    {
        ReleaseResources();
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void OnValidate()
    {
        CacheComponents();

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (uploadDebugPlanesOnEnable)
        {
            SetPlanes(debugPlanes);
        }
        else
        {
            BindPlaneCountOnly(uploadedPlaneCount);
        }
    }

    [ContextMenu("Upload Debug Planes")]
    public void UploadDebugPlanes()
    {
        SetPlanes(debugPlanes);
    }

    [ContextMenu("Clear All Planes")]
    public void ClearAllPlanes()
    {
        uploadedPlaneCount = 0;
        hasUploadedPlanes = false;
        BindPlaneCountOnly(0);
    }

    /// <summary>
    /// 直接设置一组对象空间平面。
    /// 这是 2A/2B 的主入口。
    /// </summary>
    public void SetPlanes(CutPlaneData[] planes)
    {
        int count = planes != null ? planes.Length : 0;
        EnsureCapacity(count);

        uploadedPlaneCount = count;
        hasUploadedPlanes = count > 0;
        if (count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                uploadCache[i] = new CutPlaneData(planes[i].normal, planes[i].distance, planes[i].sideSign);
            }

            cutPlaneBuffer.SetData(uploadCache, 0, 0, count);
        }

        BindResources(count);
    }

    /// <summary>
    /// 追加一个世界空间平面。
    /// 这里会自动转成“当前对象的对象空间平面”。
    /// 后续鼠标划线切割可直接走这个入口。
    /// </summary>
    public void AppendWorldPlane(Plane worldPlane, bool keepPositiveSide = true)
    {
        CutPlaneData localPlane = CutPlaneData.FromWorldPlane(transform, worldPlane, keepPositiveSide);
        AppendLocalPlane(localPlane);
    }

    public void AppendLocalPlane(CutPlaneData localPlane)
    {
        CutPlaneData[] newPlanes = new CutPlaneData[uploadedPlaneCount + 1];
        for (int i = 0; i < uploadedPlaneCount; i++)
        {
            newPlanes[i] = uploadCache[i];
        }

        newPlanes[uploadedPlaneCount] = localPlane;
        SetPlanes(newPlanes);
    }

    public CutPlaneData[] GetUploadedPlanesCopy()
    {
        if (!uploadCache.IsCreated || uploadedPlaneCount <= 0)
        {
            return Array.Empty<CutPlaneData>();
        }

        return CopyUploadedPlanes();
    }

    public void SetRuntimePlanes(CutPlaneData[] planes)
    {
        uploadDebugPlanesOnEnable = false;
        debugPlanes = Array.Empty<CutPlaneData>();
        SetPlanes(planes);
    }

    private void CacheComponents()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void EnsureCapacity(int requiredCount)
    {
        if (requiredCount <= 0)
        {
            requiredCount = 1;
        }

        if (cutPlaneBuffer != null && requiredCount <= bufferCapacity && uploadCache.IsCreated)
        {
            return;
        }

        ReleaseGpuStorageOnly();

        bufferCapacity = Mathf.NextPowerOfTwo(requiredCount);
        cutPlaneBuffer = new ComputeBuffer(bufferCapacity, CutPlaneData.Stride, ComputeBufferType.Structured);
        uploadCache = new NativeArray<CutPlaneData>(bufferCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

    private void BindResources(int planeCount)
    {
        CacheComponents();
        if (cachedRenderer == null)
        {
            return;
        }

        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetInt(CutPlaneCountName, planeCount);
        if (cutPlaneBuffer != null)
        {
            propertyBlock.SetBuffer(CutPlaneBufferName, cutPlaneBuffer);
        }

        cachedRenderer.SetPropertyBlock(propertyBlock);
    }

    private void BindPlaneCountOnly(int planeCount)
    {
        CacheComponents();
        if (cachedRenderer == null)
        {
            return;
        }

        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetInt(CutPlaneCountName, planeCount);
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ReleaseGpuStorageOnly()
    {
        if (cutPlaneBuffer != null)
        {
            cutPlaneBuffer.Release();
            cutPlaneBuffer = null;
        }

        if (uploadCache.IsCreated)
        {
            uploadCache.Dispose();
        }
    }

    private void ReleaseResources()
    {
        uploadedPlaneCount = 0;
        bufferCapacity = 0;
        ReleaseGpuStorageOnly();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawPlaneGizmos)
        {
            return;
        }

        CutPlaneData[] source = uploadedPlaneCount > 0 && uploadCache.IsCreated
            ? CopyUploadedPlanes()
            : GetFallbackGizmoSource();

        if (source == null)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        for (int i = 0; i < source.Length; i++)
        {
            DrawPlaneGizmo(source[i], gizmoSize);
        }
    }

    private CutPlaneData[] CopyUploadedPlanes()
    {
        CutPlaneData[] copy = new CutPlaneData[uploadedPlaneCount];
        for (int i = 0; i < uploadedPlaneCount; i++)
        {
            copy[i] = uploadCache[i];
        }

        return copy;
    }

    private CutPlaneData[] GetFallbackGizmoSource()
    {
        if (hasUploadedPlanes && uploadCache.IsCreated)
        {
            return CopyUploadedPlanes();
        }

        return previewDebugPlanesWhenNotUploaded ? debugPlanes : Array.Empty<CutPlaneData>();
    }

    private void DrawPlaneGizmo(CutPlaneData plane, float size)
    {
        Vector3 localNormal = plane.normal.sqrMagnitude > 1e-6f ? plane.normal.normalized : Vector3.up;
        Vector3 localCenter = plane.GetPointOnPlane();

        Vector3 tangent = Vector3.Cross(localNormal, Vector3.up);
        if (tangent.sqrMagnitude < 1e-6f)
        {
            tangent = Vector3.Cross(localNormal, Vector3.right);
        }

        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(localNormal, tangent).normalized;

        Vector3 p0 = localCenter + (tangent + bitangent) * size;
        Vector3 p1 = localCenter + (tangent - bitangent) * size;
        Vector3 p2 = localCenter + (-tangent - bitangent) * size;
        Vector3 p3 = localCenter + (-tangent + bitangent) * size;

        Matrix4x4 localToWorld = transform.localToWorldMatrix;
        Vector3 w0 = localToWorld.MultiplyPoint3x4(p0);
        Vector3 w1 = localToWorld.MultiplyPoint3x4(p1);
        Vector3 w2 = localToWorld.MultiplyPoint3x4(p2);
        Vector3 w3 = localToWorld.MultiplyPoint3x4(p3);
        Vector3 centerWorld = localToWorld.MultiplyPoint3x4(localCenter);
        Vector3 normalWorld = localToWorld.MultiplyVector(localNormal).normalized * (plane.KeepsPositiveSide() ? 1.0f : -1.0f);

        Gizmos.DrawLine(w0, w1);
        Gizmos.DrawLine(w1, w2);
        Gizmos.DrawLine(w2, w3);
        Gizmos.DrawLine(w3, w0);
        Gizmos.DrawRay(centerWorld, normalWorld * size);
    }
}
