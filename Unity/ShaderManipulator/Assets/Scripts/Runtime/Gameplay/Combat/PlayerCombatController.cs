using UnityEngine;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 负责维护玩家战斗相关的 Runtime 状态，
    /// 不关心输入来源（手势/音频/键盘），只关心“结果”：受击、护盾、硬直等。
    /// </summary>
    public sealed class PlayerCombatController
    {
        // 对外暴露统一的 RuntimeStatus（只读引用）
        public IPlayerRuntimeStatus Status => _status;

        // 内部可写的状态对象
        private readonly PlayerRuntimeStatus _status = new PlayerRuntimeStatus();

        // 内部使用的一些计时器，用于处理随时间恢复的状态（例如硬直）
        private float _stunRemainSeconds;

        /// <summary>
        /// 创建一个玩家战斗控制器。
        /// </summary>
        /// <param name="initialHealth">初始生命值，默认 100。</param>
        public PlayerCombatController(float initialHealth = 100f)
        {
            _status.Health = initialHealth;
            _status.IsAlive = true;

            _status.IsGuarding = false;
            _status.GuardStrength01 = 0f;

            _status.IsStunned = false;
            _status.IsCastingSpell = false;

            _stunRemainSeconds = 0f;
        }

        /// <summary>
        /// 每帧更新时间相关的状态（例如硬直结束）。
        /// CombatManager 可以在 Update 中调用。
        /// </summary>
        public void Tick(float deltaTime)
        {
            UpdateStun(deltaTime);
            // 如果以后需要：可以在这里处理护盾渐隐、持续伤害等效果
        }

        #region 受击 / 治疗

        /// <summary>
        /// 玩家受到一次伤害。
        /// 护盾优先承担伤害，不足部分再扣生命。
        /// </summary>
        public void ApplyHit(float damage)
        {
            if (!_status.IsAlive) return;
            if (damage <= 0f) return;

            // 先用护盾抵消一部分
            if (_status.IsGuarding && _status.GuardStrength01 > 0f)
            {
                // 简单示例：按线性比例扣护盾
                float guardCost = damage * 0.01f;
                _status.GuardStrength01 -= guardCost;
                if (_status.GuardStrength01 < 0f)
                    _status.GuardStrength01 = 0f;
            }
            else
            {
                // 没护盾或护盾耗尽时，扣血
                _status.Health -= damage;
                if (_status.Health <= 0f)
                {
                    _status.Health = 0f;
                    _status.IsAlive = false;
                }
            }
        }

        /// <summary>
        /// 玩家恢复生命（例如治疗法术）。
        /// </summary>
        public void Heal(float amount, float maxHealth = 100f)
        {
            if (!_status.IsAlive) return;
            if (amount <= 0f) return;

            _status.Health += amount;
            if (_status.Health > maxHealth)
                _status.Health = maxHealth;
        }

        #endregion

        #region 护盾 / 防御

        /// <summary>
        /// 设置玩家当前是否处于防御状态。
        /// 实际开启/关闭由法术系统或 CombatManager 决定。
        /// </summary>
        public void SetGuarding(bool isGuarding)
        {
            _status.IsGuarding = isGuarding;
        }

        /// <summary>
        /// 直接覆盖当前护盾强度（0~1）。
        /// 例如防御法术刚刚生成一个新的护盾时调用。
        /// </summary>
        public void SetGuardStrength01(float value)
        {
            _status.GuardStrength01 = Mathf.Clamp01(value);
        }

        #endregion

        #region 硬直 / 控制状态

        /// <summary>
        /// 施加一次硬直效果，持续 stunDuration 秒。
        /// 若当前已有更长的硬直，则保留较长的那一个。
        /// </summary>
        public void ApplyStun(float stunDuration)
        {
            if (stunDuration <= 0f) return;

            if (stunDuration > _stunRemainSeconds)
            {
                _stunRemainSeconds = stunDuration;
            }

            _status.IsStunned = true;
        }

        private void UpdateStun(float deltaTime)
        {
            if (_stunRemainSeconds <= 0f)
            {
                _status.IsStunned = false;
                _stunRemainSeconds = 0f;
                return;
            }

            _stunRemainSeconds -= deltaTime;
            if (_stunRemainSeconds <= 0f)
            {
                _stunRemainSeconds = 0f;
                _status.IsStunned = false;
            }
        }

        #endregion

        #region 施法标记（由法术系统驱动）

        /// <summary>
        /// 用于让 SpellOrchestrator 或 CombatManager 标记：
        /// 玩家当前是否处于“有法术正在进行”的状态。
        /// </summary>
        public void SetCastingSpell(bool isCasting)
        {
            _status.IsCastingSpell = isCasting;
        }

        #endregion
    }
}

