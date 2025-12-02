namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 敌人控制器的抽象基类。
    /// 每种敌人（Dummy、Boss、精英等）可以有各自的子类。
    /// </summary>
    public abstract class EnemyController
    {
        /// <summary>
        /// 当前敌人的 Runtime 状态快照。
        /// 外部（CombatManager / Shader）只能读接口。
        /// </summary>
        public abstract IEnemyRuntimeStatus Status { get; }

        /// <summary>
        /// 每帧更新敌人内部状态。
        /// </summary>
        public abstract void Tick(float deltaTime);

        /// <summary>
        /// 敌人受到一次伤害。
        /// </summary>
        public abstract void ApplyHit(float damage);

        /// <summary>
        /// 告诉敌人“现在应该攻击了”（由 CombatManager 或 AI 调用）。
        /// 具体攻击逻辑由子类决定。
        /// </summary>
        public abstract void TriggerAttack();
    }
}

