using UnityEngine;

/// <summary>
/// 切割视觉表现控制器
/// <para>职责：订阅输入事件，控制 LineRenderer 或其他特效的显示。</para>
/// </summary>
[RequireComponent(typeof(SliceInputHandler))]
[RequireComponent(typeof(LineRenderer))]
public class SliceVisualizer : MonoBehaviour
{
    #region Settings

    [Header("Visual Settings")]
    [Tooltip("切割线在摄像机前方的距离 (米)")]
    public float visualDistance = 5f;

    [Tooltip("线条宽度")]
    public float lineWidth = 0.03f;

    #endregion

    #region References

    private LineRenderer lineRenderer;
    private SliceInputHandler inputHandler;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        InitializeComponents();
        RegisterEvents();
    }

    void OnDestroy()
    {
        UnregisterEvents();
    }

    #endregion

    #region Initialization & Events

    private void InitializeComponents()
    {
        inputHandler = GetComponent<SliceInputHandler>();
        lineRenderer = GetComponent<LineRenderer>();

        // 初始化 LineRenderer 样式
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        // 确保有一个默认材质
        if (lineRenderer.material == null)
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void RegisterEvents()
    {
        inputHandler.OnSliceStart += HandleSliceStart;
        inputHandler.OnSliceDrag += HandleSliceDrag;
        inputHandler.OnSliceEnd += HandleSliceEnd;
    }

    private void UnregisterEvents()
    {
        if (inputHandler != null)
        {
            inputHandler.OnSliceStart -= HandleSliceStart;
            inputHandler.OnSliceDrag -= HandleSliceDrag;
            inputHandler.OnSliceEnd -= HandleSliceEnd;
        }
    }

    #endregion

    #region Event Handlers

    private void HandleSliceStart(Vector2 screenPos)
    {
        // 重置线条，准备开始新的绘制
        lineRenderer.positionCount = 2;
        Vector3 worldPos = ScreenToWorldPoint(screenPos);
        lineRenderer.SetPosition(0, worldPos);
        lineRenderer.SetPosition(1, worldPos);
    }

    private void HandleSliceDrag(Vector2 screenPos)
    {
        // 更新线条终点
        if (lineRenderer.positionCount >= 2)
        {
            Vector3 worldPos = ScreenToWorldPoint(screenPos);
            lineRenderer.SetPosition(1, worldPos);
        }
    }

    private void HandleSliceEnd(Vector2 startScreen, Vector2 endScreen)
    {
        // 切割结束，隐藏线条
        // (也可以在这里播放一个“刀光消失”的粒子特效)
        lineRenderer.positionCount = 0;
    }

    #endregion

    #region Helpers

    private Vector3 ScreenToWorldPoint(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        return ray.GetPoint(visualDistance);
    }

    #endregion
}