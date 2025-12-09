using ShaderDuel.Core;
using ShaderDuel.Gameplay;
using ShaderDuel.Hands;
using UnityEngine;

namespace ShaderDuel.Visual
{
    /// <summary>
    /// 视觉状态快照，每帧传递给这一帧画面决策时，系统能看到的上游信息
    /// </summary>
    public readonly struct VisualFrameInput
    {
        public readonly IHandsInput Hands; // 手势输入快照
        public readonly IAudioInput Audio; // 音频输入快照
        public readonly CombatRuntimeContext Combat; // 战斗局面快照

        public readonly GlobalHandFeatures GlobalHandFeatures;  //手势特征快照

        public readonly float DeltaTime;
        public readonly float Time;

        public VisualFrameInput(
            IHandsInput hands,
            IAudioInput audio,
            CombatRuntimeContext combat,
            GlobalHandFeatures globalHand,
            float deltaTime,
            float time)
        {
            Hands = hands;
            Audio = audio;
            Combat = combat;
            GlobalHandFeatures = globalHand;
            DeltaTime = deltaTime;
            Time = time;
        }
    }
}
