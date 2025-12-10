using System.Text;
using UnityEngine;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 简单的战斗调试 HUD：
    /// - 显示玩家血量
    /// - 显示所有 Dummy 敌人的 AttackCharge01
    /// - 可选：在 Console 输出同样的信息
    /// </summary>
    public class DebugCombatHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatDriver _driver;

        [Header("Options")]
        [SerializeField] private bool _printToConsole = false;
        [SerializeField] private float _consolePrintInterval = 1f;
        private float _consoleTimer;

        private void Update()
        {
            if (_driver == null || _driver.Context == null)
                return;

            if (_printToConsole)
            {
                _consoleTimer += Time.deltaTime;
                if (_consoleTimer >= _consolePrintInterval)
                {
                    Debug.Log(BuildDebugString());
                    _consoleTimer = 0f;
                }
            }
        }

        private void OnGUI()
        {
            if (_driver == null || _driver.Context == null)
                return;

            string text = BuildDebugString();

            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 600, 400), text);
        }

        private string BuildDebugString()
        {
            var ctx = _driver.Context;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== COMBAT UI ===");

            // 玩家血量
            var player = ctx.Player;
            sb.AppendLine($"Player Health : {player.Health:F1}");

            // 敌人信息（特别是 AttackCharge01）
            sb.AppendLine();
            sb.AppendLine("--- ENEMIES ---");

            foreach (var enemy in ctx.Enemies)
            {
                if (enemy is DummyEnemyRuntimeStatus dummy)
                {
                    sb.AppendLine(
                        $"Enemy {dummy.EnemyId} | HP: {dummy.Health:F1} | " +
                        $"Charge: {dummy.AttackCharge01:F2} | Alive: {dummy.IsAlive} | " +
                        $"HitThisFrame: {dummy.AttackHitThisFrame}"
                    );
                }
                else
                {
                    // 未来如果有新敌人类型，仍然能打印基本信息
                    sb.AppendLine(
                        $"Enemy (Other Type) | HP: {enemy.Health:F1} | Alive: {enemy.IsAlive}"
                    );
                }
            }

            return sb.ToString();
        }
    }
}

