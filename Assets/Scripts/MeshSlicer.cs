using UnityEngine;
using System.Collections.Generic;

public static class MeshSlicer
{
    public class SlicedHull
    {
        public Mesh PositiveMesh;
        public Mesh NegativeMesh;
    }

    // 内部类：构建Mesh的数据结构
    private class MeshBuilder
    {
        private List<Vector3> vertices = new List<Vector3>();
        private List<Vector3> normals = new List<Vector3>();
        private List<Vector2> uvs = new List<Vector2>();
        private List<List<int>> subMeshTriangles = new List<List<int>>();

        public MeshBuilder(int subMeshCount)
        {
            for (int i = 0; i < subMeshCount; i++)
                subMeshTriangles.Add(new List<int>());
        }

        public int AddVertex(Vector3 v, Vector3 n, Vector2 uv)
        {
            vertices.Add(v);
            normals.Add(n);
            uvs.Add(uv);
            return vertices.Count - 1;
        }

        public void AddTriangle(int subMeshIndex, int i1, int i2, int i3)
        {
            if (subMeshIndex >= subMeshTriangles.Count) return;
            subMeshTriangles[subMeshIndex].Add(i1);
            subMeshTriangles[subMeshIndex].Add(i2);
            subMeshTriangles[subMeshIndex].Add(i3);
        }

        public Mesh ToMesh()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.subMeshCount = subMeshTriangles.Count;
            for (int i = 0; i < subMeshTriangles.Count; i++)
            {
                mesh.SetTriangles(subMeshTriangles[i], i);
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// 核心切割入口函数
    /// </summary>
    public static SlicedHull SliceMesh(Mesh mesh, Plane plane)
    {
        int subMeshCount = mesh.subMeshCount;
        MeshBuilder posMesh = new MeshBuilder(subMeshCount);
        MeshBuilder negMesh = new MeshBuilder(subMeshCount);

        // 存储切面边缘线段，用于后续封口
        List<Edge> capEdges = new List<Edge>();

        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = mesh.uv;

        // 遍历所有子网格
        for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
        {
            int[] triangles = mesh.GetTriangles(subMeshIndex);

            // 遍历当前子网格的所有三角形
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i1 = triangles[i];
                int i2 = triangles[i + 1];
                int i3 = triangles[i + 2];

                Vector3 v1 = verts[i1]; Vector3 v2 = verts[i2]; Vector3 v3 = verts[i3];
                Vector3 n1 = normals[i1]; Vector3 n2 = normals[i2]; Vector3 n3 = normals[i3];
                Vector2 uv1 = uvs[i1]; Vector2 uv2 = uvs[i2]; Vector2 uv3 = uvs[i3];

                // 判断三个顶点相对于平面的位置（True=正面/上方，False=反面/下方）
                bool s1 = plane.GetSide(v1);
                bool s2 = plane.GetSide(v2);
                bool s3 = plane.GetSide(v3);

                // 情况1：三角形完全在平面的一侧
                if (s1 == s2 && s2 == s3)
                {
                    MeshBuilder target = s1 ? posMesh : negMesh;
                    int idx1 = target.AddVertex(v1, n1, uv1);
                    int idx2 = target.AddVertex(v2, n2, uv2);
                    int idx3 = target.AddVertex(v3, n3, uv3);
                    target.AddTriangle(subMeshIndex, idx1, idx2, idx3);
                }
                // 情况2：三角形被平面切割
                else
                {
                    // 这里的逻辑是为了将所有复杂的切割情况归一化为一种情况：
                    // 即：v1 始终是那个“孤独”的点（独自在一侧），v2 和 v3 在另一侧。
                    // 这样我们只需要写一套切割逻辑，而不是三套。

                    Vector3 rV1, rV2, rV3;
                    Vector3 rN1, rN2, rN3;
                    Vector2 rUv1, rUv2, rUv3;
                    bool rS1; // 记录孤立点在哪一侧

                    if (s1 == s3) // v2 是孤立的
                    {
                        rV1 = v2; rV2 = v3; rV3 = v1;
                        rN1 = n2; rN2 = n3; rN3 = n1;
                        rUv1 = uv2; rUv2 = uv3; rUv3 = uv1;
                        rS1 = s2;
                    }
                    else if (s1 == s2) // v3 是孤立的
                    {
                        rV1 = v3; rV2 = v1; rV3 = v2;
                        rN1 = n3; rN2 = n1; rN3 = n2;
                        rUv1 = uv3; rUv2 = uv1; rUv3 = uv2;
                        rS1 = s3;
                    }
                    else // v1 已经是孤立的
                    {
                        rV1 = v1; rV2 = v2; rV3 = v3;
                        rN1 = n1; rN2 = n2; rN3 = n3;
                        rUv1 = uv1; rUv2 = uv2; rUv3 = uv3;
                        rS1 = s1;
                    }

                    // 调用处理单个三角形切割的函数
                    CutTriangle(
                        plane, subMeshIndex,
                        rV1, rV2, rV3,
                        rN1, rN2, rN3,
                        rUv1, rUv2, rUv3,
                        rS1,
                        posMesh, negMesh, capEdges
                    );
                }
            }
        }

        // 所有面处理完毕，开始生成封口
        GenerateCaps(posMesh, negMesh, capEdges, plane);

        return new SlicedHull { PositiveMesh = posMesh.ToMesh(), NegativeMesh = negMesh.ToMesh() };
    }

    /// <summary>
    /// 处理被切割的三角形，计算交点并重新三角化
    /// </summary>
    private static void CutTriangle(
        Plane plane, int subMeshIndex,
        Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 n1, Vector3 n2, Vector3 n3,
        Vector2 uv1, Vector2 uv2, Vector2 uv3,
        bool s1, // v1 所在的侧面 (true=Pos, false=Neg)
        MeshBuilder posSide, MeshBuilder negSide,
        List<Edge> capEdges)
    {
        // 此时 v1 是孤立点，v2 和 v3 在另一侧

        // 计算 v1 到 v2 以及 v1 到 v3 的插值比例 t
        float d1 = plane.GetDistanceToPoint(v1);
        float d2 = plane.GetDistanceToPoint(v2);
        float d3 = plane.GetDistanceToPoint(v3);

        float t1 = d1 / (d1 - d2); // v1 -> v2 的交点比例
        float t2 = d1 / (d1 - d3); // v1 -> v3 的交点比例

        // 线性插值计算交点坐标、法线和UV
        Vector3 enter = Vector3.Lerp(v1, v2, t1);
        Vector3 exit = Vector3.Lerp(v1, v3, t2);
        Vector3 nEnter = Vector3.Lerp(n1, n2, t1);
        Vector3 nExit = Vector3.Lerp(n1, n3, t2);
        Vector2 uvEnter = Vector2.Lerp(uv1, uv2, t1);
        Vector2 uvExit = Vector2.Lerp(uv1, uv3, t2);

        // 1. 处理孤立点一侧 (形成一个小三角形)
        // 这一侧只有一个顶点 v1，加上两个新交点 enter 和 exit，刚好构成一个三角形
        MeshBuilder soloBuilder = s1 ? posSide : negSide;
        int iV1 = soloBuilder.AddVertex(v1, n1, uv1);
        int iEnter = soloBuilder.AddVertex(enter, nEnter, uvEnter);
        int iExit = soloBuilder.AddVertex(exit, nExit, uvExit);
        soloBuilder.AddTriangle(subMeshIndex, iV1, iEnter, iExit);

        // 2. 处理另一侧 (形成一个四边形：v2-v3-exit-enter)
        // 四边形需要拆分成两个三角形
        MeshBuilder otherBuilder = s1 ? negSide : posSide;
        int oV2 = otherBuilder.AddVertex(v2, n2, uv2);
        int oV3 = otherBuilder.AddVertex(v3, n3, uv3);
        int oEnter = otherBuilder.AddVertex(enter, nEnter, uvEnter);
        int oExit = otherBuilder.AddVertex(exit, nExit, uvExit);

        // 三角形1: v2 -> v3 -> exit
        otherBuilder.AddTriangle(subMeshIndex, oV2, oV3, oExit);
        // 三角形2: v2 -> exit -> enter
        otherBuilder.AddTriangle(subMeshIndex, oV2, oExit, oEnter);

        // 3. 记录封口边
        // 关键逻辑：我们统一按照 "Positive Mesh" 的边界方向来记录边。
        // 如果 v1 是 Pos，那么 enter->exit 就是 Pos Mesh 的边界。
        // 如果 v1 是 Neg，说明 Pos Mesh 在另一侧(四边形侧)，其边界是 exit->enter。
        if (s1)
        {
            capEdges.Add(new Edge(enter, exit));
        }
        else
        {
            capEdges.Add(new Edge(exit, enter));
        }
    }

    /// <summary>
    /// 将零散的切边连接成环，并三角化生成封口
    /// </summary>
    private static void GenerateCaps(MeshBuilder posMesh, MeshBuilder negMesh, List<Edge> edges, Plane plane)
    {
        if (edges.Count < 3) return;

        // 1. 连接边形成闭合环 (Loop Finding)
        List<List<Vector3>> loops = new List<List<Vector3>>();

        while (edges.Count > 0)
        {
            List<Vector3> loop = new List<Vector3>();
            Edge current = edges[0];
            edges.RemoveAt(0);

            loop.Add(current.Start);
            Vector3 loopEnd = current.End;

            bool closed = false;
            int safety = 0;

            // 寻找下一个连接点
            while (safety++ < 5000)
            {
                int nextIdx = -1;
                for (int i = 0; i < edges.Count; i++)
                {
                    // 允许微小的浮点误差
                    if (Vector3.SqrMagnitude(edges[i].Start - loopEnd) < 1e-10f)
                    {
                        nextIdx = i;
                        break;
                    }
                }

                if (nextIdx != -1)
                {
                    loop.Add(loopEnd);
                    loopEnd = edges[nextIdx].End;
                    edges.RemoveAt(nextIdx);

                    // 检查是否回到了起点
                    if (Vector3.SqrMagnitude(loopEnd - loop[0]) < 1e-10f)
                    {
                        closed = true;
                        break;
                    }
                }
                else
                {
                    break; // 链条断裂
                }
            }

            if (closed && loop.Count >= 3)
            {
                loops.Add(loop);
            }
        }

        // 2. 对每个闭合环进行三角化
        foreach (var loop in loops)
        {
            FillCapLoop(posMesh, negMesh, loop, plane);
        }
    }

    private static void FillCapLoop(MeshBuilder posMesh, MeshBuilder negMesh, List<Vector3> loop, Plane plane)
    {
        // 1. 构建 2D 坐标系 (用于投影 3D 点到 2D 平面)
        Vector3 xaxis = Vector3.Cross(plane.normal, Vector3.up);
        if (xaxis.sqrMagnitude < 0.001f) xaxis = Vector3.Cross(plane.normal, Vector3.right);
        xaxis.Normalize();
        Vector3 yaxis = Vector3.Cross(plane.normal, xaxis).normalized;

        // 2. 投影顶点
        Vector2[] poly2D = new Vector2[loop.Count];
        for (int i = 0; i < loop.Count; i++)
        {
            poly2D[i] = new Vector2(
                Vector3.Dot(loop[i], xaxis),
                Vector3.Dot(loop[i], yaxis)
            );
        }

        // 3. 执行三角剖分 (Ear Clipping)
        int[] indices = Triangulator.Triangulate(poly2D);

        // 4. 构建 3D Mesh
        Vector3 posNormal = -plane.normal; // Positive Cap 法线朝外 (即平面的反方向)
        Vector3 negNormal = plane.normal;  // Negative Cap 法线朝外 (即平面的正方向)

        for (int i = 0; i < indices.Length; i += 3)
        {
            int i1 = indices[i];
            int i2 = indices[i + 1];
            int i3 = indices[i + 2];

            Vector3 p1 = loop[i1];
            Vector3 p2 = loop[i2];
            Vector3 p3 = loop[i3];

            Vector2 u1 = poly2D[i1];
            Vector2 u2 = poly2D[i2];
            Vector2 u3 = poly2D[i3];

            // Positive Cap: 由于视角原因需要翻转顶点顺序
            int idx1 = posMesh.AddVertex(p1, posNormal, u1);
            int idx2 = posMesh.AddVertex(p2, posNormal, u2);
            int idx3 = posMesh.AddVertex(p3, posNormal, u3);
            posMesh.AddTriangle(0, idx1, idx3, idx2); // 注意顺序: 1-3-2

            // Negative Cap: 顺序正常
            int nIdx1 = negMesh.AddVertex(p1, negNormal, u1);
            int nIdx2 = negMesh.AddVertex(p2, negNormal, u2);
            int nIdx3 = negMesh.AddVertex(p3, negNormal, u3);
            negMesh.AddTriangle(0, nIdx1, nIdx2, nIdx3); // 注意顺序: 1-2-3
        }
    }

    private struct Edge { public Vector3 Start, End; public Edge(Vector3 s, Vector3 e) { Start = s; End = e; } }
}