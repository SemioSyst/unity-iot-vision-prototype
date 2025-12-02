using UnityEngine;
using ShaderDuel.Hands;
using ShaderDuel.Audio;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// “能量墙护盾”的法术定义（双手、防御型）。
    /// </summary>
    [CreateAssetMenu(
    fileName = "EnergyWallDefinition",
    menuName = "ShaderDuel/Spells/Energy Wall"
    )]
    public sealed class EnergyWallDefinition : SpellDefinition
    {
        public override string Id => "energy_wall";

        public override SpellHandRequirement HandRequirement => SpellHandRequirement.DualHand;

        // 比 DummySpell 稍微高一点
        public override int Priority => 20;

        // 进入「准备成功」所需的持续时间
        private const float ArmingHoldTime = 0.30f;
        // 允许中间偶发抖动的宽限时间
        private const float ArmingGraceTime = 0.10f;

        /// <summary>
        /// 触发条件：双手在 Idle，双手握拳，
        /// 左手掌向右、右手掌向左，并且姿态稳定一段时间。
        /// </summary>
        public override bool CanStart(
            HandTrackState left,
            HandTrackState right,
            GlobalHandFeatures handFeatures,
            GlobalAudioFeatures audioFeatures)
        {
            // 双手都得在 Idle 且在场，才能启动双手法术
            if (left == null || right == null)
                return false;

            if (left.Phase != HandTrackPhase.Idle ||
                right.Phase != HandTrackPhase.Idle)
                return false;

            if (!handFeatures.HasLeftHand || !handFeatures.HasRightHand)
                return false;

            var lh = handFeatures.LeftHand;
            var rh = handFeatures.RightHand;

            if (!lh.IsTracked || !rh.IsTracked)
                return false;

            // 具体姿态：双手握拳 + 左掌向右 + 右掌向左
            bool poseOk =
                lh.IsFist &&
                rh.IsFist &&
                lh.NormalOrientation == PalmNormalOrientation.Right &&
                rh.NormalOrientation == PalmNormalOrientation.Left;

            // 用 ConditionTimer 做“握拳姿态持续时间 + 防抖”
            float held = ConditionTimer.UpdateWithGrace(
                key: "EnergyWall:ArmingPose",
                isTrue: poseOk,
                falseGraceSeconds: ArmingGraceTime);

            return held >= ArmingHoldTime;
        }

        /// <summary>
        /// 创建运行中的能量墙实例。
        /// </summary>
        public override RunningSpell CreateInstance(
            SpellOrchestrator orchestrator,
            HandTrackState[] hands,
            GlobalHandFeatures handFeatures,
            GlobalAudioFeatures audioFeatures)
        {
            // 双手法术，约定 hands 里是左右两只手
            return new EnergyWallSpell(this, orchestrator, hands);
        }
    }
}
