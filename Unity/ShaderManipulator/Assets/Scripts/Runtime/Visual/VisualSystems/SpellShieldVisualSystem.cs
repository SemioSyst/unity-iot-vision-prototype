using System.Collections.Generic;
using UnityEngine;
using ShaderDuel.Gameplay;   // EnergyWallRuntimeStatus, EnemyPhase, DummyEnemyRuntimeStatus, CombatRuntimeContext 等

namespace ShaderDuel.Visual
{
    /// <summary>
    /// 负责把能量墙的 RuntimeStatus 映射到 SpellLayerState，
    /// 并根据 Boss 攻击状态对护盾进行“加亮”控制。
    /// </summary>
    public sealed class SpellShieldVisualSystem : IVisualSystem
    {
        private const string EnergyWallSpellId = "energy_wall";

        // 护盾加亮的内部脉冲值（0~1），根据 Boss 攻击状态自动升/降
        private float _shieldBoostPulse01 = 0f;

        // 加亮上升 / 下降速度（可以之后再调）
        private const float BoostRiseSpeed = 4f;  // 每秒从 0→1 需要 ~0.25s
        private const float BoostFallSpeed = 2f;  // 每秒衰减一半，稍微拖一点尾巴

        public void UpdateVisuals(in VisualFrameInput input, ref VisualFrameState state)
        {
            var combat = input.Combat;
            if (combat == null)
                return;

            // 先清一下本帧的护盾相关字段，避免残留
            ResetShieldState(ref state.Spell);

            // 1. 找出当前帧的能量墙状态（如果有）
            var wallStatus = FindEnergyWallStatus(combat.ActiveSpells);

            bool hasWall = (wallStatus != null);
            if (hasWall)
            {
                ApplyWallRuntimeToState(wallStatus, ref state.Spell);
            }

            // 2. 判断 Boss 是否处于 Attacking 阶段
            bool bossAttacking = IsBossAttacking(combat);

            // 3. 判断当前是否 Guarding（由战斗系统根据能量墙效果决定）
            bool guarded = IsPlayerGuarding(combat);
            state.Spell.Guarded = guarded;

            // 4. 更新护盾加亮脉冲
            bool shouldBoost =
                hasWall                 // 有墙存在
                && bossAttacking;       // Boss 正在攻击

            UpdateShieldBoost(shouldBoost, input.DeltaTime);
            state.Spell.ShieldBoostByBossAttack01 = _shieldBoostPulse01;
        }

        // --------- 帮助函数 ---------

        private static void ResetShieldState(ref SpellLayerState spellState)
        {
            spellState.HasEnergyWall = false;
            spellState.WallCenterUV = Vector2.zero;
            spellState.WallSizeUV = Vector2.zero;
            spellState.WallActivation01 = 0f;
            spellState.WallPhase = WallShieldPhase.Armed; // 默认值，无墙时一般不会用到
            spellState.WallPhaseProgress01 = 0f;

            spellState.ShieldBoostByBossAttack01 = 0f;
            // Guarded 在这一帧会重新由 IsPlayerGuarding 填写
        }

        private static EnergyWallRuntimeStatus FindEnergyWallStatus(
            IReadOnlyList<ISpellRuntimeStatus> activeSpells)
        {
            if (activeSpells == null) return null;

            for (int i = 0; i < activeSpells.Count; i++)
            {
                var status = activeSpells[i];
                if (status == null) continue;

                // 方式1：通过类型判断
                if (status is EnergyWallRuntimeStatus ew)
                {
                    //Debug.Log("Found EnergyWallRuntimeStatus by type.");
                    return ew;
                }

                // 方式2：通过 SpellId 判断（以防以后有别的实现）
                if (status.SpellId == EnergyWallSpellId && status is EnergyWallRuntimeStatus ewById)
                    return ewById;
            }

            return null;
        }

        private static void ApplyWallRuntimeToState(
            EnergyWallRuntimeStatus wall,
            ref SpellLayerState spellState)
        {
            spellState.HasEnergyWall = true;
            spellState.WallCenterUV = wall.WallCenterUV;
            spellState.WallSizeUV = wall.WallSizeUV;
            spellState.WallPhase = wall.Phase;
            spellState.WallPhaseProgress01 = Mathf.Clamp01(wall.PhaseProgress01);
            spellState.WallActivation01 = wall.Activation01;
            // RotationDeg 如果你将来想用，也可以额外塞到 SpellLayerState 里
        }

        private static bool IsBossAttacking(CombatRuntimeContext combat)
        {
            var enemies = combat.Enemies;
            if (enemies == null) return false;

            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e is DummyEnemyRuntimeStatus dummy)
                {
                    if (dummy.IsAlive && dummy.Phase == EnemyPhase.Attacking)
                        return true;
                }
                // 以后有其它敌人类型时，可以在这里扩展
            }

            return false;
        }

        private static bool IsPlayerGuarding(CombatRuntimeContext combat)
        {
            var player = combat.Player;
            if (player == null) return false;

            // 假定 IPlayerRuntimeStatus 里有 IsGuarding 属性，
            // 与 CombatManager.ResolveEnemyAttacks 里的逻辑对应。
            return player.IsGuarding;
        }

        private void UpdateShieldBoost(bool shouldBoost, float deltaTime)
        {
            if (shouldBoost)
            {
                // Boss 正在攻击且有墙：快速拉高
                _shieldBoostPulse01 = Mathf.MoveTowards(
                    _shieldBoostPulse01,
                    1f,
                    BoostRiseSpeed * deltaTime);
            }
            else
            {
                // 否则缓慢衰减
                _shieldBoostPulse01 = Mathf.MoveTowards(
                    _shieldBoostPulse01,
                    0f,
                    BoostFallSpeed * deltaTime);
            }
        }
    }
}
