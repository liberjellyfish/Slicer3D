using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Profiling;

public sealed class SdfSceneDataBuffer : IDisposable
{
    private static readonly ProfilerMarker UploadBuffersMarker = new ProfilerMarker("SDF.SceneData.UploadBuffers");
    private static readonly ProfilerMarker BuildSceneDataMarker = new ProfilerMarker("SDF.SceneData.Build");

    private static readonly int UseSceneSdfId = Shader.PropertyToID("_UseSceneSdf");
    private static readonly int SdfSceneShapeCountId = Shader.PropertyToID("_SdfSceneShapeCount");
    private static readonly int SdfSceneShapesId = Shader.PropertyToID("_SdfSceneShapes");
    private static readonly int SdfSceneCutPlanesId = Shader.PropertyToID("_SdfSceneCutPlanes");
    private static readonly int SdfShadowSceneShapeCountId = Shader.PropertyToID("_SdfShadowSceneShapeCount");
    private static readonly int SdfShadowSceneShapesId = Shader.PropertyToID("_SdfShadowSceneShapes");
    private static readonly int SdfShadowSceneCutPlanesId = Shader.PropertyToID("_SdfShadowSceneCutPlanes");

    private readonly List<SdfSceneShapeGpu> shapeData = new List<SdfSceneShapeGpu>();
    private readonly List<CutPlaneData> cutPlaneData = new List<CutPlaneData>();
    private readonly List<SdfCutInfluenceGpu> cutInfluenceData = new List<SdfCutInfluenceGpu>();

    private ComputeBuffer sceneShapeBuffer;
    private ComputeBuffer sceneCutPlaneBuffer;
    private ComputeBuffer cutInfluenceBuffer;
    private SdfSceneShapeGpu[] shapeUploadCache = Array.Empty<SdfSceneShapeGpu>();
    private CutPlaneData[] cutPlaneUploadCache = Array.Empty<CutPlaneData>();
    private SdfCutInfluenceGpu[] cutInfluenceUploadCache = Array.Empty<SdfCutInfluenceGpu>();
    private int lastSceneSourceHash = int.MinValue;
    private int lastUploadedDataHash = int.MinValue;
    private int dataVersion;
    private int lastGlobalDataVersion = -1;
    private int lastGlobalShapeCount = -1;
    private bool shadowGlobalsBound;
    private Renderer lastTargetRenderer;
    private int lastRendererDataVersion = -1;
    private int lastRendererShapeCount = -1;

    public int ShapeCount { get; private set; }
    public int CutPlaneCount { get; private set; }
    public int DataVersion => dataVersion;
    public ComputeBuffer ShapeBuffer => sceneShapeBuffer;
    public ComputeBuffer CutPlaneBuffer => sceneCutPlaneBuffer;
    public ComputeBuffer CutInfluenceBuffer => cutInfluenceBuffer;
    public bool LastSyncRebuiltData { get; private set; }
    public bool LastSyncUploadedBuffers { get; private set; }
    public bool LastSyncBoundShadowGlobals { get; private set; }
    public bool LastSyncBoundRendererProperties { get; private set; }

    public bool TryGetBuffers(
        out ComputeBuffer shapeBuffer,
        out ComputeBuffer cutPlaneBuffer,
        out ComputeBuffer cutInfluenceBuffer,
        out int shapeCount,
        out int cutPlaneCount,
        out int version)
    {
        shapeBuffer = sceneShapeBuffer;
        cutPlaneBuffer = sceneCutPlaneBuffer;
        cutInfluenceBuffer = this.cutInfluenceBuffer;
        shapeCount = ShapeCount;
        cutPlaneCount = CutPlaneCount;
        version = dataVersion;
        return shapeBuffer != null && cutPlaneBuffer != null && cutInfluenceBuffer != null && shapeCount > 0;
    }

    public bool Upload(Renderer targetRenderer, Transform sceneTransform, IReadOnlyList<SdfPhase1Driver> sources, MaterialPropertyBlock propertyBlock)
    {
        if (targetRenderer == null || sceneTransform == null || propertyBlock == null)
        {
            return false;
        }

        bool buffersChanged = UploadBuffers(sceneTransform, sources);

        if (ShouldBindRendererProperties(targetRenderer))
        {
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(UseSceneSdfId, shapeData.Count > 0 ? 1.0f : 0.0f);
            propertyBlock.SetInt(SdfSceneShapeCountId, shapeData.Count);
            if (shapeData.Count > 0)
            {
                propertyBlock.SetBuffer(SdfSceneShapesId, sceneShapeBuffer);
                propertyBlock.SetBuffer(SdfSceneCutPlanesId, sceneCutPlaneBuffer);
            }

            targetRenderer.SetPropertyBlock(propertyBlock);
            lastTargetRenderer = targetRenderer;
            lastRendererDataVersion = dataVersion;
            lastRendererShapeCount = shapeData.Count;
            LastSyncBoundRendererProperties = true;
        }

        bool globalsChanged = UploadShadowSceneGlobals();
        return buffersChanged || LastSyncBoundRendererProperties || globalsChanged;
    }

    public bool UploadGlobals(Transform sceneTransform, IReadOnlyList<SdfPhase1Driver> sources)
    {
        if (sceneTransform == null)
        {
            return false;
        }

        bool buffersChanged = UploadBuffers(sceneTransform, sources);
        bool globalsChanged = UploadShadowSceneGlobals();
        return buffersChanged || globalsChanged;
    }

    public void Dispose()
    {
        Shader.SetGlobalInt(SdfShadowSceneShapeCountId, 0);
        ReleaseBuffer(ref sceneShapeBuffer);
        ReleaseBuffer(ref sceneCutPlaneBuffer);
        ReleaseBuffer(ref cutInfluenceBuffer);
        ShapeCount = 0;
        CutPlaneCount = 0;
        dataVersion = 0;
        lastSceneSourceHash = int.MinValue;
        lastUploadedDataHash = int.MinValue;
        lastGlobalDataVersion = -1;
        lastGlobalShapeCount = -1;
        shadowGlobalsBound = false;
        lastTargetRenderer = null;
        lastRendererDataVersion = -1;
        lastRendererShapeCount = -1;
    }

    private void BuildSceneData(Transform sceneTransform, IReadOnlyList<SdfPhase1Driver> sources)
    {
        using (BuildSceneDataMarker.Auto())
        {
            shapeData.Clear();
            cutPlaneData.Clear();
            cutInfluenceData.Clear();

            float volumeScale = EstimateUniformScale(sceneTransform.lossyScale);
            if (sources != null)
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    SdfPhase1Driver driver = sources[i];
                    if (!SdfSceneDriverUtility.IsRenderableSurfaceDriver(driver))
                    {
                        continue;
                    }

                    CutPlaneData[] cuts = GetCutPlanes(driver);
                    int cutStart = cutPlaneData.Count;
                    cutPlaneData.AddRange(cuts);
                    Bounds influenceBounds = driver.GetWorldBounds();
                    for (int cutIndex = 0; cutIndex < cuts.Length; cutIndex++)
                    {
                        cutInfluenceData.Add(new SdfCutInfluenceGpu
                        {
                            boundsMinWS = new Vector4(influenceBounds.min.x, influenceBounds.min.y, influenceBounds.min.z, 0.0f),
                            boundsMaxWS = new Vector4(influenceBounds.max.x, influenceBounds.max.y, influenceBounds.max.z, 0.0f)
                        });
                    }

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

            ShapeCount = shapeData.Count;
            CutPlaneCount = cutPlaneData.Count;
        }
    }

    private bool UploadBuffers(Transform sceneTransform, IReadOnlyList<SdfPhase1Driver> sources)
    {
        using (UploadBuffersMarker.Auto())
        {
            LastSyncRebuiltData = false;
            LastSyncUploadedBuffers = false;
            LastSyncBoundShadowGlobals = false;
            LastSyncBoundRendererProperties = false;

            int sceneSourceHash = CalculateSceneSourceHash(sceneTransform, sources);
            if (sceneSourceHash == lastSceneSourceHash && sceneShapeBuffer != null && sceneCutPlaneBuffer != null && cutInfluenceBuffer != null)
            {
                return false;
            }

            BuildSceneData(sceneTransform, sources);
            LastSyncRebuiltData = true;
            bool shapeBufferChanged = EnsureBuffer(ref sceneShapeBuffer, Mathf.Max(shapeData.Count, 1), SdfSceneShapeGpu.Stride);
            bool cutPlaneBufferChanged = EnsureBuffer(ref sceneCutPlaneBuffer, Mathf.Max(cutPlaneData.Count, 1), CutPlaneData.Stride);
            bool cutInfluenceBufferChanged = EnsureBuffer(ref cutInfluenceBuffer, Mathf.Max(cutInfluenceData.Count, 1), SdfCutInfluenceGpu.Stride);
            int dataHash = CalculateUploadHash();
            bool dataChanged = shapeBufferChanged || cutPlaneBufferChanged || cutInfluenceBufferChanged || dataHash != lastUploadedDataHash;
            if (dataChanged)
            {
                if (shapeData.Count > 0)
                {
                    EnsureArrayCapacity(ref shapeUploadCache, shapeData.Count);
                    shapeData.CopyTo(shapeUploadCache);
                    sceneShapeBuffer.SetData(shapeUploadCache, 0, 0, shapeData.Count);
                }

                if (cutPlaneData.Count > 0)
                {
                    EnsureArrayCapacity(ref cutPlaneUploadCache, cutPlaneData.Count);
                    cutPlaneData.CopyTo(cutPlaneUploadCache);
                    sceneCutPlaneBuffer.SetData(cutPlaneUploadCache, 0, 0, cutPlaneData.Count);
                }

                if (cutInfluenceData.Count > 0)
                {
                    EnsureArrayCapacity(ref cutInfluenceUploadCache, cutInfluenceData.Count);
                    cutInfluenceData.CopyTo(cutInfluenceUploadCache);
                    cutInfluenceBuffer.SetData(cutInfluenceUploadCache, 0, 0, cutInfluenceData.Count);
                }

                lastUploadedDataHash = dataHash;
                IncrementDataVersion();
                LastSyncUploadedBuffers = true;
            }

            lastSceneSourceHash = sceneSourceHash;
            return LastSyncRebuiltData || LastSyncUploadedBuffers;
        }
    }

    private bool UploadShadowSceneGlobals()
    {
        if (shadowGlobalsBound && lastGlobalDataVersion == dataVersion && lastGlobalShapeCount == shapeData.Count)
        {
            return false;
        }

        Shader.SetGlobalInt(SdfShadowSceneShapeCountId, shapeData.Count);
        if (shapeData.Count <= 0 || sceneShapeBuffer == null || sceneCutPlaneBuffer == null)
        {
            lastGlobalDataVersion = dataVersion;
            lastGlobalShapeCount = shapeData.Count;
            shadowGlobalsBound = true;
            LastSyncBoundShadowGlobals = true;
            return true;
        }

        Shader.SetGlobalBuffer(SdfShadowSceneShapesId, sceneShapeBuffer);
        Shader.SetGlobalBuffer(SdfShadowSceneCutPlanesId, sceneCutPlaneBuffer);
        lastGlobalDataVersion = dataVersion;
        lastGlobalShapeCount = shapeData.Count;
        shadowGlobalsBound = true;
        LastSyncBoundShadowGlobals = true;
        return true;
    }

    private static CutPlaneData[] GetCutPlanes(SdfPhase1Driver driver)
    {
        SdfCutPlaneBufferController cutPlaneBufferController = driver.GetComponent<SdfCutPlaneBufferController>();
        return cutPlaneBufferController != null
            ? cutPlaneBufferController.GetUploadedPlanesCopy()
            : Array.Empty<CutPlaneData>();
    }

    private bool ShouldBindRendererProperties(Renderer targetRenderer)
    {
        return lastTargetRenderer != targetRenderer
            || lastRendererDataVersion != dataVersion
            || lastRendererShapeCount != shapeData.Count;
    }

    private int CalculateSceneSourceHash(Transform sceneTransform, IReadOnlyList<SdfPhase1Driver> sources)
    {
        unchecked
        {
            int hash = 17;
            AppendHash(ref hash, sceneTransform.localToWorldMatrix);
            hash = hash * 31 + (sources != null ? sources.Count : 0);
            if (sources == null)
            {
                return hash;
            }

            for (int i = 0; i < sources.Count; i++)
            {
                SdfPhase1Driver driver = sources[i];
                hash = hash * 31 + (driver != null ? driver.GetInstanceID() : 0);
                bool isRenderable = SdfSceneDriverUtility.IsRenderableSurfaceDriver(driver);
                hash = hash * 31 + (isRenderable ? 1 : 0);
                if (!isRenderable)
                {
                    continue;
                }

                hash = hash * 31 + driver.SceneDataVersion;
                AppendHash(ref hash, driver.transform.localToWorldMatrix);
                SdfCutPlaneBufferController cutPlaneBufferController = driver.GetComponent<SdfCutPlaneBufferController>();
                hash = hash * 31 + (cutPlaneBufferController != null ? cutPlaneBufferController.DataVersion : 0);
            }

            return hash;
        }
    }

    private static bool EnsureBuffer(ref ComputeBuffer buffer, int count, int stride)
    {
        if (buffer != null && buffer.count >= count && buffer.stride == stride)
        {
            return false;
        }

        ReleaseBuffer(ref buffer);
        buffer = new ComputeBuffer(count, stride, ComputeBufferType.Structured);
        return true;
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

    private static void EnsureArrayCapacity<T>(ref T[] array, int requiredCount)
    {
        if (array != null && array.Length >= requiredCount)
        {
            return;
        }

        array = new T[Mathf.NextPowerOfTwo(Mathf.Max(requiredCount, 1))];
    }

    private void IncrementDataVersion()
    {
        unchecked
        {
            dataVersion++;
            if (dataVersion == 0)
            {
                dataVersion = 1;
            }
        }
    }

    private int CalculateUploadHash()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + shapeData.Count;
            hash = hash * 31 + cutPlaneData.Count;
            hash = hash * 31 + cutInfluenceData.Count;

            for (int i = 0; i < shapeData.Count; i++)
            {
                SdfSceneShapeGpu shape = shapeData[i];
                AppendHash(ref hash, shape.worldToObjectRow0);
                AppendHash(ref hash, shape.worldToObjectRow1);
                AppendHash(ref hash, shape.worldToObjectRow2);
                AppendHash(ref hash, shape.sphereCenterRadius);
                AppendHash(ref hash, shape.boxExtentsShapeMode);
                AppendHash(ref hash, shape.baseCutPlane);
                AppendHash(ref hash, shape.cutRangeAndDistanceScale);
            }

            for (int i = 0; i < cutPlaneData.Count; i++)
            {
                CutPlaneData cutPlane = cutPlaneData[i];
                AppendHash(ref hash, cutPlane.normal);
                hash = hash * 31 + cutPlane.distance.GetHashCode();
                hash = hash * 31 + cutPlane.sideSign.GetHashCode();
            }

            for (int i = 0; i < cutInfluenceData.Count; i++)
            {
                SdfCutInfluenceGpu influence = cutInfluenceData[i];
                AppendHash(ref hash, influence.boundsMinWS);
                AppendHash(ref hash, influence.boundsMaxWS);
            }

            return hash;
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

    private static void AppendHash(ref int hash, Vector4 value)
    {
        unchecked
        {
            hash = hash * 31 + value.x.GetHashCode();
            hash = hash * 31 + value.y.GetHashCode();
            hash = hash * 31 + value.z.GetHashCode();
            hash = hash * 31 + value.w.GetHashCode();
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

    private static float EstimateUniformScale(Vector3 scale)
    {
        return Mathf.Max((Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3.0f, 1e-4f);
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

        public static int Stride => Marshal.SizeOf<SdfSceneShapeGpu>();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SdfCutInfluenceGpu
    {
        public Vector4 boundsMinWS;
        public Vector4 boundsMaxWS;

        public static int Stride => Marshal.SizeOf<SdfCutInfluenceGpu>();
    }
}
