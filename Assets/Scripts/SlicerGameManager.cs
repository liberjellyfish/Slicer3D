using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class SlicerGameManager : MonoBehaviour
{
    #region 1. Inspector Settings

    [Header("Layer Configuration")]
    [Tooltip("指定哪些层级的物体可以被切割")]
    public LayerMask sliceableLayer;

    [Header("Separation Physics")]
    [Tooltip("切割后物体沿法线分离的距离 (米)")]
    public float separationDistance = 0.2f;

    [Tooltip("分离动画持续时间 (秒)")]
    public float separationDuration = 0.1f;

    [Header("Raycast Precision")]
    [Tooltip("射线扫描步长 (像素)：值越小越精准，但性能开销越大")]
    public float raycastStepSize = 15f;

    [Tooltip("单次射线检测的最大穿透数量 (预分配缓冲区大小)")]
    public int maxHitsPerRay = 32;

    #endregion

    #region 2. Internal State

    private Vector3 startPoint;
    private Vector3 endPoint;
    private bool isDragging = false;
    private LineRenderer lineRenderer;

    // 预分配缓冲区，用于 Physics.RaycastNonAlloc
    private RaycastHit[] raycastBuffer;

    #endregion

    #region 3. Unity Lifecycle

    void Start()
    {
        InitializeVisuals();
        // 初始化射线检测缓冲区
        raycastBuffer = new RaycastHit[maxHitsPerRay];
    }

    void Update()
    {
        HandleInput();
    }

    #endregion

    #region 4. Input & Visualization

    private void InitializeVisuals()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        if (lineRenderer.material == null)
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void HandleInput()
    {
        // 按下：开始划线
        if (Input.GetMouseButtonDown(0))
        {
            startPoint = Input.mousePosition;
            isDragging = true;
            lineRenderer.positionCount = 2;
        }

        // 拖拽：更新视觉
        if (isDragging)
        {
            endPoint = Input.mousePosition;
            UpdateLineVisuals();
        }

        // 抬起：执行切割
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            lineRenderer.positionCount = 0;
            // 只有划线够长才算有效操作
            if (Vector3.Distance(startPoint, endPoint) > 10f)
            {
                PerformSlice();
            }
        }
    }

    private void UpdateLineVisuals()
    {
        Ray startRay = Camera.main.ScreenPointToRay(startPoint);
        Ray endRay = Camera.main.ScreenPointToRay(endPoint);
        // 在摄像机前方固定距离绘制，仅作视觉反馈
        lineRenderer.SetPosition(0, startRay.GetPoint(5f));
        lineRenderer.SetPosition(1, endRay.GetPoint(5f));
    }

    #endregion

    #region 5. Slicing Logic

    /// <summary>
    /// 执行切割流程：扫描物体 -> 计算平面 -> 切割 -> 分离
    /// </summary>
    void PerformSlice()
    {
        // 1. 计算切割平面 (基于视线)
        Ray startRay = Camera.main.ScreenPointToRay(startPoint);
        Ray endRay = Camera.main.ScreenPointToRay(endPoint);
        Vector3 vecStart = startRay.direction;
        Vector3 vecEnd = endRay.direction;

        Vector3 planeNormal = Vector3.Cross(vecStart, vecEnd).normalized;
        if (planeNormal.sqrMagnitude < 0.0001f) return;
        Plane slicePlane = new Plane(planeNormal, Camera.main.transform.position);

        // 2. 射线扫描 (Raycast Sweep) 查找目标
        HashSet<GameObject> targetsToSlice = ScanForTargets();

        // 3. 对每个目标执行切割
        foreach (GameObject target in targetsToSlice)
        {
            if (target != null)
            {
                SliceObject(target, slicePlane);
            }
        }
    }

    /// <summary>
    /// 沿划线路径发射密集射线，查找触碰到的物体
    /// </summary>
    private HashSet<GameObject> ScanForTargets()
    {
        HashSet<GameObject> targets = new HashSet<GameObject>();
        float screenDistance = Vector2.Distance(startPoint, endPoint);
        int steps = Mathf.CeilToInt(screenDistance / raycastStepSize);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 currentScreenPos = Vector2.Lerp(startPoint, endPoint, t);
            Ray r = Camera.main.ScreenPointToRay(currentScreenPos);

            // 使用 NonAlloc 版本避免 GC
            int hitCount = Physics.RaycastNonAlloc(r, raycastBuffer, 1000f, sliceableLayer);

            for (int j = 0; j < hitCount; j++)
            {
                targets.Add(raycastBuffer[j].collider.gameObject);
            }
        }
        return targets;
    }

    /// <summary>
    /// 切割单个物体并处理后续生成
    /// </summary>
    void SliceObject(GameObject target, Plane planeWorld)
    {
        MeshFilter mfComp = target.GetComponent<MeshFilter>();
        if (mfComp == null) return;

        // 转换平面到局部坐标系
        Vector3 localPoint = target.transform.InverseTransformPoint(planeWorld.normal * -planeWorld.distance);
        Vector3 localNormal = target.transform.InverseTransformDirection(planeWorld.normal).normalized;
        Plane localPlane = new Plane(localNormal, localPoint);

        // 执行核心切割算法
        MeshSlicer.SlicedHull result;
        try
        {
            result = MeshSlicer.SliceMesh(mfComp.mesh, localPlane);
        }
        catch
        {
            return; // 切割失败（如拓扑错误）则忽略
        }

        if (result.PositiveMesh.vertexCount == 0 || result.NegativeMesh.vertexCount == 0) return;

        // 创建新物体
        GameObject hullPos = CreateHull(target, result.PositiveMesh, "Pos");
        GameObject hullNeg = CreateHull(target, result.NegativeMesh, "Neg");

        // 启动分离动画
        StartCoroutine(AnimateSeparation(hullPos.transform, hullNeg.transform, planeWorld.normal));

        Destroy(target);
    }

    GameObject CreateHull(GameObject original, Mesh newMesh, string suffix)
    {
        GameObject hull = new GameObject(original.name + "_" + suffix);
        hull.transform.position = original.transform.position;
        hull.transform.rotation = original.transform.rotation;
        hull.transform.localScale = original.transform.localScale;
        hull.layer = original.layer;

        MeshFilter mf = hull.AddComponent<MeshFilter>();
        mf.mesh = newMesh;

        MeshRenderer mr = hull.AddComponent<MeshRenderer>();
        mr.materials = original.GetComponent<MeshRenderer>().materials;

        MeshCollider mc = hull.AddComponent<MeshCollider>();
        mc.sharedMesh = newMesh;
        mc.convex = true;

        Rigidbody rb = hull.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        return hull;
    }

    /// <summary>
    /// 程序化分离动画 (Ease-Out)
    /// </summary>
    IEnumerator AnimateSeparation(Transform t1, Transform t2, Vector3 normal)
    {
        float elapsed = 0f;
        Vector3 startPos1 = t1.position;
        Vector3 startPos2 = t2.position;

        Vector3 targetPos1 = startPos1 + normal * separationDistance;
        Vector3 targetPos2 = startPos2 - normal * separationDistance;

        while (elapsed < separationDuration)
        {
            if (t1 == null || t2 == null) yield break;

            float t = elapsed / separationDuration;
            t = Mathf.Sin(t * Mathf.PI * 0.5f); // Ease-Out 曲线

            t1.position = Vector3.Lerp(startPos1, targetPos1, t);
            t2.position = Vector3.Lerp(startPos2, targetPos2, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (t1 != null) t1.position = targetPos1;
        if (t2 != null) t2.position = targetPos2;
    }

    #endregion
}