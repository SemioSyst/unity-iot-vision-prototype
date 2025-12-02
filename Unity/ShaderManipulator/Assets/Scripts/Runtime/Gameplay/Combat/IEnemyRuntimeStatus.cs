namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 用来表示敌人当前的运行时状态
    /// </summary>
    public interface IEnemyRuntimeStatus : IRuntimeStatus
    {
        int EnemyId { get; }
        EnemyPhase Phase { get; }
        float PhaseProgress01 { get; }

        float Health { get; }
        bool IsAlive { get; }
    }
}

