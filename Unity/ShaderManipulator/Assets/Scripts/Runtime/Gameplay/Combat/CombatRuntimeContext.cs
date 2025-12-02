using System.Collections.Generic;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 当前战斗局面的只读快照。
    /// 由 CombatManager 在每帧末尾刷新，渲染 / UI / 调试层只读不改。
    /// </summary>
    public sealed class CombatRuntimeContext
    {
        /// <summary>玩家战斗状态。</summary>
        public IPlayerRuntimeStatus Player { get; internal set; }

        /// <summary>当前场景中所有敌人的状态列表（不同类型统一视作 IEnemyRuntimeStatus）。</summary>
        public IReadOnlyList<IEnemyRuntimeStatus> Enemies { get; internal set; }
            = System.Array.Empty<IEnemyRuntimeStatus>();

        /// <summary>当前所有运行中的法术状态列表。</summary>
        public IReadOnlyList<ISpellRuntimeStatus> ActiveSpells { get; internal set; }
            = System.Array.Empty<ISpellRuntimeStatus>();
    }
}

