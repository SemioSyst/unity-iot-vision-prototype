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

    // TODO: Boss 层的具体字段之后按 IEnemyRuntimeStatus 再细化
    public struct BossLayerState
    {
        // public float Health01;
        // public float Enraged01;
        // public float AttackTelegraph01;
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
