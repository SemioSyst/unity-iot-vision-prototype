using System.Collections.Generic;
using UnityEngine;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 定期把当前所有 RunningSpell 的 RuntimeStatus 打印到 Console。
    /// 主要用于观察 Shader 侧需要的参数是否正确更新。
    /// </summary>
    public sealed class SpellRuntimeDebugPrinter : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField]
        private SpellOrchestrator _orchestrator;

        [Header("Debug")]
        [Tooltip("多少秒打印一次状态。")]
        [SerializeField]
        private float _logInterval = 0.5f;

        [Tooltip("当没有任何运行中法术时是否也打印一条提示。")]
        [SerializeField]
        private bool _logWhenEmpty = false;

        private float _timeSinceLastLog;

        private void Reset()
        {
            // 尝试自动找场景里的 SpellOrchestrator，方便快速挂脚本
            if (_orchestrator == null)
            {
                _orchestrator = Object.FindFirstObjectByType<SpellOrchestrator>();
            }
        }

        private void Update()
        {
            if (_orchestrator == null)
            {
                return;
            }

            _timeSinceLastLog += Time.deltaTime;
            if (_timeSinceLastLog < _logInterval)
            {
                return;
            }
            _timeSinceLastLog = 0f;

            IReadOnlyList<RunningSpell> spells = _orchestrator.RunningSpells;
            if (spells == null || spells.Count == 0)
            {
                if (_logWhenEmpty)
                {
                    Debug.Log("[SpellRuntimeDebug] No running spells.");
                }
                return;
            }

            foreach (var spell in spells)
            {
                var status = spell.RuntimeStatus;

                if (status == null)
                {
                    Debug.Log($"[SpellRuntimeDebug] {spell.GetType().Name}: RuntimeStatus is null.");
                    continue;
                }

                // 对 EnergyWall 的专门打印（目前你只有这一种法术）
                if (status is EnergyWallRuntimeStatus ew)
                {
                    Debug.Log(
                        $"[SpellRuntimeDebug] EnergyWall | " +
                        $"Phase={ew.Phase} " +
                        $"Progress={ew.PhaseProgress01:F2} " +
                        $"Center={ew.WallCenterUV} " +
                        $"Size={ew.WallSizeUV} " +
                        $"Rot={ew.RotationDeg:F1} " +
                        $"Activation={ew.Activation01:F2}"
                    );
                }
                else
                {
                    // 通用兜底：至少把类型打出来
                    Debug.Log(
                        $"[SpellRuntimeDebug] {spell.GetType().Name} -> status type = {status.GetType().Name}"
                    );
                }
            }
        }
    }
}

