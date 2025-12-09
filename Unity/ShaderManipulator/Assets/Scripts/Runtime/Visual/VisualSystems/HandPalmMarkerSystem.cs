using UnityEngine;
using ShaderDuel.Hands;  // 为了 GlobalHandFeatures / HandFeatures

namespace ShaderDuel.Visual
{
    /// <summary>
    /// 从 GlobalHandFeatures 里读取左右手掌心位置，
    /// 写入 SpellLayerState，用于在 SpellQuad 里画两只手的标记。
    /// </summary>
    public sealed class HandPalmMarkerSystem : IVisualSystem
    {
        // 丢失多少帧之后完全看作不可见，用来做一个简单的 fade out
        private const int MaxLostFramesForFade = 10;

        public void UpdateVisuals(in VisualFrameInput input, ref VisualFrameState state)
        {
            var global = input.GlobalHandFeatures;

            // 先清一下，避免上一帧残留
            state.Spell.LeftPalmVisible01 = 0f;
            state.Spell.RightPalmVisible01 = 0f;

            // 左手
            if (global.HasLeftHand)
            {
                ApplyHand(global.LeftHand, ref state.Spell.LeftPalmPos01, ref state.Spell.LeftPalmVisible01);
            }

            // 右手
            if (global.HasRightHand)
            {
                ApplyHand(global.RightHand, ref state.Spell.RightPalmPos01, ref state.Spell.RightPalmVisible01);
            }
        }

        private static void ApplyHand(
            in HandFeatures hand,
            ref Vector2 outPos01,
            ref float outVisible01)
        {
            if (!hand.IsTracked)
            {
                outVisible01 = 0f;
                return;
            }

            // 位置：暂时直接拿 PalmCenter.xy 作为 0~1 屏幕空间
            // 如果你的 PalmCenter 是世界坐标，这里改成 Camera.WorldToViewportPoint 之类的就行
            // 注意 Y 轴要翻转一下
            outPos01 = new Vector2(hand.PalmCenter.x, 1.0f - hand.PalmCenter.y);

            // 可见度：根据连续丢帧数做一个简单的线性衰减
            // FramesSinceSeen = 0 → 1
            // FramesSinceSeen >= MaxLostFramesForFade → 0
            int lost = Mathf.Max(hand.FramesSinceSeen, 0);
            if (lost <= 0)
            {
                outVisible01 = 1f;
            }
            else if (lost >= MaxLostFramesForFade)
            {
                outVisible01 = 0f;
            }
            else
            {
                float t = (float)lost / MaxLostFramesForFade;
                outVisible01 = 1f - t;
            }
        }
    }
}
