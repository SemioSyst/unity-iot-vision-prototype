using UnityEngine;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 用来表示敌人当前所处的阶段
    /// </summary>
    public enum EnemyPhase
    {
        Inactive,   // 完全没东西（槽位空）
        Spawning,   // 出现动画 / 渐显
        Alive,      // 存在中（待命）
        Attacking,  // 正在攻击（给 shader 做攻击特效）
        Dying,      // 死亡动画中
    }

}
