using UnityEngine;
using ShaderDuel.Hands;   // 为了 HandFeatures / GlobalHandFeatures

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 左手 / 右手标记。
    /// </summary>
    public enum HandSide
    {
        Left,
        Right
    }

    /// <summary>
    /// 每只手的高层状态。
    /// </summary>
    public enum HandTrackPhase
    {
        NoHand = 0,   // 未检测到手
        Idle = 1,     // 手在场，未被任何法术占用
        Candidate = 2,// 进入“施术候选”缓冲区
        InSpell = 3   // 正在被某个法术实例占用
    }

    /// <summary>
    /// 每只手的当前跟踪上下文，用于调度器决策。
    /// </summary>
    public class HandTrackState
    {
        public readonly HandSide Side;

        public HandTrackPhase Phase = HandTrackPhase.NoHand;

        /// <summary>最近一帧的手部特征（从 HandFeatureExtractor 提供）。</summary>
        public HandFeatures Features;

        /// <summary>自上次“看到这只手”以来已过去的帧数，用于容忍丢帧。</summary>
        public int FramesSinceSeen = int.MaxValue;

        /// <summary>当前占用这只手的法术实例，如果没有则为 null。</summary>
        public RunningSpell CurrentSpell;

        public HandTrackState(HandSide side)
        {
            Side = side;
        }

        /// <summary>
        /// 调度器可以用这个帮助判断某些条件，比如“是否视为还在场”。
        /// </summary>
        public bool IsConsideredPresent(int maxMissingFrames)
        {
            return FramesSinceSeen <= maxMissingFrames;
        }
    }
}

