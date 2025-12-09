namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// Dummy 敌人的状态快照。
    /// </summary>
    public sealed class DummyEnemyRuntimeStatus : IEnemyRuntimeStatus
    {
        public int EnemyId { get; set; }

        // EnemyPhase 枚举值，表示当前所处的阶段
        // Inactive 完全没东西（槽位空）
        // Spawning 出现动画 / 渐显
        // Alive 存在中（待命）
        // Attacking 正在攻击（给 shader 做攻击特效）
        // Dying 死亡动画中
        public EnemyPhase Phase { get; set; }
        public float PhaseProgress01 { get; set; }

        public float Health { get; set; }
        public bool IsAlive { get; set; }

        // 怒气条（已存在）
        public float Rage01 { get; set; }

        // 新增：这一轮攻击的蓄力进度（0~1，方便给 shader 用）
        public float AttackCharge01 { get; set; }

        // 新增：这一帧是否真正“出手命中”
        public bool AttackHitThisFrame { get; set; }

        // 新增：这次命中的伤害量（方便 CombatManager 读取）
        public float AttackDamage { get; set; }
    }

}

