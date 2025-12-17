using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 封口生成器
/// 负责将切割产生的离散点云转化为闭合的多边形网格
/// </summary>
public static class CapTriangulator
{
    public struct CapMesh
    {
        public Vector3[] Vertices;
        public int[] Triangles;
        public Vector2[] UVs;
    }

    /// <summary>
    /// 生成封口网格 (目前优化用于凸多面体 Convex)
    /// </summary>
    /// <param name="intersections">杂乱的切点列表</param>
    /// <param name="planeNormal">切面法线（用于确定投影平面）</param>
    public static CapMesh GenerateCap(List<Vector3> intersections, Vector3 planeNormal)
    {
        // 1. 数据清洗：去除重复点
        // 阈值的平方，避免开根号计算
        const float THRESHOLD_SQ = 0.001f * 0.001f;

        List<Vector3> uniquePoints = new List<Vector3>(intersections.Count);

        foreach (var p in intersections)
        {
            bool exists = false;
            foreach (var existing in uniquePoints)
            {
                if (Vector3.SqrMagnitude(p - existing) < THRESHOLD_SQ)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists) uniquePoints.Add(p);
        }

        // 至少需要3个点才能构成面
        if (uniquePoints.Count < 3) return new CapMesh();

        // 2. 几何中心计算 (用于极角排序)
        Vector3 center = Vector3.zero;
        foreach (var p in uniquePoints) center += p;
        center /= uniquePoints.Count;

        // 3. 构建2D投影坐标基 (Basis Vectors)
        // 我们需要在这个切面上建立一个局部坐标系 (Forward, Right)
        Vector3 forward;
        // 避免 Forward 与法线平行导致叉乘失效
        if (Mathf.Abs(Vector3.Dot(planeNormal, Vector3.up)) > 0.99f)
            forward = Vector3.Cross(planeNormal, Vector3.right).normalized;
        else
            forward = Vector3.Cross(planeNormal, Vector3.up).normalized;

        Vector3 right = Vector3.Cross(planeNormal, forward).normalized;

        // 4. 极角排序 (构建闭合环)
        // 使用 Atan2 确保点按顺时针/逆时针排列
        uniquePoints.Sort((a, b) =>
        {
            Vector3 dirA = a - center;
            Vector3 dirB = b - center;

            // 投影到切面坐标系
            float angleA = Mathf.Atan2(Vector3.Dot(dirA, forward), Vector3.Dot(dirA, right));
            float angleB = Mathf.Atan2(Vector3.Dot(dirB, forward), Vector3.Dot(dirB, right));

            return angleA.CompareTo(angleB);
        });

        // 5. 三角剖分 (Vertex Fan 模式)
        // 选取第一个点 (Index 0) 作为扇面的轴心，连接其他所有点
        // 这种方法对于凸多边形是最高效的 (O(N))

        int vertexCount = uniquePoints.Count;
        int triangleCount = vertexCount - 2;

        List<Vector3> capVerts = uniquePoints; // 直接引用，无需复制
        List<int> capTris = new List<int>(triangleCount * 3);
        List<Vector2> capUVs = new List<Vector2>(vertexCount);

        // 填充默认 UV
        for (int i = 0; i < vertexCount; i++) capUVs.Add(Vector2.zero);

        // 构建三角形索引
        // Pivot(0) -> Current(i+1) -> Next(i+2)
        for (int i = 0; i < triangleCount; i++)
        {
            capTris.Add(0);
            capTris.Add(i + 1);
            capTris.Add(i + 2);
        }

        return new CapMesh
        {
            Vertices = capVerts.ToArray(),
            Triangles = capTris.ToArray(),
            UVs = capUVs.ToArray()
        };
    }
}