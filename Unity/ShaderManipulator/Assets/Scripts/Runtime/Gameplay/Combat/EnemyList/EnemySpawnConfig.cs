using System;
using UnityEngine;

namespace ShaderDuel.Gameplay
{
    public enum EnemyKind
    {
        Dummy = 0,
        // 以后要增加类型就在这里加：
        // Shooter,
        // Boss,
    }

    [Serializable]
    public struct EnemySpawnConfig
    {
        public EnemyKind Kind;

        [Min(1)]
        public int Count;

        [Min(1f)]
        public float InitialHealth;
    }
}

