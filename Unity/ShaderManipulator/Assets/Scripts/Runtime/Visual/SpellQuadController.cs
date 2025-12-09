using ShaderDuel.Visual;
using UnityEngine;

/// <summary>
/// 负责把 SpellLayerState 中的数据写入 SpellQuad 的材质，
/// 当前只处理左右手掌心的位置与可见度。
/// </summary>
[RequireComponent(typeof(Renderer))]
public sealed class SpellQuadController : MonoBehaviour
{
    [Header("Target Renderer")]
    [SerializeField]
    private Renderer targetRenderer;

    [Header("Shader Property Names")]
    [Tooltip("左右手掌心位置（0-1 屏幕空间），Vector2 / Vector4")]
    [SerializeField]
    private string leftPalmPosProperty = "_LeftPalmPos";

    [SerializeField]
    private string rightPalmPosProperty = "_RightPalmPos";

    [Tooltip("左右手掌心可见度（0=不可见,1=完全可见），float")]
    [SerializeField]
    private string leftPalmVisibleProperty = "_LeftPalmVisible";

    [SerializeField]
    private string rightPalmVisibleProperty = "_RightPalmVisible";

    private Material _material;
    private int _leftPalmPosID;
    private int _rightPalmPosID;
    private int _leftPalmVisibleID;
    private int _rightPalmVisibleID;

    private void Awake()
    {
        // 自动补 Renderer
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (targetRenderer == null)
        {
            Debug.LogError("[SpellQuadController] 找不到 Renderer，脚本将被禁用。", this);
            enabled = false;
            return;
        }

        // 使用 renderer.material 以获得当前物体专属的材质实例
        _material = targetRenderer.material;

        if (_material == null)
        {
            Debug.LogError("[SpellQuadController] Renderer 上没有材质。", this);
            enabled = false;
            return;
        }

        // 缓存 PropertyID（便于后面改名）
        _leftPalmPosID = Shader.PropertyToID(leftPalmPosProperty);
        _rightPalmPosID = Shader.PropertyToID(rightPalmPosProperty);
        _leftPalmVisibleID = Shader.PropertyToID(leftPalmVisibleProperty);
        _rightPalmVisibleID = Shader.PropertyToID(rightPalmVisibleProperty);
    }

    /// <summary>
    /// 由 VisualOrchestrator 在每帧调用，
    /// 将 SpellLayerState 映射到 shader 参数。
    /// </summary>
    public void ApplyState(in SpellLayerState state)
    {
        if (_material == null) return;

        // 位置：假定已经是 0-1 的 UV / 屏幕归一化坐标
        // Vector2 会自动转成 Vector4（z,w 为 0）
        _material.SetVector(_leftPalmPosID, state.LeftPalmPos01);
        _material.SetVector(_rightPalmPosID, state.RightPalmPos01);

        // 可见度
        _material.SetFloat(_leftPalmVisibleID, state.LeftPalmVisible01);
        _material.SetFloat(_rightPalmVisibleID, state.RightPalmVisible01);
    }
}
