using UnityEngine;

/// <summary>
/// 切割标记组件
/// 挂载此组件的物体才能被系统识别并切割
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Sliceable : MonoBehaviour
{
    [Header("切割属性")]
    [Tooltip("封口材质。若留空，则使用物体原始材质")]
    public Material CapMaterial;

    [Tooltip("是否为实心物体。实心物体切割后会生成封口面，空心物体只会生成外壳")]
    public bool IsSolid = true;
}