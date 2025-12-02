namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 玩家当前战斗状态的快照。
    /// 由 PlayerCombatController 每帧写入。
    /// </summary>
    public sealed class PlayerRuntimeStatus : IPlayerRuntimeStatus
    {
        public float Health { get; set; }
        public bool IsAlive { get; set; }

        public bool IsGuarding { get; set; }
        public float GuardStrength01 { get; set; }

        public bool IsStunned { get; set; }
        public bool IsCastingSpell { get; set; }
    }
}

