namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 用来表示玩家当前的运行时状态
    /// </summary>
    public interface IPlayerRuntimeStatus : IRuntimeStatus
    {
        float Health { get; }
        bool IsAlive { get; }

        bool IsGuarding { get; }
        float GuardStrength01 { get; }

        bool IsStunned { get; }
        bool IsCastingSpell { get; }
    }
}

