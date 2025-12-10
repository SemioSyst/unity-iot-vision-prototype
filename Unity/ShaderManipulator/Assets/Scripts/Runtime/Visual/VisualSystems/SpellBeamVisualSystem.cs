using System.Collections.Generic;
using UnityEngine;
using ShaderDuel.Gameplay;   // ChargeBeamRuntimeStatus, ChargeBeamPhase, ISpellRuntimeStatus

namespace ShaderDuel.Visual
{
    /// <summary>
    /// 把当前帧的蓄力光炮状态（ChargeBeamRuntimeStatus）
    /// 映射到 SpellLayerState，供 SpellQuad shader 使用。
    /// </summary>
    public sealed class SpellBeamVisualSystem : IVisualSystem
    {
        private const string ChargeBeamSpellId = "charge_beam";

        public void UpdateVisuals(in VisualFrameInput input, ref VisualFrameState state)
        {
            var combat = input.Combat;
            if (combat == null)
                return;

            // 只重置本系统负责的 Beam 部分，不动墙 / 手心 / 护盾
            ResetBeamState(ref state.Spell);

            // 1. 找出当前帧的光炮 runtimeStatus（如果有）
            var beamStatus = FindChargeBeamStatus(combat.ActiveSpells);
            if (beamStatus == null)
            {
                // 没有光炮，Beam 区域保持为默认值
                return;
            }

            // 2. 把 ChargeBeamRuntimeStatus 映射到 SpellLayerState
            ApplyBeamRuntimeToState(beamStatus, ref state.Spell);
        }

        // --------- 内部帮助方法 ---------

        private static void ResetBeamState(ref SpellLayerState spellState)
        {
            spellState.HasChargeBeam = false;
            spellState.BeamOriginUV = Vector2.zero;
            spellState.BeamSizeUV = Vector2.zero;
            spellState.BeamActivation01 = 0f;
            spellState.BeamPhase = ChargeBeamPhase.Armed; // 默认值，无光炮时一般不会被用到
            spellState.BeamPhaseProgress01 = 0f;
            spellState.BeamChargingProgress01 = 0f;
        }

        private static ChargeBeamRuntimeStatus FindChargeBeamStatus(
            IReadOnlyList<ISpellRuntimeStatus> activeSpells)
        {
            if (activeSpells == null)
                return null;

            for (int i = 0; i < activeSpells.Count; i++)
            {
                var status = activeSpells[i];
                if (status == null)
                    continue;

                // 优先通过类型判断
                if (status is ChargeBeamRuntimeStatus beam)
                    return beam;

                // 备用：通过 SpellId 判断，以防以后有别的实现类
                if (status.SpellId == ChargeBeamSpellId && status is ChargeBeamRuntimeStatus beamById)
                    return beamById;
            }

            return null;
        }

        private static void ApplyBeamRuntimeToState(
            ChargeBeamRuntimeStatus beam,
            ref SpellLayerState spellState)
        {
            
            bool hasBeam = true;

            spellState.HasChargeBeam = hasBeam;
            spellState.BeamOriginUV = beam.BeamOriginUV;
            spellState.BeamSizeUV = beam.BeamSizeUV;
            spellState.BeamActivation01 = beam.Activation01;

            spellState.BeamPhase = beam.Phase;
            spellState.BeamPhaseProgress01 = Mathf.Clamp01(beam.PhaseProgress01);
            spellState.BeamChargingProgress01 = Mathf.Clamp01(beam.ChargingProgress01);
        }
    }
}
