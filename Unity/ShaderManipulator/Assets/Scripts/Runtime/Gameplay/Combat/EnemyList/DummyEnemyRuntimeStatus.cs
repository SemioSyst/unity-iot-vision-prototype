namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// Dummy 敌人的状态快照。
    /// </summary>
    public sealed class DummyEnemyRuntimeStatus : IEnemyRuntimeStatus
    {
        public int EnemyId { get; set; }

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

