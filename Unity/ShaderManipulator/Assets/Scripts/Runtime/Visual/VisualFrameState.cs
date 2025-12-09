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

        // 以后你再往这里加其它 spell 相关的字段（比如是否在施法状态、护盾、弹幕等等）
    }

    // TODO: Overlay 层将来做 HUD/屏幕特效再补
    public struct OverlayLayerState
    {
        // public float HurtOverlay01;
        // public float ScreenShake01;
    }

}
