using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 静态网格切割核心算法库
/// <para>负责处理网格的几何切割、顶点分类、截面生成和封口。</para>
/// <para>采用零 GC (Zero Alloc) 设计，通过静态缓存复用内存。</para>
/// </summary>
public static class MeshSlicer
{
    #region 1. 公共数据结构

    /// <summary>
    /// 切割结果容器，包含正反两个新网格
    /// </summary>
    public class SlicedHull
    {
        public Mesh PositiveMesh;
        public Mesh NegativeMesh;
    }

    #endregion

    #region 2. 缓存系统 (Memory Cache)

    /// <summary>
    /// 静态缓存池
    /// 用于复用 List 和 Array，避免每次切割时产生 GC Alloc
    /// </summary>
    private static class Cache
    {
        // --- 原始网格数据缓存 ---
        public static List<Vector3> SrcVertices = new List<Vector3>(10000);
        public static List<Vector3> SrcNormals = new List<Vector3>(10000);
        public static List<Vector2> SrcUVs = new List<Vector2>(10000);
        public static List<int> SrcTriangles = new List<int>(10000); // 消除 GetTriangles 产生的垃圾

        // --- 封口计算缓存 ---
        public static List<Edge> CapEdges = new List<Edge>(1000);
        // 空间哈希字典，用于 O(1) 查找边
        public static Dictionary<Vector3Int, int> EdgeMap = new Dictionary<Vector3Int, int>(1000);
        public static List<Vector3> LoopVerts = new List<Vector3>(100);

        // --- 构建器缓存 ---
        public static BuilderCache Pos = new BuilderCache();
        public static BuilderCache Neg = new BuilderCache();

        /// <summary>
        /// 清理所有缓存数据，准备下一次计算
        /// </summary>
        public static void Clear()
        {
            SrcVertices.Clear();
            SrcNormals.Clear();
            SrcUVs.Clear();
            SrcTriangles.Clear();
            CapEdges.Clear();
            EdgeMap.Clear();
            Pos.Clear();
            Neg.Clear();
            LoopVerts.Clear();
        }
    }

    /// <summary>
    /// 网格构建器专用的底层数据缓存
    /// </summary>
    private class BuilderCache
    {
        public List<Vector3> Vertices = new List<Vector3>(5000);
        public List<Vector3> Normals = new List<Vector3>(5000);
        public List<Vector2> UVs = new List<Vector2>(5000);
        public List<List<int>> SubMeshTris = new List<List<int>>();

        public void Clear()
        {
            Vertices.Clear();
            Normals.Clear();
            UVs.Clear();
            foreach (var list in SubMeshTris) list.Clear();
        }

        public void EnsureSubMeshCount(int count)
        {
            while (SubMeshTris.Count < count)
                SubMeshTris.Add(new List<int>(1000));
        }
    }

    #endregion

    #region 3. 辅助结构体

    /// <summary>
    /// 轻量级网格构建器
    /// 封装了向 Cache 添加顶点和三角形的操作
    /// </summary>
    private struct MeshBuilder
    {
        private BuilderCache builderCache;

        public MeshBuilder(BuilderCache cache, int subMeshCount)
        {
            builderCache = cache;
            builderCache.EnsureSubMeshCount(subMeshCount);
        }

        public int AddVertex(Vector3 v, Vector3 n, Vector2 uv)
        {
            builderCache.Vertices.Add(v);
            builderCache.Normals.Add(n);
            builderCache.UVs.Add(uv);
            return builderCache.Vertices.Count - 1;
        }

        public void AddTriangle(int subMeshIndex, int i1, int i2, int i3)
        {
            builderCache.SubMeshTris[subMeshIndex].Add(i1);
            builderCache.SubMeshTris[subMeshIndex].Add(i2);
            builderCache.SubMeshTris[subMeshIndex].Add(i3);
        }

        public Mesh ToMesh()
        {
            Mesh mesh = new Mesh();
            mesh.SetVertices(builderCache.Vertices);
            mesh.SetNormals(builderCache.Normals);
            mesh.SetUVs(0, builderCache.UVs);

            mesh.subMeshCount = builderCache.SubMeshTris.Count;
            for (int i = 0; i < builderCache.SubMeshTris.Count; i++)
            {
                // 处理空 SubMesh 的情况
                if (builderCache.SubMeshTris[i].Count > 0)
                    mesh.SetTriangles(builderCache.SubMeshTris[i], i);
                else
                    mesh.SetTriangles((List<int>)null, i);
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// 简单的边结构，用于记录切面线段
    /// </summary>
    private struct Edge
    {
        public Vector3 Start, End;
        public Edge(Vector3 s, Vector3 e) { Start = s; End = e; }
    }

    #endregion

    #region 4. 核心逻辑 (Core Logic)

    /// <summary>
    /// 执行网格切割的主入口
    /// </summary>
    /// <param name="mesh">原始网格</param>
    /// <param name="plane">切割平面（局部坐标系）</param>
    public static SlicedHull SliceMesh(Mesh mesh, Plane plane)
    {
        // 1. 初始化阶段：清理缓存并读取原始数据 (Zero Alloc)
        Cache.Clear();
        mesh.GetVertices(Cache.SrcVertices);
        mesh.GetNormals(Cache.SrcNormals);
        mesh.GetUVs(0, Cache.SrcUVs);

        int subMeshCount = mesh.subMeshCount;
        MeshBuilder posMesh = new MeshBuilder(Cache.Pos, subMeshCount);
        MeshBuilder negMesh = new MeshBuilder(Cache.Neg, subMeshCount);

        // 2. 遍历阶段：处理所有三角形
        for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
        {
            // 使用复用列表获取三角形索引
            mesh.GetTriangles(Cache.SrcTriangles, subMeshIndex);
            int triCount = Cache.SrcTriangles.Count;

            for (int i = 0; i < triCount; i += 3)
            {
                int i1 = Cache.SrcTriangles[i];
                int i2 = Cache.SrcTriangles[i + 1];
                int i3 = Cache.SrcTriangles[i + 2];

                Vector3 v1 = Cache.SrcVertices[i1]; Vector3 v2 = Cache.SrcVertices[i2]; Vector3 v3 = Cache.SrcVertices[i3];
                Vector3 n1 = Cache.SrcNormals[i1]; Vector3 n2 = Cache.SrcNormals[i2]; Vector3 n3 = Cache.SrcNormals[i3];
                Vector2 uv1 = Cache.SrcUVs[i1]; Vector2 uv2 = Cache.SrcUVs[i2]; Vector2 uv3 = Cache.SrcUVs[i3];

                // 预计算距离，减少重复运算
                float d1 = plane.GetDistanceToPoint(v1);
                float d2 = plane.GetDistanceToPoint(v2);
                float d3 = plane.GetDistanceToPoint(v3);

                bool s1 = d1 > 0;
                bool s2 = d2 > 0;
                bool s3 = d3 > 0;

                // Case A: 三角形完全在平面一侧
                if (s1 == s2 && s2 == s3)
                {
                    MeshBuilder target = s1 ? posMesh : negMesh;
                    int idx1 = target.AddVertex(v1, n1, uv1);
                    int idx2 = target.AddVertex(v2, n2, uv2);
                    int idx3 = target.AddVertex(v3, n3, uv3);
                    target.AddTriangle(subMeshIndex, idx1, idx2, idx3);
                }
                // Case B: 三角形被切割
                else
                {
                    // 归一化处理：确保第一个参数总是“孤立点”
                    // 这样 CutTriangle 只需要处理一种拓扑情况
                    Vector3 rV1, rV2, rV3; Vector3 rN1, rN2, rN3; Vector2 rUv1, rUv2, rUv3;
                    float rD1, rD2, rD3;
                    bool rS1;

                    if (s1 == s3)
                    { // s2 是孤立点
                        rV1 = v2; rV2 = v3; rV3 = v1;
                        rN1 = n2; rN2 = n3; rN3 = n1;
                        rUv1 = uv2; rUv2 = uv3; rUv3 = uv1;
                        rD1 = d2; rD2 = d3; rD3 = d1;
                        rS1 = s2;
                    }
                    else if (s1 == s2)
                    { // s3 是孤立点
                        rV1 = v3; rV2 = v1; rV3 = v2;
                        rN1 = n3; rN2 = n1; rN3 = n2;
                        rUv1 = uv3; rUv2 = uv1; rUv3 = uv2;
                        rD1 = d3; rD2 = d1; rD3 = d2;
                        rS1 = s3;
                    }
                    else
                    { // s1 是孤立点
                        rV1 = v1; rV2 = v2; rV3 = v3;
                        rN1 = n1; rN2 = n2; rN3 = n3;
                        rUv1 = uv1; rUv2 = uv2; rUv3 = uv3;
                        rD1 = d1; rD2 = d2; rD3 = d3;
                        rS1 = s1;
                    }

                    CutTriangle(subMeshIndex, rV1, rV2, rV3, rN1, rN2, rN3, rUv1, rUv2, rUv3, rD1, rD2, rD3, rS1, posMesh, negMesh);
                }
            }
        }

        // 3. 封口阶段：生成切面
        GenerateCaps(posMesh, negMesh, plane);

        return new SlicedHull { PositiveMesh = posMesh.ToMesh(), NegativeMesh = negMesh.ToMesh() };
    }

    #endregion

    #region 5. 几何算法 (Geometry Helpers)

    /// <summary>
    /// 切割单个三角形，并记录封口边
    /// </summary>
    private static void CutTriangle(int subMeshIndex,
        Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 n1, Vector3 n2, Vector3 n3,
        Vector2 uv1, Vector2 uv2, Vector2 uv3,
        float d1, float d2, float d3,
        bool s1, MeshBuilder posSide, MeshBuilder negSide)
    {
        // 计算插值比例 (利用相似三角形原理)
        float t1 = d1 / (d1 - d2);
        float t2 = d1 / (d1 - d3);

        // 插值计算交点属性
        Vector3 enter = Vector3.Lerp(v1, v2, t1);
        Vector3 exit = Vector3.Lerp(v1, v3, t2);
        Vector3 nEnter = Vector3.Lerp(n1, n2, t1);
        Vector3 nExit = Vector3.Lerp(n1, n3, t2);
        Vector2 uvEnter = Vector2.Lerp(uv1, uv2, t1);
        Vector2 uvExit = Vector2.Lerp(uv1, uv3, t2);

        // 缝合孤立点侧 (形成 1 个三角形)
        MeshBuilder soloBuilder = s1 ? posSide : negSide;
        int iV1 = soloBuilder.AddVertex(v1, n1, uv1);
        int iEnter = soloBuilder.AddVertex(enter, nEnter, uvEnter);
        int iExit = soloBuilder.AddVertex(exit, nExit, uvExit);
        soloBuilder.AddTriangle(subMeshIndex, iV1, iEnter, iExit);

        // 缝合另一侧 (梯形拆分为 2 个三角形)
        MeshBuilder otherBuilder = s1 ? negSide : posSide;
        int oV2 = otherBuilder.AddVertex(v2, n2, uv2);
        int oV3 = otherBuilder.AddVertex(v3, n3, uv3);
        int oEnter = otherBuilder.AddVertex(enter, nEnter, uvEnter);
        int oExit = otherBuilder.AddVertex(exit, nExit, uvExit);

        otherBuilder.AddTriangle(subMeshIndex, oV2, oV3, oExit);
        otherBuilder.AddTriangle(subMeshIndex, oV2, oExit, oEnter);

        // 记录封口边 (统一按 Positive Mesh 逆时针方向记录)
        if (s1) Cache.CapEdges.Add(new Edge(enter, exit));
        else Cache.CapEdges.Add(new Edge(exit, enter));
    }

    /// <summary>
    /// 生成封口 (Cap Generation)
    /// </summary>
    private static void GenerateCaps(MeshBuilder posMesh, MeshBuilder negMesh, Plane plane)
    {
        var edges = Cache.CapEdges;
        if (edges.Count < 3) return;

        // 构建空间查找表 (O(N) 优化)
        for (int i = 0; i < edges.Count; i++)
        {
            Vector3Int key = Quantize(edges[i].Start);
            if (!Cache.EdgeMap.ContainsKey(key))
            {
                Cache.EdgeMap[key] = i;
            }
        }

        // 提取所有闭合环路
        while (Cache.EdgeMap.Count > 0)
        {
            var enumerator = Cache.EdgeMap.GetEnumerator();
            if (!enumerator.MoveNext()) break;

            int startEdgeIdx = enumerator.Current.Value;
            Cache.EdgeMap.Remove(enumerator.Current.Key);

            Cache.LoopVerts.Clear();
            Edge current = edges[startEdgeIdx];
            Cache.LoopVerts.Add(current.Start);
            Vector3 loopEnd = current.End;

            bool closed = false;
            int safety = 0;

            // 追踪链条
            while (safety++ < 5000)
            {
                Vector3Int key = Quantize(loopEnd);

                if (Cache.EdgeMap.TryGetValue(key, out int nextIdx))
                {
                    Cache.LoopVerts.Add(loopEnd);

                    current = edges[nextIdx];
                    loopEnd = current.End;
                    Cache.EdgeMap.Remove(key);

                    // 检查是否闭合
                    if (Vector3.SqrMagnitude(loopEnd - Cache.LoopVerts[0]) < 1e-8f)
                    {
                        closed = true;
                        break;
                    }
                }
                else break;
            }

            if (closed && Cache.LoopVerts.Count >= 3)
            {
                FillCapLoop(posMesh, negMesh, Cache.LoopVerts, plane);
            }
            enumerator.Dispose();
        }
    }

    /// <summary>
    /// 对闭合环进行三角化并填补网格
    /// </summary>
    private static void FillCapLoop(MeshBuilder posMesh, MeshBuilder negMesh, List<Vector3> loop, Plane plane)
    {
        // 构建 2D 投影坐标系
        Vector3 xaxis = Vector3.Cross(plane.normal, Vector3.up);
        if (xaxis.sqrMagnitude < 0.001f) xaxis = Vector3.Cross(plane.normal, Vector3.right);//切面本来就水平，法线与up平行
        xaxis.Normalize();//正交化1
        Vector3 yaxis = Vector3.Cross(plane.normal, xaxis).normalized;

        // 投影到 2D
        Vector2[] poly2D = new Vector2[loop.Count];
        for (int i = 0; i < loop.Count; i++)
        {
            poly2D[i] = new Vector2(Vector3.Dot(loop[i], xaxis), Vector3.Dot(loop[i], yaxis));//投影
        }

        // 耳切法三角剖分
        int[] indices = Triangulator.Triangulate(poly2D);
        Vector3 posNormal = -plane.normal;
        Vector3 negNormal = plane.normal;

        // 构建 3D Cap 网格
        for (int i = 0; i < indices.Length; i += 3)
        {
            int i1 = indices[i]; int i2 = indices[i + 1]; int i3 = indices[i + 2];
            Vector3 p1 = loop[i1]; Vector3 p2 = loop[i2]; Vector3 p3 = loop[i3];
            Vector2 u1 = poly2D[i1]; Vector2 u2 = poly2D[i2]; Vector2 u3 = poly2D[i3];

            // Positive Cap: 法线朝下，需要翻转顶点顺序
            int idx1 = posMesh.AddVertex(p1, posNormal, u1);
            int idx2 = posMesh.AddVertex(p2, posNormal, u2);
            int idx3 = posMesh.AddVertex(p3, posNormal, u3);
            posMesh.AddTriangle(0, idx1, idx3, idx2);

            // Negative Cap: 顺序保持不变
            int nIdx1 = negMesh.AddVertex(p1, negNormal, u1);
            int nIdx2 = negMesh.AddVertex(p2, negNormal, u2);
            int nIdx3 = negMesh.AddVertex(p3, negNormal, u3);
            negMesh.AddTriangle(0, nIdx1, nIdx2, nIdx3);
        }
    }

    /// <summary>
    /// 将 float 坐标量化为 int，用于字典模糊查找 (Hash Key)
    /// </summary>
    private static Vector3Int Quantize(Vector3 v)
    {
        return new Vector3Int(
            Mathf.RoundToInt(v.x * 10000f),
            Mathf.RoundToInt(v.y * 10000f),
            Mathf.RoundToInt(v.z * 10000f)
        );
    }

    #endregion
}