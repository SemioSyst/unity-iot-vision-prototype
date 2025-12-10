using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 战斗层核心：
    /// - 站在“战斗规则”的角度解释 SpellOrchestrator 的输出；
    /// - 负责把伤害 / 护盾等效果应用到玩家和敌人；
    /// - 刷新 CombatRuntimeContext 供渲染 / UI 使用。
    /// 不直接驱动法术 FSM，只读取 SpellOrchestrator.RunningSpells。
    /// </summary>
    public sealed class CombatManager
    {
        public CombatRuntimeContext Context => _context;

        public PlayerCombatController Player => _player;
        public IReadOnlyList<EnemyController> Enemies => _enemies;

        private readonly PlayerCombatController _player;
        private readonly List<EnemyController> _enemies = new();
        private readonly SpellOrchestrator _spellOrchestrator;

        private readonly CombatRuntimeContext _context = new CombatRuntimeContext();

        public CombatManager(
            SpellOrchestrator spellOrchestrator,
            PlayerCombatController player,
            IEnumerable<EnemyController> initialEnemies = null)
        {
            _spellOrchestrator = spellOrchestrator;
            _player = player;

            if (initialEnemies != null)
            {
                _enemies.AddRange(initialEnemies);
            }
        }

        /// <summary>
        /// 外部可以在运行时添加敌人（比如生成新波次）。
        /// </summary>
        public void AddEnemy(EnemyController enemy)
        {
            if (enemy != null)
            {
                _enemies.Add(enemy);
            }
        }

        /// <summary>
        /// 每帧由一个 MonoBehaviour 驱动调用。
        /// 不驱动 SpellOrchestrator 的 Update，只做战斗层的解释与结算。
        /// </summary>
        public void Tick(float deltaTime)
        {
            // 1. 玩家 / 敌人自己的时间演化
            _player.Tick(deltaTime);
            foreach (var enemy in _enemies)
            {
                enemy.Tick(deltaTime);
            }

            // 2. 读取当前运行中的法术
            var runningSpells = _spellOrchestrator.RunningSpells;

            // 3. 先解释法术对玩家的防御效果（能量墙护盾等）
            ResolveSpellEffectsForPlayer(runningSpells);

            // 4. 再结算法术对敌人的伤害（比如蓄力光炮 DOT）
            ResolveSpellDamageToEnemies(runningSpells, deltaTime);

            // 5. 最后结算敌人攻击对玩家的伤害
            ResolveEnemyAttacks();

            // 6. 刷新 Context 供渲染 / UI 使用
            RefreshContext(runningSpells);
        }

        #region 解释法术效果 → 玩家战斗状态

        /// <summary>
        /// 从当前所有 RunningSpell 的 RuntimeStatus 中推导玩家的“防御状态”。
        /// 目前只处理能量墙：Channeling 时视为有护盾。
        /// </summary>
        private void ResolveSpellEffectsForPlayer(IReadOnlyList<RunningSpell> runningSpells)
        {
            bool hasGuard = false;
            float maxGuardStrength01 = 0f;

            foreach (var spell in runningSpells)
            {
                var status = spell.RuntimeStatus; // ISpellRuntimeStatus

                // 这里只关心能量墙（以后可以扩展更多类型）
                if (status is EnergyWallRuntimeStatus wallStatus)
                {
                    if (wallStatus.Phase == WallShieldPhase.Channeling)
                    {
                        hasGuard = true;
                        if (wallStatus.Activation01 > maxGuardStrength01)
                        {
                            maxGuardStrength01 = wallStatus.Activation01;
                        }
                    }
                }
            }

            // 把结果写回玩家战斗控制器
            _player.SetGuarding(hasGuard);
            _player.SetGuardStrength01(hasGuard ? maxGuardStrength01 : 0f);
        }

        #endregion

        #region 法术效果 → 敌人伤害

        /// <summary>
        /// 结算当前所有法术对敌人的伤害。
        /// 目前只实现：蓄力光炮在 Firing 阶段对所有敌人造成小额 DOT。
        /// </summary>
        private void ResolveSpellDamageToEnemies(
            IReadOnlyList<RunningSpell> runningSpells,
            float deltaTime)
        {
            if (_enemies.Count == 0)
                return;

            // 基础 DPS：光炮在最低蓄力时的每秒伤害
            const float MinDps = 5f;
            // 完全蓄满时的每秒伤害上限
            const float MaxDps = 18f;

            foreach (var spell in runningSpells)
            {
                var status = spell.RuntimeStatus;

                // 只关心蓄力光炮
                if (status is ChargeBeamRuntimeStatus beamStatus)
                {
                    // 只有在 Firing 阶段且“可见度”> 0 时才造成伤害
                    if (beamStatus.Phase != ChargeBeamPhase.Firing ||
                        beamStatus.Activation01 <= 0f)
                    {
                        continue;
                    }

                    // 根据蓄力程度在 [MinDps, MaxDps] 之间插值
                    float dps = Mathf.Lerp(MinDps, MaxDps, beamStatus.ChargingProgress01);

                    // 本帧伤害 = 每秒伤害 * deltaTime
                    float damageThisFrame = dps * deltaTime;
                    if (damageThisFrame <= 0f)
                        continue;

                    // 对场上所有敌人施加伤害
                    foreach (var enemy in _enemies)
                    {
                        // 敌人内部自己判断死活 / 无效状态
                        enemy.ApplyHit(damageThisFrame);

                        // 如果是 Dummy 敌人，打一个“本帧被光炮击中”的标记
                        if (enemy.Status is DummyEnemyRuntimeStatus dummyStatus)
                        {
                            dummyStatus.HitByBeamThisFrame = true;
                        }
                    }
                }
            }
        }

        #endregion

        #region 敌人效果处理
        private void ResolveEnemyAttacks()
        {
            foreach (var enemy in _enemies)
            {
                var status = enemy.Status;

                if (status is DummyEnemyRuntimeStatus dummy &&
                    dummy.AttackHitThisFrame &&
                    dummy.IsAlive)
                {
                    // 这里可以加上“位置 / 是否被能量墙挡住”的判断；
                    // 现在先简化：只要玩家没护盾就扣血。

                    if (!_player.Status.IsGuarding)
                    {
                        _player.ApplyHit(dummy.AttackDamage);
                    }
                    else
                    {
                        // 有护盾：要么完全免疫，要么在这里做减伤逻辑
                        // 例如：护盾强度 0~1 → 挡掉对应比例伤害
                    }

                    // 这次命中处理完后，DummyEnemyController 下一帧会把 AttackHitThisFrame 清成 false
                }
            }
        }
        #endregion

        #region Context 刷新

        private void RefreshContext(IReadOnlyList<RunningSpell> runningSpells)
        {
            // 玩家
            _context.Player = _player.Status;

            // 敌人列表（不同敌人类型统一抽象为 IEnemyRuntimeStatus）
            _context.Enemies = _enemies
                .Select(e => e.Status)
                .ToArray();

            // 当前所有运行中的法术状态
            _context.ActiveSpells = runningSpells
                .Select(s => s.RuntimeStatus)
                .ToArray();
        }

        #endregion
    }
}

