using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// GPU 侧切割平面协议。
/// 平面方程统一约定为：
/// planeSdf = dot(p, normal) + distance
///
/// 在后续 Shader 中使用：
/// d = max(d, -planeSdf)
///
/// 这意味着：
/// - planeSdf >= 0 的一侧保留
/// - planeSdf < 0 的一侧被切掉
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct CutPlaneData
{
    public Vector3 normal;
    public float distance;

    public CutPlaneData(Vector3 normal, float distance)
    {
        this.normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
        this.distance = distance;
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

    public static CutPlaneData FromLocalPointNormal(Vector3 localPoint, Vector3 localNormal)
    {
        Vector3 safeNormal = localNormal.sqrMagnitude > 1e-6f ? localNormal.normalized : Vector3.up;
        float localDistance = -Vector3.Dot(safeNormal, localPoint);
        return new CutPlaneData(safeNormal, localDistance);
    }

    public static CutPlaneData FromWorldPlane(Transform targetSpace, Plane worldPlane)
    {
        Vector3 worldNormal = worldPlane.normal.sqrMagnitude > 1e-6f
            ? worldPlane.normal.normalized
            : Vector3.up;

        // Unity Plane 满足：dot(n, x) + distance = 0
        // 因此取 x = -distance * n 可得到平面上的一个点。
        Vector3 worldPointOnPlane = -worldPlane.distance * worldNormal;

        Vector3 localNormal = targetSpace.InverseTransformDirection(worldNormal).normalized;
        Vector3 localPoint = targetSpace.InverseTransformPoint(worldPointOnPlane);

        return FromLocalPointNormal(localPoint, localNormal);
    }
}
