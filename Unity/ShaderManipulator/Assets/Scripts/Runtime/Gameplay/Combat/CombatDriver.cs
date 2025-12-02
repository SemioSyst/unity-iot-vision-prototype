using System.Collections.Generic;
using UnityEngine;

namespace ShaderDuel.Gameplay
{
    public sealed class CombatDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpellOrchestrator _spellOrchestrator;

        [Header("Initial Enemies")]
        [Tooltip("开局要生成的敌人配置列表，可以配置多种类型和数量。")]
        [SerializeField] private List<EnemySpawnConfig> _initialEnemyConfigs = new();

        private CombatManager _combat;

        public CombatManager Combat => _combat;
        public CombatRuntimeContext Context => _combat?.Context;

        private void Awake()
        {
            if (_spellOrchestrator == null)
            {
                _spellOrchestrator = Object.FindAnyObjectByType<SpellOrchestrator>();
                if (_spellOrchestrator == null)
                {
                    Debug.LogError("[CombatDriver] 找不到 SpellOrchestrator 引用，请在 Inspector 中手动绑定。");
                    enabled = false;
                    return;
                }
            }

            var player = new PlayerCombatController();
            var initialEnemies = BuildInitialEnemiesFromConfig();

            _combat = new CombatManager(_spellOrchestrator, player, initialEnemies);
        }

        private List<EnemyController> BuildInitialEnemiesFromConfig()
        {
            var list = new List<EnemyController>();
            int nextId = 1;

            foreach (var cfg in _initialEnemyConfigs)
            {
                for (int i = 0; i < cfg.Count; i++)
                {
                    var enemy = CreateEnemyController(cfg.Kind, nextId++, cfg.InitialHealth);
                    if (enemy != null)
                    {
                        list.Add(enemy);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// 根据配置的敌人类型创建对应的 EnemyController。
        /// 以后新增敌人类型只需要在这里加 case。
        /// </summary>
        private EnemyController CreateEnemyController(EnemyKind kind, int id, float hp)
        {
            switch (kind)
            {
                case EnemyKind.Dummy:
                    return new DummyEnemyController(id, hp);

                // 以后加新类型时：
                // case EnemyKind.Shooter:
                //     return new ShooterEnemyController(id, hp);
                //
                // case EnemyKind.Boss:
                //     return new BossEnemyController(id, hp);

                default:
                    Debug.LogWarning($"[CombatDriver] 未知的敌人类型: {kind}");
                    return null;
            }
        }

        private void Update()
        {
            if (_combat == null) return;
            _combat.Tick(Time.deltaTime);
        }
    }
}
