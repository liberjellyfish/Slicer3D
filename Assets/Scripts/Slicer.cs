using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 核心切割算法库
/// 负责处理网格的几何切割、顶点分类与新网格生成
/// </summary>
public static class Slicer
{
    // =================================================================================
    // 内部类：网格构建器 (MeshBuilder)
    // 负责收集顶点数据并最终生成 Unity Mesh
    // =================================================================================
    private class MeshBuilder
    {
        // 预估容量，减少 List 扩容带来的 GC
        // 假设切割后的切片大约有原网格 1/2 的体量
        private const int INITIAL_CAPACITY = 256;

        private List<Vector3> vertices;
        private List<Vector3> normals;
        private List<Vector2> uvs;
        private List<int> triangles;

        public MeshBuilder()
        {
            vertices = new List<Vector3>(INITIAL_CAPACITY);
            normals = new List<Vector3>(INITIAL_CAPACITY);
            uvs = new List<Vector2>(INITIAL_CAPACITY);
            triangles = new List<int>(INITIAL_CAPACITY * 3);
        }

        /// <summary>
        /// 添加一个完整的三角形
        /// </summary>
        public void AddTriangle(
            Vector3 v1, Vector3 v2, Vector3 v3,
            Vector3 n1, Vector3 n2, Vector3 n3,
            Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            int baseIndex = vertices.Count;

            // 添加几何数据
            vertices.Add(v1); vertices.Add(v2); vertices.Add(v3);
            normals.Add(n1); normals.Add(n2); normals.Add(n3);
            uvs.Add(uv1); uvs.Add(uv2); uvs.Add(uv3);

            // 添加拓扑索引
            triangles.Add(baseIndex);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex + 2);
        }

        /// <summary>
        /// 批量添加封口面的几何数据
        /// </summary>
        public void AddCapGeometry(Vector3[] capVerts, int[] capTris, Vector2[] capUVs, Vector3 capNormal)
        {
            int baseIndex = vertices.Count;

            // 批量添加顶点数据
            for (int i = 0; i < capVerts.Length; i++)
            {
                vertices.Add(capVerts[i]);
                normals.Add(capNormal); // 封口面通常是平的，法线一致
                uvs.Add(capUVs[i]);
            }

            // 批量添加三角形索引 (注意偏移量)
            for (int i = 0; i < capTris.Length; i++)
            {
                triangles.Add(baseIndex + capTris[i]);
            }
        }

        /// <summary>
        /// 构建最终的 Mesh 对象
        /// </summary>
        public Mesh ToMesh()
        {
            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices); // SetVertices 比 .vertices = ... 稍微快一点
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);

            mesh.RecalculateBounds();
            // mesh.RecalculateTangents(); // 如果使用法线贴图，需要开启此行
            return mesh;
        }

        public bool HasGeometry => vertices.Count > 0;
    }

    // =================================================================================
    // 公共接口
    // =================================================================================

    /// <summary>
    /// 对目标物体执行切割
    /// </summary>
    /// <param name="target">被切割的游戏物体</param>
    /// <param name="slicePlane">世界空间切割平面</param>
    public static void Slice(GameObject target, Plane slicePlane)
    {
        // 1. 数据校验与获取
        if (target == null) return;
        MeshFilter meshFilter = target.GetComponent<MeshFilter>();
        if (meshFilter == null) return;

        Mesh originalMesh = meshFilter.sharedMesh;
        if (originalMesh == null) return;

        // 2. 空间转换：世界平面 -> 局部平面
        // 这一步至关重要，因为 Mesh 数据存储在局部空间
        Plane localPlane = WorldPlaneToLocal(slicePlane, target.transform);

        // 3. 初始化构建器
        MeshBuilder positiveBuilder = new MeshBuilder(); // 平面上方
        MeshBuilder negativeBuilder = new MeshBuilder(); // 平面下方
        List<Vector3> intersectionPoints = new List<Vector3>(); // 记录切面交点

        // 4. 执行几何运算
        // 提取原始网格数据缓存，避免循环中重复访问属性产生 GC
        MeshData originalData = new MeshData(originalMesh);

        PerformMeshSlicing(originalData, localPlane, positiveBuilder, negativeBuilder, intersectionPoints);

        // 5. 生成封口
        Sliceable sliceable = target.GetComponent<Sliceable>();
        if (sliceable != null && sliceable.IsSolid)
        {
            GenerateCap(positiveBuilder, negativeBuilder, intersectionPoints, localPlane);
        }

        // 6. 实例化结果
        if (positiveBuilder.HasGeometry && negativeBuilder.HasGeometry)
        {
            CreatePiece(target, positiveBuilder, "Positive");
            CreatePiece(target, negativeBuilder, "Negative");

            // 销毁旧物体
            Object.Destroy(target);
        }
        else
        {
            // Debug.Log("未产生有效切割（物体未跨越平面）");
        }
    }

    // =================================================================================
    // 核心几何逻辑
    // =================================================================================

    // 简单的结构体用于缓存 Mesh 数据，减少 API 调用开销
    private struct MeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public int[] Triangles;

        public MeshData(Mesh mesh)
        {
            Vertices = mesh.vertices;
            Normals = mesh.normals;
            UVs = mesh.uv;
            Triangles = mesh.triangles;
        }
    }

    private static void PerformMeshSlicing(MeshData meshData, Plane plane, MeshBuilder posBuilder, MeshBuilder negBuilder, List<Vector3> intersectionPoints)
    {
        int triangleCount = meshData.Triangles.Length;

        // 遍历每个三角形 (步长为3)
        for (int i = 0; i < triangleCount; i += 3)
        {
            // 获取三角形索引
            int idx1 = meshData.Triangles[i];
            int idx2 = meshData.Triangles[i + 1];
            int idx3 = meshData.Triangles[i + 2];

            // 获取顶点数据
            Vector3 v1 = meshData.Vertices[idx1];
            Vector3 v2 = meshData.Vertices[idx2];
            Vector3 v3 = meshData.Vertices[idx3];

            // 判断点在平面哪一侧 (GetSide: true=上方, false=下方)
            bool side1 = plane.GetSide(v1);
            bool side2 = plane.GetSide(v2);
            bool side3 = plane.GetSide(v3);

            // 情况 A: 所有点都在正向 -> 直接保留到正向 Mesh
            if (side1 && side2 && side3)
            {
                posBuilder.AddTriangle(
                    v1, v2, v3,
                    meshData.Normals[idx1], meshData.Normals[idx2], meshData.Normals[idx3],
                    meshData.UVs[idx1], meshData.UVs[idx2], meshData.UVs[idx3]
                );
            }
            // 情况 B: 所有点都在负向 -> 直接保留到负向 Mesh
            else if (!side1 && !side2 && !side3)
            {
                negBuilder.AddTriangle(
                    v1, v2, v3,
                    meshData.Normals[idx1], meshData.Normals[idx2], meshData.Normals[idx3],
                    meshData.UVs[idx1], meshData.UVs[idx2], meshData.UVs[idx3]
                );
            }
            // 情况 C: 跨越平面 -> 需要裁剪三角形
            else
            {
                SliceTriangle(
                    plane,
                    v1, v2, v3,
                    meshData.Normals[idx1], meshData.Normals[idx2], meshData.Normals[idx3],
                    meshData.UVs[idx1], meshData.UVs[idx2], meshData.UVs[idx3],
                    side1, side2, side3,
                    posBuilder, negBuilder, intersectionPoints
                );
            }
        }
    }

    /// <summary>
    /// 处理单个被切割的三角形
    /// 将其分解为 3 个新的小三角形，并分配给对应的侧
    /// </summary>
    private static void SliceTriangle(
        Plane plane,
        Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 n1, Vector3 n2, Vector3 n3,
        Vector2 uv1, Vector2 uv2, Vector2 uv3,
        bool s1, bool s2, bool s3,
        MeshBuilder posBuilder, MeshBuilder negBuilder,
        List<Vector3> intersectionPoints)
    {
        // 1. 重新排序顶点
        // 目标：将唯一的那个点（孤立点）放到 index 0
        Vector3[] v = { v1, v2, v3 };
        Vector3[] n = { n1, n2, n3 };
        Vector2[] uv = { uv1, uv2, uv3 };
        bool[] s = { s1, s2, s3 };

        int isoIndex; // 孤立点索引
        if (s[0] == s[1]) isoIndex = 2;       // 0,1同侧，2孤立
        else if (s[0] == s[2]) isoIndex = 1;  // 0,2同侧，1孤立
        else isoIndex = 0;                    // 1,2同侧，0孤立

        // 获取排序后的数据引用
        int idxIso = isoIndex;
        int idxNext1 = (isoIndex + 1) % 3;
        int idxNext2 = (isoIndex + 2) % 3;

        Vector3 vIso = v[idxIso]; Vector3 vNext1 = v[idxNext1]; Vector3 vNext2 = v[idxNext2];
        Vector3 nIso = n[idxIso]; Vector3 nNext1 = n[idxNext1]; Vector3 nNext2 = n[idxNext2];
        Vector2 uvIso = uv[idxIso]; Vector2 uvNext1 = uv[idxNext1]; Vector2 uvNext2 = uv[idxNext2];

        // 孤立点在哪一侧？
        bool isIsoPositive = s[idxIso];

        // 2. 计算交点 (Raycast)
        // 射线方向：孤立点 -> 另外两个点
        float t1 = GetIntersectionT(plane, vIso, vNext1);
        float t2 = GetIntersectionT(plane, vIso, vNext2);

        // 得到交点坐标 (Lerp 比 ray.GetPoint 更快且容易插值属性)
        Vector3 i1 = Vector3.Lerp(vIso, vNext1, t1);
        Vector3 i2 = Vector3.Lerp(vIso, vNext2, t2);

        // 插值计算属性
        Vector3 ni1 = Vector3.Lerp(nIso, nNext1, t1);
        Vector3 ni2 = Vector3.Lerp(nIso, nNext2, t2);
        Vector2 uvi1 = Vector2.Lerp(uvIso, uvNext1, t1);
        Vector2 uvi2 = Vector2.Lerp(uvIso, uvNext2, t2);

        // 3. 分配三角形
        MeshBuilder isoSideBuilder = isIsoPositive ? posBuilder : negBuilder;
        MeshBuilder otherSideBuilder = isIsoPositive ? negBuilder : posBuilder;

        // 3.1 孤立侧：1个三角形 (Tip)
        isoSideBuilder.AddTriangle(vIso, i1, i2, nIso, ni1, ni2, uvIso, uvi1, uvi2);

        // 3.2 另一侧：一个四边形 -> 拆分为 2 个三角形
        // Tri 1: i1, vNext1, vNext2
        otherSideBuilder.AddTriangle(i1, vNext1, vNext2, ni1, nNext1, nNext2, uvi1, uvNext1, uvNext2);
        // Tri 2: i1, vNext2, i2
        otherSideBuilder.AddTriangle(i1, vNext2, i2, ni1, nNext2, ni2, uvi1, uvNext2, uvi2);

        // 4. 记录交点用于封口
        intersectionPoints.Add(i1);
        intersectionPoints.Add(i2);
    }

    // 计算线段与平面的交点比例 T (0~1)
    private static float GetIntersectionT(Plane plane, Vector3 start, Vector3 end)
    {
        Vector3 dir = end - start;
        float dist;
        // 使用 Raycast 寻找交点
        // 注意：这里假设一定相交，因为是在 CutTriangle 中调用的
        new Ray(start, dir);
        plane.Raycast(new Ray(start, dir), out dist);
        return dist / dir.magnitude;
    }

    private static void GenerateCap(MeshBuilder posBuilder, MeshBuilder negBuilder, List<Vector3> intersectionPoints, Plane localPlane)
    {
        // 调用专用的三角剖分器
        // 注意：正向面的封口法线应指向平面反方向（外部），负向面指向平面正方向
        CapTriangulator.CapMesh capMesh = CapTriangulator.GenerateCap(intersectionPoints, localPlane.normal);

        if (capMesh.Vertices == null || capMesh.Vertices.Length < 3) return;

        // 填入正向网格 (法线取反: -normal)
        posBuilder.AddCapGeometry(capMesh.Vertices, capMesh.Triangles, capMesh.UVs, -localPlane.normal);

        // 填入负向网格 (法线相同: normal)
        // 关键：为了保证从外部看可见，负向面的三角形绕序必须反转
        int[] reversedTris = new int[capMesh.Triangles.Length];
        for (int i = 0; i < capMesh.Triangles.Length; i += 3)
        {
            reversedTris[i] = capMesh.Triangles[i];
            reversedTris[i + 1] = capMesh.Triangles[i + 2]; // 交换 1,2 顺序实现反转
            reversedTris[i + 2] = capMesh.Triangles[i + 1];
        }
        negBuilder.AddCapGeometry(capMesh.Vertices, reversedTris, capMesh.UVs, localPlane.normal);
    }

    private static Plane WorldPlaneToLocal(Plane worldPlane, Transform tr)
    {
        // 转换法线 (方向)
        Vector3 localNormal = tr.InverseTransformDirection(worldPlane.normal);
        // 转换平面上的点 (位置)
        // Plane.distance 是原点到平面的带符号距离。 point = normal * -distance
        Vector3 pointOnPlane = worldPlane.normal * -worldPlane.distance;
        Vector3 localPoint = tr.InverseTransformPoint(pointOnPlane);

        return new Plane(localNormal, localPoint);
    }

    private static void CreatePiece(GameObject original, MeshBuilder meshBuilder, string suffix)
    {
        GameObject piece = new GameObject($"{original.name}_{suffix}");
        piece.transform.position = original.transform.position;
        piece.transform.rotation = original.transform.rotation;
        piece.transform.localScale = original.transform.localScale;

        // 设置网格
        Mesh newMesh = meshBuilder.ToMesh();
        piece.AddComponent<MeshFilter>().mesh = newMesh;
        piece.AddComponent<MeshRenderer>().sharedMaterial = original.GetComponent<MeshRenderer>().sharedMaterial;

        // 设置物理
        MeshCollider mc = piece.AddComponent<MeshCollider>();
        mc.sharedMesh = newMesh;
        mc.convex = true; // 必须为凸包才能模拟物理

        Rigidbody rb = piece.AddComponent<Rigidbody>();
        Rigidbody originalRb = original.GetComponent<Rigidbody>();
        if (originalRb != null)
        {
            rb.mass = originalRb.mass * 0.5f;
            rb.linearVelocity = originalRb.linearVelocity;
            rb.angularVelocity = originalRb.angularVelocity;
        }

        // 传递组件属性
        Sliceable oldSliceable = original.GetComponent<Sliceable>();
        if (oldSliceable != null)
        {
            Sliceable newSliceable = piece.AddComponent<Sliceable>();
            newSliceable.IsSolid = oldSliceable.IsSolid;
            newSliceable.CapMaterial = oldSliceable.CapMaterial;
        }

        piece.layer = original.layer;

        // 分离效果
        rb.AddExplosionForce(100f, original.transform.position, 1f);
    }
}