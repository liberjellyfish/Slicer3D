using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 切割核心逻辑控制器
/// <para>职责：接收输入指令，执行射线扫描，调用 MeshSlicer 算法，管理物体生成。</para>
/// </summary>
[RequireComponent(typeof(SliceInputHandler))]
public class SliceController : MonoBehaviour
{
    #region Inspector Settings

    [Header("Layer Configuration")]
    [Tooltip("指定哪些层级的物体可以被切割")]
    public LayerMask sliceableLayer;

    [Header("Visuals & Materials")]
    [Tooltip("切割面的材质（可选）。如果不填，将复用原物体的材质")]
    public Material crossSectionMaterial;

    [Header("Physics Settings")]
    [Tooltip("分离距离 (米)")]
    public float separationDistance = 0.2f;

    [Tooltip("分离动画时长 (秒)")]
    public float separationDuration = 0.1f;

    [Header("Optimization Settings")]
    [Tooltip("射线扫描精度 (像素)")]
    public float raycastStepSize = 15f;

    [Tooltip("射线检测缓冲区大小")]
    public int maxHitsPerRay = 32;

    [Header("Debug")]
    [Tooltip("在 Scene 窗口显示射线扫描的轨迹")]
    public bool showDebugGizmos = true;
    public Color debugGizmoColor = Color.red;

    #endregion

    #region Internal State

    private SliceInputHandler inputHandler;
    private RaycastHit[] raycastBuffer;

    // 用于 Debug 绘制
    private Vector3 debugStartPoint;
    private Vector3 debugEndPoint;
    private bool hasDebugData = false;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        inputHandler = GetComponent<SliceInputHandler>();
        raycastBuffer = new RaycastHit[maxHitsPerRay];

        RegisterEvents();
    }

    void OnDestroy()
    {
        UnregisterEvents();
    }

    // 在 Scene 窗口绘制调试信息，解决“看不见”的问题
    void OnDrawGizmos()
    {
        if (showDebugGizmos && hasDebugData)
        {
            Gizmos.color = debugGizmoColor;
            Gizmos.DrawLine(debugStartPoint, debugEndPoint);
            // 画出扫描范围的示意框
            Gizmos.DrawWireSphere(debugStartPoint, 0.1f);
            Gizmos.DrawWireSphere(debugEndPoint, 0.1f);
        }
    }

    #endregion

    #region Event Management

    private void RegisterEvents()
    {
        if (inputHandler != null)
            inputHandler.OnSliceEnd += PerformSlice;
    }

    private void UnregisterEvents()
    {
        if (inputHandler != null)
            inputHandler.OnSliceEnd -= PerformSlice;
    }

    #endregion

    #region Core Slicing Logic

    /// <summary>
    /// 响应切割结束事件，执行核心逻辑
    /// </summary>
    private void PerformSlice(Vector2 startScreen, Vector2 endScreen)
    {
        // 记录调试数据
        if (showDebugGizmos)
        {
            Ray r1 = Camera.main.ScreenPointToRay(startScreen);
            Ray r2 = Camera.main.ScreenPointToRay(endScreen);
            debugStartPoint = r1.GetPoint(2f);
            debugEndPoint = r2.GetPoint(2f);
            hasDebugData = true;
        }

        // 1. 计算世界空间的切割平面
        Plane slicePlane;
        if (!CalculateSlicePlane(startScreen, endScreen, out slicePlane)) return;

        // 2. 射线扫描：寻找所有被划线击中的物体 (所见即所得)
        HashSet<GameObject> targets = ScanForTargets(startScreen, endScreen);

        // 3. 执行切割
        foreach (GameObject target in targets)
        {
            if (target != null)
            {
                ProcessSlice(target, slicePlane);
            }
        }
    }

    /// <summary>
    /// 根据屏幕划线计算世界空间切割面
    /// </summary>
    private bool CalculateSlicePlane(Vector2 startScreen, Vector2 endScreen, out Plane plane)
    {
        plane = new Plane();

        Ray startRay = Camera.main.ScreenPointToRay(startScreen);
        Ray endRay = Camera.main.ScreenPointToRay(endScreen);

        Vector3 vecStart = startRay.direction;
        Vector3 vecEnd = endRay.direction;

        Vector3 planeNormal = Vector3.Cross(vecStart, vecEnd).normalized;

        // 如果划线太短导致法线无效，返回 false
        if (planeNormal.sqrMagnitude < 0.0001f) return false;

        plane = new Plane(planeNormal, Camera.main.transform.position);
        return true;
    }

    /// <summary>
    /// 执行射线扫描 (Raycast Sweep)
    /// </summary>
    private HashSet<GameObject> ScanForTargets(Vector2 startScreen, Vector2 endScreen)
    {
        HashSet<GameObject> targets = new HashSet<GameObject>();
        float distance = Vector2.Distance(startScreen, endScreen);
        int steps = Mathf.CeilToInt(distance / raycastStepSize);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 currentPos = Vector2.Lerp(startScreen, endScreen, t);
            Ray ray = Camera.main.ScreenPointToRay(currentPos);

            // 使用 NonAlloc 避免 GC
            int hitCount = Physics.RaycastNonAlloc(ray, raycastBuffer, 1000f, sliceableLayer);

            for (int j = 0; j < hitCount; j++)
            {
                targets.Add(raycastBuffer[j].collider.gameObject);
            }
        }
        return targets;
    }

    /// <summary>
    /// 切割单个物体
    /// </summary>
    private void ProcessSlice(GameObject target, Plane planeWorld)
    {
        MeshFilter mf = target.GetComponent<MeshFilter>();
        if (mf == null) return;

        // 空间转换：将平面转到物体的局部坐标系
        Vector3 localPoint = target.transform.InverseTransformPoint(planeWorld.normal * -planeWorld.distance);
        Vector3 localNormal = target.transform.InverseTransformDirection(planeWorld.normal).normalized;
        Plane localPlane = new Plane(localNormal, localPoint);

        // 调用静态算法库
        MeshSlicer.SlicedHull result;
        try
        {
            result = MeshSlicer.SliceMesh(mf.mesh, localPlane);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Slice Failed: {e.Message}");
            return;
        }

        if (result.PositiveMesh.vertexCount == 0 || result.NegativeMesh.vertexCount == 0) return;

        // --- 核心更新：使用 MeshSeparator 处理凹多面体 ---
        // 切割结果可能包含多个不相连的部分（例如 U 型物体切底），需要分离
        List<Mesh> posMeshes = MeshSeparator.Separate(result.PositiveMesh);
        List<Mesh> negMeshes = MeshSeparator.Separate(result.NegativeMesh);

        // 生成 Positive 侧的所有物体
        foreach (var mesh in posMeshes)
        {
            GameObject hull = CreateHull(target, mesh, "Pos");
            // 沿法线方向移动
            StartCoroutine(AnimateSeparationSingle(hull.transform, planeWorld.normal));
        }

        // 生成 Negative 侧的所有物体
        foreach (var mesh in negMeshes)
        {
            GameObject hull = CreateHull(target, mesh, "Neg");
            // 沿法线反方向移动
            StartCoroutine(AnimateSeparationSingle(hull.transform, -planeWorld.normal));
        }

        // 销毁原物体
        Destroy(target);
    }

    #endregion

    #region Object Generation & Animation

    private GameObject CreateHull(GameObject original, Mesh newMesh, string suffix)
    {
        GameObject hull = new GameObject(original.name + "_" + suffix);

        // 保持变换一致
        hull.transform.position = original.transform.position;
        hull.transform.rotation = original.transform.rotation;
        hull.transform.localScale = original.transform.localScale;
        hull.layer = original.layer;

        // 添加组件
        MeshFilter mf = hull.AddComponent<MeshFilter>();
        mf.mesh = newMesh;

        MeshRenderer mr = hull.AddComponent<MeshRenderer>();
        // 获取原材质列表
        Material[] originalMats = original.GetComponent<MeshRenderer>().materials;

        // 智能材质分配逻辑：修复“紫色平面”问题
        if (newMesh.subMeshCount > originalMats.Length)
        {
            Material[] newMats = new Material[newMesh.subMeshCount];
            for (int i = 0; i < originalMats.Length; i++)
            {
                newMats[i] = originalMats[i];
            }
            Material capMat = crossSectionMaterial != null ? crossSectionMaterial : originalMats[0];
            for (int i = originalMats.Length; i < newMats.Length; i++)
            {
                newMats[i] = capMat;
            }
            mr.materials = newMats;
        }
        else
        {
            mr.materials = originalMats;
        }

        MeshCollider mc = hull.AddComponent<MeshCollider>();
        mc.sharedMesh = newMesh;
        mc.convex = true; // 必须开启 Convex 才能支持 Rigidbody 碰撞

        Rigidbody rb = hull.AddComponent<Rigidbody>();
        rb.isKinematic = true;  // 接管物理
        rb.useGravity = false;

        return hull;
    }

    // 重构：只处理单个物体的移动动画
    private IEnumerator AnimateSeparationSingle(Transform t, Vector3 direction)
    {
        float elapsed = 0f;
        Vector3 pStart = t.position;
        Vector3 pEnd = pStart + direction * separationDistance;

        while (elapsed < separationDuration)
        {
            // 防御性检查
            if (t == null) yield break;

            float r = elapsed / separationDuration;
            r = Mathf.Sin(r * Mathf.PI * 0.5f); // Ease-Out

            t.position = Vector3.Lerp(pStart, pEnd, r);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (t != null) t.position = pEnd;
    }

    #endregion
}