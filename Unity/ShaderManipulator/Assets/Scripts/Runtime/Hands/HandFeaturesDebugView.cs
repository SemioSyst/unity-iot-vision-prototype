using UnityEngine;
namespace ShaderDuel.Hands
{
    /// <summary>
    /// 手势特征调试可视化：
    /// - 定时在 Console 打印当前左右手的关键特征；
    /// - 在 Scene 视图里用 Gizmos 画出掌心位置与局部坐标轴。
    /// 仅用于调试，不参与正式游戏逻辑。
    /// </summary>
    public class HandFeaturesDebugView : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("场景中的 HandFeatureExtractor（一般挂在 AppRoot 上）")]
        [SerializeField] private HandFeatureExtractor _extractor;

        // ========= 日志部分 =========
        [Header("Logging")]
        [Tooltip("是否打印左手信息")]
        [SerializeField] private bool _logLeftHand = true;

        [Tooltip("是否打印右手信息")]
        [SerializeField] private bool _logRightHand = true;

        [Tooltip("当手完全没被跟踪时是否也打印（IsTracked = false）")]
        [SerializeField] private bool _logWhenNotTracked = false;

        [Tooltip("日志打印间隔（秒），避免刷屏）")]
        [SerializeField] private float _logInterval = 0.5f;

        private float _logTimer;

        // ========= Gizmos 部分 =========
        [Header("Gizmos")]
        [Tooltip("是否在 Scene 视图中绘制 Gizmos")]
        [SerializeField] private bool _drawGizmos = true;

        [Tooltip("把归一化坐标放大到世界坐标的缩放系数")]
        [SerializeField] private float _worldScale = 3f;

        [Tooltip("整体平移偏移，用来把手的 gizmo 挪到你能看见的位置")]
        [SerializeField] private Vector3 _worldOffset = new Vector3(0, 1, 0);

        [Tooltip("掌心小球半径")]
        [SerializeField] private float _palmRadius = 0.05f;

        [Tooltip("局部坐标轴长度")]
        [SerializeField] private float _axisLength = 0.25f;

        private void Update()
        {
            if (_extractor == null) return;

            _logTimer += Time.deltaTime;
            if (_logTimer < _logInterval) return;
            _logTimer = 0f;

            var g = _extractor.Global;

            // 左手
            if (_logLeftHand)
            {
                if (g.HasLeftHand || _logWhenNotTracked)
                {
                    LogHand("Left", g.LeftHand, g.HasLeftHand);
                }
            }

            // 右手
            if (_logRightHand)
            {
                if (g.HasRightHand || _logWhenNotTracked)
                {
                    LogHand("Right", g.RightHand, g.HasRightHand);
                }
            }
        }

        private void LogHand(string label, HandFeatures hand, bool hasHandFlag)
        {
            // hasHandFlag = “这一帧是否真的检测到了手”
            // hand.IsTracked  = 特征内部标记（延续历史帧时为 false）
            string presence = hasHandFlag ? "Detected" : "NotDetected";
            string tracked = hand.IsTracked ? "Tracked" : "Lost";

            string orientN = hand.NormalOrientation.ToString();
            string orientT = hand.TangentOrientation.ToString();

            string shape =
                $"Fist={hand.IsFist}, Open={hand.IsOpenPalm}, Pinch={hand.IsPinch}";

            string speed =
                $"PalmSpeed={hand.PalmSpeed:F2}, IndexSpeed={hand.IndexTipSpeed:F2}";

            Debug.Log(
                $"[HandDebug] {label} " +
                $"Presence={presence}, Tracked={tracked}, " +
                $"Normal={orientN}, Tangent={orientT}, " +
                $"{shape}, {speed}"
            );
        }

        // ---------------- Gizmos 可视化 ----------------

        private void OnDrawGizmos()
        {
            if (!_drawGizmos) return;
            if (_extractor == null) return;

            var g = _extractor.Global;

            if (g.HasLeftHand)
            {
                DrawHandGizmos(g.LeftHand, Color.cyan);
            }

            if (g.HasRightHand)
            {
                DrawHandGizmos(g.RightHand, Color.magenta);
            }
        }

        private void DrawHandGizmos(HandFeatures hand, Color baseColor)
        {
            // 把 0~1 的归一化坐标“拉伸 + 平移”到世界坐标，方便在 Scene 里看
            Vector3 worldPalm =
                new Vector3(hand.PalmCenter.x, hand.PalmCenter.y, -hand.PalmCenter.z)
                * _worldScale + _worldOffset;

            // 掌心小球
            Gizmos.color = baseColor;
            Gizmos.DrawSphere(worldPalm, _palmRadius);

            // 局部坐标轴：Right / TangentUp / Normal
            Vector3 right = hand.PalmRight.normalized;
            Vector3 up = hand.PalmTangentUp.normalized;
            Vector3 normal = hand.PalmNormal.normalized;

            float L = _axisLength;

            // X：右（红-ish）
            Gizmos.color = Color.red;
            Gizmos.DrawLine(worldPalm, worldPalm + right * L);

            // Y：tangentUp（绿）
            Gizmos.color = Color.green;
            Gizmos.DrawLine(worldPalm, worldPalm + up * L);

            // Z：normal（蓝）
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(worldPalm, worldPalm + normal * L);
        }
    }
}