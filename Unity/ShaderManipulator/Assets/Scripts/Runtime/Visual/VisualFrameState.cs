using ShaderDuel.Gameplay;
using UnityEngine;

namespace ShaderDuel.Visual
{
    /// <summary>
    /// 从视觉系统各个子模块收集的每帧状态，用于决策当前帧的视觉输出
    /// </summary>
    public struct VisualFrameState
    {
        public BackgroundLayerState Background;
        public BossLayerState Boss;
        public SpellLayerState Spell;
        public OverlayLayerState Overlay;
    }

    // TODO: 等你确定 Background 需要什么信息后再填字段
    public struct BackgroundLayerState
    {
        // public float BaseBrightness;
        // public float PulseIntensity;
    }

    /// <summary>
    /// BossQuad 所需的敌人状态快照。
    /// 所有字段都假定已经在 VisualSystem 那一层换算好。
    /// </summary>
    public struct BossLayerState
    {
        /// <summary>
        /// 敌人 ID（如果只有一个敌人，可以直接用 0）。
        /// </summary>
        public int EnemyId;

        /// <summary>
        /// 敌人当前阶段（Spawning / Alive / Attacking / Dying / Inactive）。
        /// Shader 里通常会用 int / float 来判断阶段分支。
        /// </summary>
        public EnemyPhase EnemyPhase;

        /// <summary>
        /// 当前阶段内部进度（0~1）。
        /// 例如 Spawning/Dying 的渐显/渐隐等。
        /// </summary>
        public float EnemyPhaseProgress01;

        /// <summary>
        /// 敌人生命 0~1（已经归一化的血量百分比）。
        /// </summary>
        public float EnemyHealth01;

        /// <summary>
        /// 敌人最大生命，用于 Shader 做一些和最大值相关的效果（可选）。
        /// </summary>
        public float EnemyHealthMax;

        /// <summary>
        /// 当前攻击蓄力进度 0~1（Attacking 阶段用）。
        /// </summary>
        public float EnemyAttackCharge01;

        /// <summary>
        /// 攻击命中瞬间的脉冲值（0~1）。
        /// 可以由 VisualSystem 做一个短暂衰减的 pulse。
        /// </summary>
        public float EnemyAttackHitPulse01;

        // NEW: 敌人被光炮命中的脉冲（0~1）
        public float EnemyHitByBeamPulse01;
    }

    // TODO: Spell 层之后根据 ISpellRuntimeStatus 来设计
    public struct SpellLayerState
    {
        /// <summary>
        /// 左手掌心位置（假定为 0-1 的屏幕空间 / 归一化坐标）。
        /// </summary>
        public Vector2 LeftPalmPos01;

        /// <summary>
        /// 右手掌心位置（假定为 0-1 的屏幕空间 / 归一化坐标）。
        /// </summary>
        public Vector2 RightPalmPos01;

        /// <summary>
        /// 左手可见度（0=完全不可见，1=完全可见）。
        /// </summary>
        public float LeftPalmVisible01;

        /// <summary>
        /// 右手可见度（0=完全不可见，1=完全可见）。
        /// </summary>
        public float RightPalmVisible01;

        // —— 能量墙几何与激活度 —— 
        /// <summary>是否当前帧有能量墙存在（Activation01 > 0）。</summary>
        public bool HasEnergyWall;

        /// <summary>墙中心屏幕空间坐标（0-1）。</summary>
        public Vector2 WallCenterUV;

        /// <summary>墙尺寸（宽高，0-1）。</summary>
        public Vector2 WallSizeUV;

        /// <summary>墙的基础可见度/强度，对应 EnergyWallRuntimeStatus.Activation01。</summary>
        public float WallActivation01;

        /// <summary>墙当前所处阶段（Armed / Channeling / Recovery）。</summary>
        public WallShieldPhase WallPhase;

        /// <summary>墙当前阶段内部的 0-1 进度，对应 EnergyWallRuntimeStatus.PhaseProgress01。</summary>
        public float WallPhaseProgress01;

        // —— 光炮（Charge Beam）——
        /// <summary>当前是否存在光炮（runtimeStatus 存在且 Activation01 > 0）。</summary>
        public bool HasChargeBeam;

        /// <summary>光炮起点 UV（0-1）。</summary>
        public Vector2 BeamOriginUV;

        /// <summary>光炮尺寸 UV（x = 宽度，y = 长度）。</summary>
        public Vector2 BeamSizeUV;

        /// <summary>光炮当前整体可见度 / 强度（0-1）。</summary>
        public float BeamActivation01;

        /// <summary>光炮当前阶段（Armed / Firing / Recovery）。</summary>
        public ChargeBeamPhase BeamPhase;

        /// <summary>当前阶段内部进度（0-1）。</summary>
        public float BeamPhaseProgress01;

        /// <summary>蓄力进度（0-1），对应 ChargingProgress01。</summary>
        public float BeamChargingProgress01;

        // —— Boss 攻击与防御交互 —— 
        /// <summary>
        /// Boss 处于 Attack 阶段且玩家有能量墙时的“加亮因子”0-1，
        /// 由 VisualSystem 做平滑脉冲。
        /// </summary>
        public float ShieldBoostByBossAttack01;

        /// <summary>
        /// 玩家当前是否处于“Guarding”状态（Combat.Player.Status.IsGuarding）。
        /// 方便 Shader 区分“真的挡住了” vs “只是视觉上有墙”。
        /// </summary>
        public bool Guarded;
    }

    // TODO: Overlay 层将来做 HUD/屏幕特效再补
    public struct OverlayLayerState
    {
        // public float HurtOverlay01;
        // public float ScreenShake01;
    }

}
