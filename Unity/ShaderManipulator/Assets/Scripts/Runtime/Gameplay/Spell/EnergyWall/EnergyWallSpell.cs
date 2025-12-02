using UnityEngine;
using ShaderDuel.Hands;
using ShaderDuel.Audio;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 能量墙护盾的阶段（对视觉层 / 调试都比较直观）。
    /// </summary>
    public enum WallShieldPhase
    {
        Armed,      // 准备完成，等待张开双手
        Channeling, // 墙已展开、正在维持
        Recovery    // 收尾 / 冷却
    }

    /// <summary>
    /// 能量墙护盾的运行时输出。
    /// 视觉层可以通过 ISpellRuntimeStatus 强转为这个类型来读更多字段。
    /// </summary>
    public sealed class EnergyWallRuntimeStatus : ISpellRuntimeStatus
    {
        /// <summary>法术 Id（固定为 "energy_wall"）。</summary>
        public string SpellId { get; set; }

        /// <summary>当前墙的阶段。</summary>
        public WallShieldPhase Phase { get; set; }

        /// <summary>当前阶段内部的 0–1 进度。</summary>
        public float PhaseProgress01 { get; set; }

        /// <summary>墙中心在屏幕空间的归一化坐标（0–1）。</summary>
        public Vector2 WallCenterUV { get; set; }

        /// <summary>墙在屏幕空间的宽高（归一化 0–1）。</summary>
        public Vector2 WallSizeUV { get; set; }

        /// <summary>墙的朝向角度（度数，0 = 以屏幕 x 轴为基准）。</summary>
        public float RotationDeg { get; set; }

        /// <summary>视觉“强度”/“可见度”（0–1），大致等于是否在 Channeling & Recovery 内。</summary>
        public float Activation01 { get; set; }
    }

    /// <summary>
    /// 能量墙护盾的运行时 FSM：
    /// 阶段：Armed → Channeling → Recovery。
    /// </summary>
    public sealed class EnergyWallSpell : RunningSpell
    {
        private enum Phase
        {
            Armed,
            Channeling,
            Recovery
        }

        // 当前阶段
        private Phase _phase = Phase.Armed;
        // 当前阶段内经过的时间
        private float _timeInPhase;

        // 最大维持时间（防止永久不结束）
        private const float MaxChannelTime = 10f;

        // 掌心不再向前时，允许“挽回”的宽限时间
        private const float LoseChannelGraceTime = 0.3f;

        // Recovery 阶段总时长
        private const float RecoveryDuration = 0.5f;

        // 掌心离开朝前姿态后累计的时间
        private float _noChannelElapsed;

        // 最大等待时间：在 Armed 最多等多久（玩家摆启动姿势）才算超时
        private const float ArmedMaxDuration = 8.0f;   // 例如 8 秒，可再调

        // 丢手宽限时间：短时间内丢手不当失败，超过才算放弃
        private const float ArmedLostGraceTime = 0.25f; // 例如 0.25 秒，可再调

        // Armed 阶段累计的“丢手时间”
        private float _armedLostElapsed = 0f;

        private readonly EnergyWallRuntimeStatus _status;

        /// <summary>
        /// 构造函数，获取法术定义、调度器和绑定的手状态。
        /// 初始化运行时状态。
        /// </summary>
        /// <param name="definition"></param>
        /// <param name="orchestrator"></param>
        /// <param name="boundHands"></param>
        public EnergyWallSpell(
            SpellDefinition definition,
            SpellOrchestrator orchestrator,
            HandTrackState[] boundHands)
            : base(definition, orchestrator, boundHands)
        {
            // 初始化运行时状态
            _status = new EnergyWallRuntimeStatus
            {
                SpellId = definition.Id,
                Phase = WallShieldPhase.Armed,
                PhaseProgress01 = 0f,
                WallCenterUV = new Vector2(0.5f, 0.5f),
                WallSizeUV = new Vector2(0.6f, 0.5f),
                RotationDeg = 0f,
                Activation01 = 0f
            };

            // 绑定运行时状态接口
            // 由于 EnergyWallRuntimeStatus 是引用类型，
            // 所以后续直接更新 _status 即可反映到外部接口 RuntimeStatus 上。
            RuntimeStatus = _status;
        }

        /// <summary>
        /// 每帧更新法术状态机。
        /// </summary>
        /// <param name="deltaTime"></param>
        /// <param name="handFeatures"></param>
        /// <param name="audioFeatures"></param>
        public override void Tick(
            float deltaTime,
            GlobalHandFeatures handFeatures,
            GlobalAudioFeatures audioFeatures)
        {
            if (IsCompleted || IsCancelled)
                return;

            _timeInPhase += deltaTime;

            switch (_phase)
            {
                case Phase.Armed:
                    UpdateArmed(deltaTime, handFeatures, audioFeatures);
                    break;

                case Phase.Channeling:
                    UpdateChanneling(deltaTime, handFeatures, audioFeatures);
                    break;

                case Phase.Recovery:
                    UpdateRecovery(deltaTime);
                    break;
            }
        }

        #region Armed

        /// <summary>
        /// Armed：准备完毕，等待“双手张开 + 掌心向前”。
        /// </summary>
        private void UpdateArmed(
            float deltaTime,
            GlobalHandFeatures handFeatures,
            GlobalAudioFeatures audioFeatures)
        {
            _status.Phase = WallShieldPhase.Armed;
            _status.PhaseProgress01 = 0f;
            _status.Activation01 = 0f;

            // --------------------------------------------------
            // 1）处理“丢手”宽限逻辑
            // --------------------------------------------------
            bool hasLeft = handFeatures.HasLeftHand;
            bool hasRight = handFeatures.HasRightHand;

            bool anyHandMissing = !hasLeft || !hasRight;
            bool bothHandsMissing = !hasLeft && !hasRight;

            if (anyHandMissing)
            {
                // 只要有手在丢，就累计丢失时间
                _armedLostElapsed += deltaTime;
            }
            else
            {
                // 双手都稳定可见，丢失计时清零
                _armedLostElapsed = 0f;
            }

            // 如果两只手都看不到，并且持续超过宽限时间，
            // 视为玩家已经“放弃准备”，直接进入 Recovery，而不是 Cancel。
            if (bothHandsMissing && _armedLostElapsed >= ArmedLostGraceTime)
            {
                StartRecovery();    // 用你现有的 StartRecovery，走完整收尾动画
                return;
            }

            // --------------------------------------------------
            // 2）Armed 阶段整体超时：一直不摆启动姿势
            // --------------------------------------------------
            if (_timeInPhase >= ArmedMaxDuration)
            {
                // 超时同样进入 Recovery（法术失败但有收尾）
                StartRecovery();
                return;
            }

            // --------------------------------------------------
            // 3）检测启动姿势：进入 Channeling
            // --------------------------------------------------
            if (IsChannelActPose(handFeatures))
            {
                _phase = Phase.Channeling;
                _timeInPhase = 0f;
                _noChannelElapsed = 0f;
                _armedLostElapsed = 0f;
                return;
            }
        }

        #endregion

        #region Channeling

        /// <summary>
        /// 判断是否处于“能量墙开启姿势”：张开双手 + 掌心向前。
        /// </summary>
        private static bool IsChannelActPose(GlobalHandFeatures features)
        {
            bool leftOk = false;
            bool rightOk = false;

            if (features.HasLeftHand)
            {
                var lh = features.LeftHand;
                leftOk = lh.IsTracked &&
                         lh.IsOpenPalm &&
                         lh.NormalOrientation == PalmNormalOrientation.Forward;
            }

            if (features.HasRightHand)
            {
                var rh = features.RightHand;
                rightOk = rh.IsTracked &&
                          rh.IsOpenPalm &&
                          rh.NormalOrientation == PalmNormalOrientation.Forward;
            }

            return leftOk && rightOk;
        }

        /// <summary>
        /// 判断是否处于“能量墙维持姿势”：任意一只手张开 + 掌心向前。
        /// </summary>
        private static bool IsChannelPose(GlobalHandFeatures features)
        {
            bool leftOk = false;
            bool rightOk = false;

            if (features.HasLeftHand)
            {
                var lh = features.LeftHand;
                leftOk = lh.IsTracked &&
                         lh.IsOpenPalm &&
                         lh.NormalOrientation == PalmNormalOrientation.Forward;
            }

            if (features.HasRightHand)
            {
                var rh = features.RightHand;
                rightOk = rh.IsTracked &&
                          rh.IsOpenPalm &&
                          rh.NormalOrientation == PalmNormalOrientation.Forward;
            }

            return leftOk || rightOk;
        }

        private void UpdateChanneling(
            float deltaTime,
            GlobalHandFeatures handFeatures,
            GlobalAudioFeatures audioFeatures)
        {
            // 安全阈值：最长持续时间
            if (_timeInPhase >= MaxChannelTime)
            {
                StartRecovery();
                return;
            }

            // 只要还有任意一只手保持“张开 + 掌心向前”，就持续维持
            if (IsChannelPose(handFeatures))
            {
                _noChannelElapsed = 0f;
            }
            else
            {
                // 手势中断，开始累计“宽限时间”
                _noChannelElapsed += deltaTime;
                if (_noChannelElapsed >= LoseChannelGraceTime)
                {
                    StartRecovery();
                    return;
                }
            }

            // ---- 更新 RuntimeStatus，供 Shader 使用 ----

            _status.Phase = WallShieldPhase.Channeling;
            _status.PhaseProgress01 = Mathf.Clamp01(_timeInPhase / MaxChannelTime);
            _status.Activation01 = 1f;

            // 1. 位置：用双手中心作为墙中心（0–1 空间）
            Vector3 center = handFeatures.TwoHandCenter;
            Vector2 uv = new Vector2(
                Mathf.Clamp01(center.x),
                Mathf.Clamp01(center.y));
            _status.WallCenterUV = uv;

            // 2. 尺寸：根据双手距离估一个宽度，高度先给常数
            float distance = handFeatures.TwoHandDistance;
            float width = Mathf.Clamp01(distance * 1.5f); // 视情况调
            const float height = 0.5f;                    // 占屏幕一半高，可之后再调

            _status.WallSizeUV = new Vector2(width, height);

            // 3. 朝向：先简单固定为 0 度（水平墙）
            _status.RotationDeg = 0f;
        }

        private void StartRecovery()
        {
            _phase = Phase.Recovery;
            _timeInPhase = 0f;
        }

        #endregion

        #region Recovery

        private void UpdateRecovery(float deltaTime)
        {
            _status.Phase = WallShieldPhase.Recovery;

            float t = Mathf.Clamp01(_timeInPhase / RecoveryDuration);
            _status.PhaseProgress01 = t;

            // 让 Activation01 在收尾阶段从 1 慢慢衰减到 0，
            // 方便 shader 做消散动画等。
            _status.Activation01 = 1f - t;

            if (_timeInPhase >= RecoveryDuration)
            {
                IsCompleted = true;
            }
        }

        #endregion

        /// <summary>
        /// 注意：法术结束时的清理工作。
        /// 需要对应 Definition 里的设计。
        /// </summary>
        public override void OnEnd()
        {
            // 清掉这个法术用到的计时 key，防止下次触发受上次残留影响
            foreach (var hand in BoundHands)
            {
                string key = "EnergyWall:ArmingPose";
                ConditionTimer.Reset(key);
            }
            Debug.Log("[EnergyWall] Ended.");

            // 这里暂时不需要通知 Shader 层，后面真法术再补
        }
    }
}
