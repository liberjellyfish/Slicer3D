using UnityEngine;

/// <summary>
/// 鼠标输入控制器
/// 处理画线输入、可视化渲染以及发起切割请求
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class MouseSlicer : MonoBehaviour
{
    [Header("检测配置")]
    [Tooltip("只有该层级的物体会被切割")]
    public LayerMask SliceableLayer;
    [Tooltip("最小有效划线长度 (屏幕像素)")]
    public float MinSliceDistance = 10f;

    [Header("视觉配置")]
    [Tooltip("切割线距离摄像机的深度")]
    public float VisualDepth = 5f;
    public Color LineColor = Color.red;
    public float LineWidth = 0.05f;

    private Vector3 _startMousePos;
    private bool _isDragging = false;
    private LineRenderer _lineRenderer;
    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
        InitializeLineRenderer();
    }

    private void InitializeLineRenderer()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = 2;
        _lineRenderer.enabled = false;

        // 设置材质与外观
        _lineRenderer.startWidth = LineWidth;
        _lineRenderer.endWidth = LineWidth;

        // 使用内置材质避免粉色方块
        if (_lineRenderer.material == null || _lineRenderer.material.name.StartsWith("Default-Material"))
        {
            _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        _lineRenderer.startColor = LineColor;
        _lineRenderer.endColor = LineColor;
        _lineRenderer.numCapVertices = 4; // 圆角端点
    }

    private void Update()
    {
        // 1. 开始画线
        if (Input.GetMouseButtonDown(0))
        {
            _startMousePos = Input.mousePosition;
            _isDragging = true;
            _lineRenderer.enabled = true;
            UpdateLineVisual();
        }
        // 2. 拖拽中
        else if (Input.GetMouseButton(0) && _isDragging)
        {
            UpdateLineVisual();
        }
        // 3. 释放并切割
        else if (Input.GetMouseButtonUp(0) && _isDragging)
        {
            _isDragging = false;
            _lineRenderer.enabled = false;

            Vector3 endMousePos = Input.mousePosition;
            PerformSlice(_startMousePos, endMousePos);
        }
    }

    /// <summary>
    /// 更新 LineRenderer 的端点位置
    /// </summary>
    private void UpdateLineVisual()
    {
        Vector3 currentMousePos = Input.mousePosition;

        // 将屏幕坐标转换为世界坐标 (固定深度)
        Vector3 startWorld = GetWorldPosAtDepth(_startMousePos, VisualDepth);
        Vector3 endWorld = GetWorldPosAtDepth(currentMousePos, VisualDepth);

        _lineRenderer.SetPosition(0, startWorld);
        _lineRenderer.SetPosition(1, endWorld);
    }

    private Vector3 GetWorldPosAtDepth(Vector3 screenPos, float depth)
    {
        Ray ray = _mainCamera.ScreenPointToRay(screenPos);
        return ray.GetPoint(depth);
    }

    private void PerformSlice(Vector3 startScreen, Vector3 endScreen)
    {
        // 距离校验
        if (Vector3.Distance(startScreen, endScreen) < MinSliceDistance) return;

        // 构建切割平面
        Plane slicePlane = CalculateSlicePlane(startScreen, endScreen);

        // 查找目标
        // Unity 6 / 2023+ API，若旧版本报错请改回 FindObjectsOfType<Sliceable>()
        Sliceable[] targets = FindObjectsByType<Sliceable>(FindObjectsSortMode.None);

        foreach (Sliceable target in targets)
        {
            // Layer 掩码校验
            if (((1 << target.gameObject.layer) & SliceableLayer) != 0)
            {
                Slicer.Slice(target.gameObject, slicePlane);
            }
        }
    }

    /// <summary>
    /// 根据屏幕划线构建世界空间平面
    /// 平面由三个点确定：摄像机位置、划线起点、划线终点
    /// </summary>
    private Plane CalculateSlicePlane(Vector3 startScreen, Vector3 endScreen)
    {
        Ray startRay = _mainCamera.ScreenPointToRay(startScreen);
        Ray endRay = _mainCamera.ScreenPointToRay(endScreen);

        // 在射线上取两个深度的点
        Vector3 p1 = startRay.GetPoint(10.0f);
        Vector3 p2 = endRay.GetPoint(10.0f);
        Vector3 camPos = _mainCamera.transform.position;

        // 构建平面
        // 注意点顺序决定法线方向，这里使用 (Camera, Start, End)
        return new Plane(camPos, p1, p2);
    }
}