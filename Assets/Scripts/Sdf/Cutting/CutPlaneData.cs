using System;
using System.Runtime.InteropServices;
using UnityEngine;

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct CutPlaneData
{
    public Vector3 normal;
    public float distance;
    public float sideSign;
    [HideInInspector]
    public Vector3 padding;

    public CutPlaneData(Vector3 normal, float distance, float sideSign)
    {
        this.normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
        this.distance = distance;
        this.sideSign = Mathf.Sign(Mathf.Approximately(sideSign, 0.0f) ? 1.0f : sideSign);
        this.padding = Vector3.zero;
    }

    public static int Stride => Marshal.SizeOf<CutPlaneData>();

    public float Evaluate(Vector3 point)
    {
        return Vector3.Dot(point, normal) + distance;
    }

    public Vector3 GetPointOnPlane()
    {
        return -distance * normal;
    }

    public bool KeepsPositiveSide()
    {
        return sideSign >= 0.0f;
    }

    public static CutPlaneData FromLocalPointNormal(Vector3 localPoint, Vector3 localNormal, bool keepPositiveSide)
    {
        Vector3 safeNormal = localNormal.sqrMagnitude > 1e-6f ? localNormal.normalized : Vector3.up;
        float localDistance = -Vector3.Dot(safeNormal, localPoint);
        return new CutPlaneData(safeNormal, localDistance, keepPositiveSide ? 1.0f : -1.0f);
    }

    public static CutPlaneData FromWorldPlane(Transform targetSpace, Plane worldPlane, bool keepPositiveSide)
    {
        Vector3 worldNormal = worldPlane.normal.sqrMagnitude > 1e-6f ? worldPlane.normal.normalized : Vector3.up;
        Vector3 worldPointOnPlane = -worldPlane.distance * worldNormal;

        Vector3 localNormal = targetSpace.InverseTransformDirection(worldNormal).normalized;
        Vector3 localPoint = targetSpace.InverseTransformPoint(worldPointOnPlane);

        return FromLocalPointNormal(localPoint, localNormal, keepPositiveSide);
    }
}
