using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 网格分离器
/// <para>职责：检测网格中的连通分量，将不相连的部分拆分为独立的 Mesh。</para>
/// <para>算法：使用并查集 (Union-Find) + 空间哈希 (Spatial Hashing) 实现高效分离。</para>
/// </summary>
public static class MeshSeparator
{
    /// <summary>
    /// 将一个可能包含多个独立部分的 Mesh 分离为多个 Mesh
    /// </summary>
    public static List<Mesh> Separate(Mesh sourceMesh)
    {
        if (sourceMesh == null || sourceMesh.vertexCount == 0) return new List<Mesh>();

        int[] triangles = sourceMesh.triangles;
        Vector3[] vertices = sourceMesh.vertices;
        Vector3[] normals = sourceMesh.normals;
        Vector2[] uvs = sourceMesh.uv;

        // 三角形总数
        int triCount = triangles.Length / 3;

        // 1. 初始化并查集
        UnionFind uf = new UnionFind(triCount);

        // 2. 空间哈希表：记录每个位置第一次出现的三角形 ID
        // Key: 量化后的坐标 (实现顶点焊接，防止切面脱落)
        // Value: 属于该坐标的某个三角形索引
        Dictionary<Vector3Int, int> positionToTriangleMap = new Dictionary<Vector3Int, int>(vertices.Length);

        // 3. 遍历所有三角形，构建连通关系
        for (int i = 0; i < triCount; i++)
        {
            // 获取三角形的三个顶点索引
            int i1 = triangles[i * 3];
            int i2 = triangles[i * 3 + 1];
            int i3 = triangles[i * 3 + 2];

            // 尝试通过顶点位置 Union 三角形
            UnionByPosition(i, vertices[i1], uf, positionToTriangleMap);
            UnionByPosition(i, vertices[i2], uf, positionToTriangleMap);
            UnionByPosition(i, vertices[i3], uf, positionToTriangleMap);
        }

        // 4. 根据 Root 分组三角形
        // Key: Root ID, Value: 该组的所有三角形索引
        Dictionary<int, List<int>> groups = new Dictionary<int, List<int>>();

        for (int i = 0; i < triCount; i++)
        {
            int root = uf.Find(i);
            if (!groups.ContainsKey(root))
            {
                groups[root] = new List<int>();
            }
            // 存储的是三角形在原数组中的起始偏移 (i * 3)
            groups[root].Add(i * 3);
        }

        // 如果只有一个组，说明没有分离，直接返回原 Mesh (拷贝一份以防后续修改)
        if (groups.Count <= 1)
        {
            return new List<Mesh> { InstantiateMesh(sourceMesh) };
        }

        // 5. 构建分离后的 Mesh
        List<Mesh> separatedMeshes = new List<Mesh>(groups.Count);
        foreach (var group in groups.Values)
        {
            separatedMeshes.Add(BuildSubMesh(group, vertices, normals, uvs, triangles));
        }

        return separatedMeshes;
    }

    private static void UnionByPosition(int triIdx, Vector3 pos, UnionFind uf, Dictionary<Vector3Int, int> map)
    {
        Vector3Int key = Quantize(pos);
        if (map.TryGetValue(key, out int neighborTriIdx))
        {
            // 如果这个位置之前被其他三角形用过，说明这两个三角形是连通的
            uf.Union(triIdx, neighborTriIdx);
        }
        else
        {
            // 记录该位置归属于当前三角形
            map[key] = triIdx;
        }
    }

    private static Mesh BuildSubMesh(List<int> triIndices, Vector3[] orgVerts, Vector3[] orgNormals, Vector2[] orgUvs, int[] orgTriangles)
    {
        Mesh newMesh = new Mesh();

        // 映射表：旧顶点索引 -> 新顶点索引
        Dictionary<int, int> oldToNewVertexMap = new Dictionary<int, int>();
        List<Vector3> newVerts = new List<Vector3>();
        List<Vector3> newNormals = new List<Vector3>();
        List<Vector2> newUvs = new List<Vector2>();
        List<int> newTriangles = new List<int>();

        foreach (int triStart in triIndices)
        {
            for (int k = 0; k < 3; k++)
            {
                int oldVertIdx = orgTriangles[triStart + k];

                if (!oldToNewVertexMap.ContainsKey(oldVertIdx))
                {
                    oldToNewVertexMap[oldVertIdx] = newVerts.Count;
                    newVerts.Add(orgVerts[oldVertIdx]);

                    if (orgNormals.Length > 0) newNormals.Add(orgNormals[oldVertIdx]);
                    if (orgUvs.Length > 0) newUvs.Add(orgUvs[oldVertIdx]);
                }

                newTriangles.Add(oldToNewVertexMap[oldVertIdx]);
            }
        }

        newMesh.SetVertices(newVerts);
        newMesh.SetNormals(newNormals);
        newMesh.SetUVs(0, newUvs);
        newMesh.SetTriangles(newTriangles, 0);
        newMesh.RecalculateBounds();

        return newMesh;
    }

    private static Mesh InstantiateMesh(Mesh source)
    {
        return Object.Instantiate(source);
    }

    private static Vector3Int Quantize(Vector3 v)
    {
        // 精度控制：10000 对应 0.0001 米
        return new Vector3Int(
            Mathf.RoundToInt(v.x * 10000f),
            Mathf.RoundToInt(v.y * 10000f),
            Mathf.RoundToInt(v.z * 10000f)
        );
    }

    // --- 并查集内部类 ---
    private class UnionFind
    {
        private int[] parent;
        private int[] rank;

        public UnionFind(int size)
        {
            parent = new int[size];
            rank = new int[size];
            for (int i = 0; i < size; i++)
            {
                parent[i] = i;
                rank[i] = 0;
            }
        }

        public int Find(int i)
        {
            if (parent[i] != i)
            {
                parent[i] = Find(parent[i]); // 路径压缩
            }
            return parent[i];
        }

        public void Union(int i, int j)
        {
            int rootI = Find(i);
            int rootJ = Find(j);

            if (rootI != rootJ)
            {
                // 按秩合并
                if (rank[rootI] < rank[rootJ])
                {
                    parent[rootI] = rootJ;
                }
                else if (rank[rootI] > rank[rootJ])
                {
                    parent[rootJ] = rootI;
                }
                else
                {
                    parent[rootI] = rootJ;
                    rank[rootJ]++;
                }
            }
        }
    }
}