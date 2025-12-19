using UnityEngine;
using System;

/// <summary>
/// 切割输入处理器
/// <para>职责：监听底层输入设备（鼠标/触摸），向外分发切割事件。</para>
/// <para>特点：不包含任何游戏逻辑，仅负责传递屏幕坐标数据。</para>
/// </summary>
public class SliceInputHandler : MonoBehaviour
{
    #region Events (事件定义)

    // 使用 Action 委托作为轻量级事件
    // Vector2 参数代表屏幕坐标 (Screen Position)
    public event Action<Vector2> OnSliceStart;
    public event Action<Vector2> OnSliceDrag;
    public event Action<Vector2, Vector2> OnSliceEnd;

    #endregion

    #region Internal State

    private Vector2 startPosition;
    private bool isSlicing = false;

    // 最小有效切割距离（防止误触）
    private const float MinSliceDistance = 10f;

    #endregion

    #region Unity Lifecycle

    void Update()
    {
        HandleMouseInput();
    }

    #endregion

    #region Input Logic

    private void HandleMouseInput()
    {
        // 1. 按下：记录起点，广播开始事件
        if (Input.GetMouseButtonDown(0))
        {
            isSlicing = true;
            startPosition = Input.mousePosition;
            OnSliceStart?.Invoke(startPosition);
        }

        // 2. 拖拽：持续广播当前位置
        if (isSlicing)
        {
            OnSliceDrag?.Invoke(Input.mousePosition);
        }

        // 3. 抬起：判断有效性，广播结束事件
        if (Input.GetMouseButtonUp(0) && isSlicing)
        {
            isSlicing = false;
            Vector2 endPosition = Input.mousePosition;

            // 只有划线距离足够长，才视为有效切割
            if (Vector2.Distance(startPosition, endPosition) > MinSliceDistance)
            {
                OnSliceEnd?.Invoke(startPosition, endPosition);
            }
            else
            {
                // 如果距离太短，视为取消，可以广播一个 Reset 事件（此处复用 Start 或清空逻辑）
                OnSliceStart?.Invoke(startPosition); // 简单重置视觉
            }
        }
    }

    #endregion
}