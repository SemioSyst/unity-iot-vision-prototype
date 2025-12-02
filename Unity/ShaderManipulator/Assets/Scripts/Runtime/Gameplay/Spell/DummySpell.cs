using UnityEngine;
using ShaderDuel.Hands;
using ShaderDuel.Audio;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 一个非常简单的假法术：
    /// 右手在 Idle 状态下，握拳 + 掌心朝前，持续一小段时间就触发，
    /// 然后自动运行 0.5 秒后结束。
    /// </summary>
    [System.Serializable]
    public class DummySpellDefinition : SpellDefinition
    {
        public override string Id => "DummySpell";

        // 单手法术
        public override SpellHandRequirement HandRequirement => SpellHandRequirement.SingleHand;

        // 低优先级，后面真法术可以比它高
        public override int Priority => 0;

        // 触发条件：右手在 Idle，且 IsFist && NormalOrientation==Forward
        public override bool CanStart(
            HandTrackState left,
            HandTrackState right,
            GlobalHandFeatures handFeatures,
            GlobalAudioFeatures audioFeatures)
        {
            // 没右手、或右手当前已经被别的法术占用，直接不行
            if (right == null) return false;
            if (right.Phase != HandTrackPhase.Idle) return false;
            //Debug.Log("[DummySpell] right hand is free");

            var f = right.Features;
            if (f.FramesSinceSeen > 3)
                return false; // 容忍丢帧

            bool cond =
                f.IsFist &&
                f.TangentOrientation == PalmTangentOrientation.Forward;

            // 用 ConditionTimer 做“握拳朝前持续时间”的输入窗口
            string key = $"Dummy:{right.Side}:FistForward";
            float held = ConditionTimer.UpdateWithGrace(
                key,
                cond,
                falseGraceSeconds: 0.10f // 允许轻微抖动
            );

            // 比如说至少要保持 0.25 秒
            //return held >= 0.25f;
            if (held >= 1.0f)
            {
                //Debug.Log("[DummySpell] Triggered!");
                return true;
            }
            else
            {
                return false;
            }
        }

        public override RunningSpell CreateInstance(
            SpellOrchestrator orchestrator,
            HandTrackState[] boundHands,
            GlobalHandFeatures handFeatures,
            GlobalAudioFeatures audioFeatures)
        {
            // 这里简单假设只绑定一只手（右手）
            return new DummySpellInstance(this, orchestrator, boundHands);
        }
    }

    /// <summary>
    /// DummySpell 的运行时实例：
    /// 计时 0.5 秒后自动完成。
    /// </summary>
    public class DummySpellInstance : RunningSpell
    {
        private float _elapsed;
        private const float Duration = 2.0f;

        public DummySpellInstance(
            DummySpellDefinition def,
            SpellOrchestrator orchestrator,
            HandTrackState[] hands)
            : base(def, orchestrator, hands)
        {
        }

        public override void Tick(float dt,
                                  GlobalHandFeatures handFeatures,
                                  GlobalAudioFeatures audioFeatures)
        {
            if (IsCompleted || IsCancelled)
                return;

            _elapsed += dt;

            // 这里不做复杂逻辑，单纯“持续一下就结束”
            if (_elapsed >= Duration)
            {
                IsCompleted = true;

                // 顺便填一下 RuntimeStatus，方便后面可视化/Shader 用
                RuntimeStatus = new DummySpellRuntimeStatus
                {
                    Progress01 = 1f
                };
            }
            else
            {
                RuntimeStatus = new DummySpellRuntimeStatus
                {
                    Progress01 = Mathf.Clamp01(_elapsed / Duration)
                };
            }
        }

        public override void OnEnd()
        {
            // 清掉这个法术用到的计时 key，防止下次触发受上次残留影响
            foreach (var hand in BoundHands)
            {
                string key = $"Dummy:{hand.Side}:FistForward";
                ConditionTimer.Reset(key);
            }
            Debug.Log("[DummySpell] Ended.");

            // 这里暂时不需要通知 Shader 层，后面真法术再补
        }
    }

    public sealed class DummySpellRuntimeStatus : ISpellRuntimeStatus
    {
        public string SpellId => "DummySpell";

        public float Progress01;
    }
}
