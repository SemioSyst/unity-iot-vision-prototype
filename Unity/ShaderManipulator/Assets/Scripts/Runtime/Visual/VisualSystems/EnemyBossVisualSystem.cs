using System.Collections.Generic;
using UnityEngine;
using ShaderDuel.Gameplay;   // EnemyPhase, DummyEnemyRuntimeStatus, CombatRuntimeContext 等
// using ShaderDuel.Visual;  // 如果你把 IVisualSystem / VisualFrame* 放在这个命名空间，就把本类放一起

namespace ShaderDuel.Visual
{
    /// <summary>
    /// 从 CombatRuntimeContext.Enemies 中选出当前 Boss（目前是 Dummy），
    /// 并将其状态写入 BossLayerState，供 BossQuad 使用。
    /// </summary>
    public sealed class EnemyBossVisualSystem : IVisualSystem
    {
        // 记录每个敌人历史上观测到的最大生命值，用来推算 EnemyHealthMax
        private readonly Dictionary<int, float> _maxHealthByEnemyId = new Dictionary<int, float>();

        // 记录每个敌人的命中脉冲值（0~1），用于 EnemyAttackHitPulse01
        private readonly Dictionary<int, float> _hitPulseByEnemyId = new Dictionary<int, float>();

        // 命中脉冲从 1 衰减到 0 的时间（秒）
        private const float HitPulseDuration = 0.3f;

        // 记录每个敌人被光炮命中的脉冲值（0~1），用于 EnemyHitByBeamPulse01
        private readonly Dictionary<int, float> _beamHitPulseByEnemyId = new Dictionary<int, float>();

        // 被光炮命中脉冲从 1 衰减到 0 的时间（秒）
        private const float BeamHitPulseDuration = 0.25f;

        public void UpdateVisuals(in VisualFrameInput input, ref VisualFrameState state)
        {
            var context = input.Combat;
            if (context == null || context.Enemies == null || context.Enemies.Count == 0)
            {
                state.Boss = default;
                return;
            }

            // 1. 选出当前要作为 Boss 的敌人：优先选择还活着的 Dummy
            DummyEnemyRuntimeStatus dummyBoss = null;

            // 先找存活的 Dummy
            foreach (var enemyStatus in context.Enemies)
            {
                if (enemyStatus is DummyEnemyRuntimeStatus dummy && dummy.IsAlive)
                {
                    dummyBoss = dummy;
                    break;
                }
            }

            // 如果没有存活的，就退而求其次，找任何一个 Dummy
            if (dummyBoss == null)
            {
                foreach (var enemyStatus in context.Enemies)
                {
                    if (enemyStatus is DummyEnemyRuntimeStatus dummy)
                    {
                        dummyBoss = dummy;
                        break;
                    }
                }
            }

            // 场上真的一个 Dummy 都没有，就清空 Boss 状态
            if (dummyBoss == null)
            {
                state.Boss = default;
                return;
            }

            // 2. 从 DummyEnemyRuntimeStatus 映射到 BossLayerState

            int enemyId = dummyBoss.EnemyId;

            // 2.1 生命值 & 最大生命值（用历史记录的最大 Health 作为 Max）
            float health = Mathf.Max(dummyBoss.Health, 0f);
            float maxHealth = GetOrUpdateMaxHealth(enemyId, health);
            float health01 = (maxHealth > 0f) ? Mathf.Clamp01(health / maxHealth) : 0f;

            // 2.2 阶段 & 阶段进度
            EnemyPhase phase = dummyBoss.Phase;
            float phaseProgress01 = Mathf.Clamp01(dummyBoss.PhaseProgress01);

            // 2.3 攻击蓄力
            float attackCharge01 = Mathf.Clamp01(dummyBoss.AttackCharge01);

            // 2.4 命中脉冲（命中的那一帧置为 1，然后在后续帧按时间衰减）
            float hitPulse01 = UpdateHitPulse(enemyId, dummyBoss.AttackHitThisFrame, input.DeltaTime);

            // 2.5 被光炮命中脉冲（光炮扫到 Dummy）
            float hitByBeamPulse01 = UpdateBeamHitPulse(enemyId, dummyBoss.HitByBeamThisFrame, input.DeltaTime);

            // 3. 写入 BossLayerState
            state.Boss.EnemyId = enemyId;
            state.Boss.EnemyPhase = phase;
            state.Boss.EnemyPhaseProgress01 = phaseProgress01;
            state.Boss.EnemyHealth01 = health01;
            state.Boss.EnemyHealthMax = maxHealth;
            state.Boss.EnemyAttackCharge01 = attackCharge01;
            state.Boss.EnemyAttackHitPulse01 = hitPulse01;
            state.Boss.EnemyHitByBeamPulse01 = hitByBeamPulse01;
        }

        /// <summary>
        /// 更新并返回该敌人的最大生命值：取“历史上观测到的最大 Health”。
        /// </summary>
        private float GetOrUpdateMaxHealth(int enemyId, float currentHealth)
        {
            if (_maxHealthByEnemyId.TryGetValue(enemyId, out float existingMax))
            {
                float newMax = Mathf.Max(existingMax, currentHealth);
                _maxHealthByEnemyId[enemyId] = newMax;
                return newMax;
            }
            else
            {
                // 首次看到这个敌人，就用当前生命作为起始 Max
                float initialMax = Mathf.Max(currentHealth, 0f);
                _maxHealthByEnemyId[enemyId] = initialMax;
                return initialMax;
            }
        }

        /// <summary>
        /// 根据 AttackHitThisFrame 和 deltaTime 更新命中脉冲值（0~1）。
        /// 命中当帧设为 1，之后按固定时长线性衰减到 0。
        /// </summary>
        private float UpdateHitPulse(int enemyId, bool hitThisFrame, float deltaTime)
        {
            if (!_hitPulseByEnemyId.TryGetValue(enemyId, out float pulse))
            {
                pulse = 0f;
            }

            if (hitThisFrame)
            {
                // 命中当帧：直接打满
                pulse = 1f;
            }
            else if (pulse > 0f && HitPulseDuration > 0f)
            {
                // 非命中帧：按照持续时间线性衰减
                float decayPerSecond = 1f / HitPulseDuration;
                pulse = Mathf.Max(0f, pulse - decayPerSecond * deltaTime);
            }

            _hitPulseByEnemyId[enemyId] = pulse;
            return pulse;
        }

        /// <summary>
        /// 根据 HitByBeamThisFrame 和 deltaTime 更新“被光炮命中脉冲”值（0~1）。
        /// 命中当帧设为 1，之后按固定时长线性衰减到 0。
        /// </summary>
        private float UpdateBeamHitPulse(int enemyId, bool hitByBeamThisFrame, float deltaTime)
        {
            if (!_beamHitPulseByEnemyId.TryGetValue(enemyId, out float pulse))
            {
                pulse = 0f;
            }

            if (hitByBeamThisFrame)
            {
                // 命中当帧：直接打满
                pulse = 1f;
            }
            else if (pulse > 0f && BeamHitPulseDuration > 0f)
            {
                // 非命中帧：按照持续时间线性衰减
                float decayPerSecond = 1f / BeamHitPulseDuration;
                pulse = Mathf.Max(0f, pulse - decayPerSecond * deltaTime);
            }

            _beamHitPulseByEnemyId[enemyId] = pulse;
            return pulse;
        }
    }
}
