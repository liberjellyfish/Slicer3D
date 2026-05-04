using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class SdfSceneDataBuffer : IDisposable
{
    private static readonly int UseSceneSdfId = Shader.PropertyToID("_UseSceneSdf");
    private static readonly int SdfSceneShapeCountId = Shader.PropertyToID("_SdfSceneShapeCount");
    private static readonly int SdfSceneShapesId = Shader.PropertyToID("_SdfSceneShapes");
    private static readonly int SdfSceneCutPlanesId = Shader.PropertyToID("_SdfSceneCutPlanes");

    private readonly List<SdfSceneShapeGpu> shapeData = new List<SdfSceneShapeGpu>();
    private readonly List<CutPlaneData> cutPlaneData = new List<CutPlaneData>();

    private ComputeBuffer sceneShapeBuffer;
    private ComputeBuffer sceneCutPlaneBuffer;

    public int ShapeCount { get; private set; }
    public int CutPlaneCount { get; private set; }

    public void Upload(Renderer targetRenderer, Transform sceneTransform, IReadOnlyList<SdfPhase1Driver> sources, MaterialPropertyBlock propertyBlock)
    {
        if (targetRenderer == null || sceneTransform == null || propertyBlock == null)
        {
            return;
        }

        BuildSceneData(sceneTransform, sources);
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

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(UseSceneSdfId, shapeData.Count > 0 ? 1.0f : 0.0f);
        propertyBlock.SetInt(SdfSceneShapeCountId, shapeData.Count);
        if (shapeData.Count > 0)
        {
            propertyBlock.SetBuffer(SdfSceneShapesId, sceneShapeBuffer);
            propertyBlock.SetBuffer(SdfSceneCutPlanesId, sceneCutPlaneBuffer);
        }

        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    public void Dispose()
    {
        ReleaseBuffer(ref sceneShapeBuffer);
        ReleaseBuffer(ref sceneCutPlaneBuffer);
        ShapeCount = 0;
        CutPlaneCount = 0;
    }

    private void BuildSceneData(Transform sceneTransform, IReadOnlyList<SdfPhase1Driver> sources)
    {
        shapeData.Clear();
        cutPlaneData.Clear();

        float volumeScale = EstimateUniformScale(sceneTransform.lossyScale);
        if (sources != null)
        {
            for (int i = 0; i < sources.Count; i++)
            {
                SdfPhase1Driver driver = sources[i];
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

        ShapeCount = shapeData.Count;
        CutPlaneCount = cutPlaneData.Count;
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

        ReleaseBuffer(ref buffer);
        buffer = new ComputeBuffer(count, stride, ComputeBufferType.Structured);
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
}
