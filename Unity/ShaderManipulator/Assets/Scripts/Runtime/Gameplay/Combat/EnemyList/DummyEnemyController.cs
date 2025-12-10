using UnityEngine;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 一个用于测试的 Dummy 敌人控制器。
    /// </summary>
    public sealed class DummyEnemyController : EnemyController
    {
        // 对外只暴露 IEnemyRuntimeStatus
        public override IEnemyRuntimeStatus Status => _status;

        // 内部实际可写的状态对象
        private readonly DummyEnemyRuntimeStatus _status = new DummyEnemyRuntimeStatus();

        private float _phaseTimer;

        // 攻击节奏相关
        private readonly float _attackInterval = 30f;      // 每 30 秒一轮
        private readonly float _attackWindupDuration = 6f; // 蓄力 6 秒
        private readonly float _attackRecoveryDuration = 0.5f; // 收招 0.5 秒
        private float _timeSinceLastAttack;
        private bool _autoAttackEnabled = true;
        private bool _hitFiredInThisAttack;

        public DummyEnemyController(int enemyId, float initialHealth = 50f)
        {
            _status.EnemyId = enemyId;
            _status.Health = initialHealth;
            _status.IsAlive = true;

            _status.Phase = EnemyPhase.Spawning;
            _status.PhaseProgress01 = 0f;
            _status.Rage01 = 0f;

            _phaseTimer = 0f;
            _timeSinceLastAttack = 0f;
        }

        public override void Tick(float deltaTime)
        {
            // 每帧开头先清掉“被光炮击中”标记，保持 per-frame 语义
            _status.HitByBeamThisFrame = false;

            if (!_status.IsAlive && _status.Phase != EnemyPhase.Dying &&
                _status.Phase != EnemyPhase.Inactive)
            {
                // 一旦被判定死亡，进入 Dying 阶段（渐隐）
                ChangePhase(EnemyPhase.Dying);
            }

            _phaseTimer += deltaTime;

            switch (_status.Phase)
            {
                case EnemyPhase.Spawning:
                    UpdateSpawning(deltaTime);
                    break;

                case EnemyPhase.Alive:
                    UpdateAlive(deltaTime);
                    break;

                case EnemyPhase.Attacking:
                    UpdateAttacking(deltaTime);
                    break;

                case EnemyPhase.Dying:
                    UpdateDying(deltaTime);
                    break;

                case EnemyPhase.Inactive:
                    // 槽位空，不做事
                    _status.PhaseProgress01 = 0f;
                    break;
            }
        }

        #region Phase Updates

        private void UpdateSpawning(float dt)
        {
            const float spawnDuration = 0.5f;
            _status.PhaseProgress01 = Mathf.Clamp01(_phaseTimer / spawnDuration);

            if (_phaseTimer >= spawnDuration)
            {
                ChangePhase(EnemyPhase.Alive);
            }
        }

        private void UpdateAlive(float dt)
        {
            _status.PhaseProgress01 = 1f;

            // 怒气慢慢涨
            _status.Rage01 = Mathf.Clamp01(_status.Rage01 + dt * 0.1f);

            // 攻击节奏：只在 Alive 且允许自动攻击时才计时
            if (_autoAttackEnabled)
            {
                _timeSinceLastAttack += dt;
                if (_timeSinceLastAttack >= _attackInterval)
                {
                    StartAttack();
                }
            }
        }

        private void StartAttack()
        {
            _timeSinceLastAttack = 0f;
            _hitFiredInThisAttack = false;

            _status.AttackCharge01 = 0f;
            _status.AttackHitThisFrame = false;
            _status.AttackDamage = 0f;

            ChangePhase(EnemyPhase.Attacking);
        }

        private void UpdateAttacking(float dt)
        {
            // 一轮攻击总时长 = 蓄力 + 收招
            float totalDuration = _attackWindupDuration + _attackRecoveryDuration;

            // CombatManager 每帧都会 Tick 所有敌人，所以这里用 _phaseTimer 追时间
            _status.PhaseProgress01 = Mathf.Clamp01(_phaseTimer / totalDuration);

            // 先清掉上一帧的命中标记
            _status.AttackHitThisFrame = false;

            // 1）蓄力阶段：0 ~ attackWindupDuration
            if (_phaseTimer <= _attackWindupDuration)
            {
                _status.AttackCharge01 = Mathf.Clamp01(_phaseTimer / _attackWindupDuration);
            }
            else
            {
                _status.AttackCharge01 = 1f;
            }

            // 2）到达蓄力完成时间点时，触发一次“命中事件”
            if (!_hitFiredInThisAttack && _phaseTimer >= _attackWindupDuration)
            {
                _hitFiredInThisAttack = true;
                _status.AttackHitThisFrame = true;
                _status.AttackDamage = 10f; // 比如 10 点伤害，先写死
            }

            // 3）到总时长结束 → 回到 Alive
            if (_phaseTimer >= totalDuration)
            {
                _status.AttackCharge01 = 0f;
                ChangePhase(EnemyPhase.Alive);
            }
        }

        private void UpdateDying(float dt)
        {
            const float dyingDuration = 0.5f;
            float t = Mathf.Clamp01(_phaseTimer / dyingDuration);
            _status.PhaseProgress01 = 1f - t;

            if (_phaseTimer >= dyingDuration)
            {
                _status.Phase = EnemyPhase.Inactive;
                _status.PhaseProgress01 = 0f;
            }
        }

        #endregion

        #region Public API 实现

        public override void ApplyHit(float damage)
        {
            if (_status.Phase == EnemyPhase.Inactive)
                return;

            if (damage <= 0f) return;

            _status.Health -= damage;
            if (_status.Health <= 0f)
            {
                _status.Health = 0f;
                _status.IsAlive = false;
                // 真正切 Phase 的动作在 Tick 里统一处理
            }
        }

        public override void TriggerAttack()
        {
            // 只有在 Alive 时才允许发起攻击
            if (_status.Phase == EnemyPhase.Alive)
            {
                ChangePhase(EnemyPhase.Attacking);
            }
        }

        /// <summary>是否自动按节奏攻击（可以在调试时临时关掉）。</summary>
        public bool AutoAttackEnabled
        {
            get => _autoAttackEnabled;
            set => _autoAttackEnabled = value;
        }

        /// <summary>立刻开始一轮攻击（无视冷却），调试用。</summary>
        public void ForceAttackNow()
        {
            if (_status.Phase == EnemyPhase.Alive)
            {
                StartAttack();
            }
        }

        #endregion

        private void ChangePhase(EnemyPhase newPhase)
        {
            _status.Phase = newPhase;
            _phaseTimer = 0f;
        }
    }
}

