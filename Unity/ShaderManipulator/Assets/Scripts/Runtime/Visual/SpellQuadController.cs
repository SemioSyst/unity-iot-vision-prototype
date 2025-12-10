using ShaderDuel.Visual;
using UnityEngine;

/// <summary>
/// 负责把 SpellLayerState 中的数据写入 SpellQuad 的材质。
/// 当前包括：
/// - 左右手掌心位置与可见度；
/// - 能量墙几何（中心/尺寸）与激活度 + 阶段；
/// - 蓄力光炮（几何 / 阶段 / 蓄力）；
/// - Boss 攻击对护盾的加亮因子；
/// - 当前是否处于 Guard 状态。
/// </summary>
[RequireComponent(typeof(Renderer))]
public sealed class SpellQuadController : MonoBehaviour
{
    [Header("Target Renderer")]
    [SerializeField]
    private Renderer targetRenderer;

    #region Hand Palms

    [Header("Hand Palm Shader Properties")]
    [Tooltip("左手掌心位置（0-1 屏幕空间），Vector2/Vector4")]
    [SerializeField]
    private string leftPalmPosProperty = "_LeftPalmPos";

    [Tooltip("右手掌心位置（0-1 屏幕空间），Vector2/Vector4")]
    [SerializeField]
    private string rightPalmPosProperty = "_RightPalmPos";

    [Tooltip("左手掌心可见度（0~1），float")]
    [SerializeField]
    private string leftPalmVisibleProperty = "_LeftPalmVisible";

    [Tooltip("右手掌心可见度（0~1），float")]
    [SerializeField]
    private string rightPalmVisibleProperty = "_RightPalmVisible";

    #endregion

    #region Energy Wall

    [Header("Energy Wall Shader Properties")]
    [Tooltip("是否有能量墙（0/1），float")]
    [SerializeField]
    private string hasEnergyWallProperty = "_HasEnergyWall";

    [Tooltip("能量墙中心 UV（0-1），Vector2/Vector4")]
    [SerializeField]
    private string wallCenterUVProperty = "_WallCenterUV";

    [Tooltip("能量墙尺寸 UV（宽高 0-1），Vector2/Vector4")]
    [SerializeField]
    private string wallSizeUVProperty = "_WallSizeUV";

    [Tooltip("能量墙基础激活度/可见度（0-1），float")]
    [SerializeField]
    private string wallActivationProperty = "_WallActivation01";

    [Tooltip("能量墙当前阶段（整数），float/int")]
    [SerializeField]
    private string wallPhaseProperty = "_WallPhase";

    [Tooltip("能量墙当前阶段进度（0-1），float")]
    [SerializeField]
    private string wallPhaseProgressProperty = "_WallPhaseProgress01";

    #endregion

    #region Charge Beam

    [Header("Charge Beam Shader Properties")]
    [Tooltip("当前是否存在光炮（0/1），float")]
    [SerializeField]
    private string hasChargeBeamProperty = "_HasChargeBeam";

    [Tooltip("光炮起点 UV（0-1），Vector2/Vector4")]
    [SerializeField]
    private string beamOriginUVProperty = "_BeamOriginUV";

    [Tooltip("光炮尺寸 UV（x = 宽度, y = 长度），Vector2/Vector4")]
    [SerializeField]
    private string beamSizeUVProperty = "_BeamSizeUV";

    [Tooltip("光炮整体可见度 / 强度（0-1），float")]
    [SerializeField]
    private string beamActivationProperty = "_BeamActivation01";

    [Tooltip("光炮当前阶段（整数），float/int")]
    [SerializeField]
    private string beamPhaseProperty = "_BeamPhase";

    [Tooltip("光炮当前阶段进度（0-1），float")]
    [SerializeField]
    private string beamPhaseProgressProperty = "_BeamPhaseProgress01";

    [Tooltip("光炮蓄力进度（0-1），float")]
    [SerializeField]
    private string beamChargingProgressProperty = "_BeamChargingProgress01";

    #endregion

    #region Boss / Guard

    [Header("Boss Interaction Shader Properties")]
    [Tooltip("Boss 攻击对护盾的加亮因子（0-1），float")]
    [SerializeField]
    private string shieldBoostByBossAttackProperty = "_ShieldBoostByBossAttack01";

    [Tooltip("当前是否 Guarding（0/1），float")]
    [SerializeField]
    private string guardedProperty = "_Guarded";

    #endregion

    // 材质与属性 ID 缓存
    private Material _material;

    private int _leftPalmPosID;
    private int _rightPalmPosID;
    private int _leftPalmVisibleID;
    private int _rightPalmVisibleID;

    private int _hasEnergyWallID;
    private int _wallCenterUVID;
    private int _wallSizeUVID;
    private int _wallActivationID;
    private int _wallPhaseID;
    private int _wallPhaseProgressID;

    private int _hasChargeBeamID;
    private int _beamOriginUVID;
    private int _beamSizeUVID;
    private int _beamActivationID;
    private int _beamPhaseID;
    private int _beamPhaseProgressID;
    private int _beamChargingProgressID;

    private int _shieldBoostByBossAttackID;
    private int _guardedID;

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

        // —— 手心 —— 
        _leftPalmPosID = Shader.PropertyToID(leftPalmPosProperty);
        _rightPalmPosID = Shader.PropertyToID(rightPalmPosProperty);
        _leftPalmVisibleID = Shader.PropertyToID(leftPalmVisibleProperty);
        _rightPalmVisibleID = Shader.PropertyToID(rightPalmVisibleProperty);

        // —— 能量墙 —— 
        _hasEnergyWallID = Shader.PropertyToID(hasEnergyWallProperty);
        _wallCenterUVID = Shader.PropertyToID(wallCenterUVProperty);
        _wallSizeUVID = Shader.PropertyToID(wallSizeUVProperty);
        _wallActivationID = Shader.PropertyToID(wallActivationProperty);
        _wallPhaseID = Shader.PropertyToID(wallPhaseProperty);
        _wallPhaseProgressID = Shader.PropertyToID(wallPhaseProgressProperty);

        // —— 光炮 —— 
        _hasChargeBeamID = Shader.PropertyToID(hasChargeBeamProperty);
        _beamOriginUVID = Shader.PropertyToID(beamOriginUVProperty);
        _beamSizeUVID = Shader.PropertyToID(beamSizeUVProperty);
        _beamActivationID = Shader.PropertyToID(beamActivationProperty);
        _beamPhaseID = Shader.PropertyToID(beamPhaseProperty);
        _beamPhaseProgressID = Shader.PropertyToID(beamPhaseProgressProperty);
        _beamChargingProgressID = Shader.PropertyToID(beamChargingProgressProperty);

        // —— Boss / Guard —— 
        _shieldBoostByBossAttackID = Shader.PropertyToID(shieldBoostByBossAttackProperty);
        _guardedID = Shader.PropertyToID(guardedProperty);
    }

    /// <summary>
    /// 由 VisualOrchestrator 在每帧调用，
    /// 将 SpellLayerState 映射到 SpellQuad 使用的 shader 参数。
    /// </summary>
    public void ApplyState(in SpellLayerState state)
    {
        if (_material == null) return;

        // ―― 手心 ―― 
        _material.SetVector(_leftPalmPosID, state.LeftPalmPos01);
        _material.SetVector(_rightPalmPosID, state.RightPalmPos01);

        _material.SetFloat(_leftPalmVisibleID, state.LeftPalmVisible01);
        _material.SetFloat(_rightPalmVisibleID, state.RightPalmVisible01);

        // ―― 能量墙 ―― 
        float hasWall01 = state.HasEnergyWall ? 1f : 0f;
        _material.SetFloat(_hasEnergyWallID, hasWall01);

        _material.SetVector(_wallCenterUVID, state.WallCenterUV);
        _material.SetVector(_wallSizeUVID, state.WallSizeUV);

        _material.SetFloat(_wallActivationID, state.WallActivation01);
        _material.SetInt(_wallPhaseID, (int)state.WallPhase);
        _material.SetFloat(_wallPhaseProgressID, state.WallPhaseProgress01);

        // ―― 光炮 ―― 
        float hasBeam01 = state.HasChargeBeam ? 1f : 0f;
        _material.SetFloat(_hasChargeBeamID, hasBeam01);

        _material.SetVector(_beamOriginUVID, state.BeamOriginUV);
        _material.SetVector(_beamSizeUVID, state.BeamSizeUV);

        _material.SetFloat(_beamActivationID, state.BeamActivation01);
        _material.SetInt(_beamPhaseID, (int)state.BeamPhase);
        _material.SetFloat(_beamPhaseProgressID, state.BeamPhaseProgress01);
        _material.SetFloat(_beamChargingProgressID, state.BeamChargingProgress01);

        // ―― Boss & 护盾 交互 ―― 
        _material.SetFloat(_shieldBoostByBossAttackID, state.ShieldBoostByBossAttack01);

        float guarded01 = state.Guarded ? 1f : 0f;
        _material.SetFloat(_guardedID, guarded01);
    }
}
