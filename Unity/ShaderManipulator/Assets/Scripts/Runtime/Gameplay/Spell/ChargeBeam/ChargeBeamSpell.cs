using UnityEngine;
using ShaderDuel.Hands;
using ShaderDuel.Audio;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 蓄力光炮的阶段（视觉 / 调试使用）。
    /// </summary>
    public enum ChargeBeamPhase
    {
        Armed,      // 准备 / 蓄力阶段
        Firing,     // 正在发射光炮
        Recovery    // 收尾 / 冷却
    }

    /// <summary>
    /// 蓄力光炮的运行时输出。
    /// 视觉层可以通过 ISpellRuntimeStatus 强转为这个类型来读更多字段。
    /// </summary>
    public sealed class ChargeBeamRuntimeStatus : ISpellRuntimeStatus
    {
        /// <summary>法术 Id（固定为 "charge_beam"）。</summary>
        public string SpellId { get; set; }

        /// <summary>当前阶段。</summary>
        public ChargeBeamPhase Phase { get; set; }

        /// <summary>当前阶段内部的 0–1 进度。</summary>
        public float PhaseProgress01 { get; set; }

        /// <summary>当前蓄力进度（0–1）。</summary>
        public float ChargingProgress01 { get; set; }

        /// <summary>光炮起点在屏幕空间的归一化坐标（0–1）。</summary>
        public Vector2 BeamOriginUV { get; set; }

        /// <summary>光炮在屏幕空间的宽度 / 长度（归一化 0–1）。</summary>
        /// x = 宽度，y = 长度。
        public Vector2 BeamSizeUV { get; set; }

        /// <summary>光炮朝向角度（度数，0 = 以屏幕 x 轴为基准）。</summary>
        public float RotationDeg { get; set; }

        /// <summary>视觉“强度”/“可见度”（0–1）。</summary>
        public float Activation01 { get; set; }
    }

    /// <summary>
    /// 蓄力光炮运行时 FSM：
    /// 阶段：Armed（蓄力） → Firing（发射） → Recovery（冷却）。
    /// </summary>
    public sealed class ChargeBeamSpell : RunningSpell
    {
        private enum Phase
        {
            Armed,
            Firing,
            Recovery
        }

        // 当前阶段
        private Phase _phase = Phase.Armed;
        // 当前阶段内经过的时间
        private float _timeInPhase;

        // Armed 阶段：最大等待时间（蓄力阶段最大持续时间）
        private const float ArmedMaxDuration = 20f; // 比能量墙更长，玩家可以慢慢蓄

        // 丢手宽限时间：短时间内丢手不当失败，超过才算放弃
        private const float ArmedLostGraceTime = 0.25f;

        // Armed 阶段累计的“丢手时间”
        private float _armedLostElapsed = 0f;

        // Firing 阶段的最大持续时间（光炮最长发射时间）
        private const float MaxFiringDuration = 3.0f;

        // 掌心不再保持发射姿态时，允许“挽回”的宽限时间
        private const float LoseFiringGraceTime = 0.2f;

        // Firing 阶段中“丢姿态”的累计时间
        private float _noFiringPoseElapsed = 0f;

        // Recovery 阶段总时长
        private const float RecoveryDuration = 0.5f;

        // 蓄力相关
        // 完全蓄满所需的“嘈杂”时间（秒），按音频 IsLoud 叠加
        private const float FullChargeLoudSeconds = 2.0f;

        // 当前蓄力进度 [0,1]
        private float _chargingProgress01 = 0f;

        private readonly ChargeBeamRuntimeStatus _status;

        public ChargeBeamSpell(
            SpellDefinition definition,
            SpellOrchestrator orchestrator,
            HandTrackState[] boundHands)
            : base(definition, orchestrator, boundHands)
        {
            _status = new ChargeBeamRuntimeStatus
            {
                SpellId = definition.Id,
                Phase = ChargeBeamPhase.Armed,
                PhaseProgress01 = 0f,
                ChargingProgress01 = 0f,
                BeamOriginUV = new Vector2(0.5f, 0.5f),
                BeamSizeUV = new Vector2(0.1f, 1.0f),
                RotationDeg = 0f,
                Activation01 = 0f
            };

            RuntimeStatus = _status;
        }

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

                case Phase.Firing:
                    UpdateFiring(deltaTime, handFeatures, audioFeatures);
                    break;

                case Phase.Recovery:
                    UpdateRecovery(deltaTime);
                    break;
            }
        }

        #region Armed（蓄力阶段）

        private void UpdateArmed(
            float deltaTime,
            GlobalHandFeatures handFeatures,
            GlobalAudioFeatures audioFeatures)
        {
            _status.Phase = ChargeBeamPhase.Armed;
            _status.PhaseProgress01 = Mathf.Clamp01(_timeInPhase / ArmedMaxDuration);

            // --------------------------------------------------
            // 1）处理“丢手”宽限逻辑（基本沿用能量墙）
            // --------------------------------------------------
            bool hasLeft = handFeatures.HasLeftHand;
            bool hasRight = handFeatures.HasRightHand;

            bool anyHandMissing = !hasLeft || !hasRight;
            bool bothHandsMissing = !hasLeft && !hasRight;

            if (anyHandMissing)
            {
                _armedLostElapsed += deltaTime;
            }
            else
            {
                _armedLostElapsed = 0f;
            }

            if (bothHandsMissing && _armedLostElapsed >= ArmedLostGraceTime)
            {
                // 玩家基本放弃了这个蓄力，进入收尾而不是直接 Cancel
                StartRecovery();
                return;
            }

            // --------------------------------------------------
            // 2）Armed 阶段整体超时：一直不进入发射姿势
            // --------------------------------------------------
            if (_timeInPhase >= ArmedMaxDuration)
            {
                StartRecovery();
                return;
            }

            // --------------------------------------------------
            // 3）蓄力逻辑：音频 IsLoud 时增加 ChargingProgress01
            // --------------------------------------------------

            // TODO: 根据 GlobalAudioFeatures 实际字段名调整 IsLoud
            bool isLoud = audioFeatures.Main.IsLoud;
            if (isLoud && FullChargeLoudSeconds > 0f)
            {
                _chargingProgress01 += deltaTime / FullChargeLoudSeconds;
                _chargingProgress01 = Mathf.Clamp01(_chargingProgress01);
            }

            // 这里暂时不做蓄力衰减，方便玩家提前蓄好再发。
            _status.ChargingProgress01 = _chargingProgress01;

            // 蓄力阶段可以用 Activation01 表示整体“能量感”
            _status.Activation01 = _chargingProgress01;

            // 位置也可以先跟着双手中心走，方便做聚能特效
            Vector3 center = handFeatures.TwoHandCenter;
            Vector2 uv = new Vector2(
                Mathf.Clamp01(center.x),
                Mathf.Clamp01(center.y));
            _status.BeamOriginUV = uv;

            // 宽度可以轻微受双手距离和蓄力影响
            float distance = handFeatures.TwoHandDistance;
            float widthBase = Mathf.Clamp01(distance * 0.8f);
            float widthCharged = Mathf.Lerp(widthBase * 0.5f, widthBase * 1.5f, _chargingProgress01);
            _status.BeamSizeUV = new Vector2(widthCharged, 1.0f);
            _status.RotationDeg = 0f;

            // --------------------------------------------------
            // 4）检测发射姿势：进入 Firing
            // （沿用能量墙的“双手张开 + 掌心向前”）
            // --------------------------------------------------
            if (IsFiringActPose(handFeatures))
            {
                _phase = Phase.Firing;
                _timeInPhase = 0f;
                _noFiringPoseElapsed = 0f;
                return;
            }
        }

        #endregion

        #region Firing（发射阶段）

        /// <summary>
        /// 判断是否处于“光炮发射启动姿势”：张开双手 + 掌心向前。
        /// </summary>
        private static bool IsFiringActPose(GlobalHandFeatures features)
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
        /// 判断是否处于“光炮维持姿势”：任意一只手张开 + 掌心向前。
        /// </summary>
        private static bool IsFiringPose(GlobalHandFeatures features)
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

        private void UpdateFiring(
            float deltaTime,
            GlobalHandFeatures handFeatures,
            GlobalAudioFeatures audioFeatures)
        {
            // 安全阈值：最长持续时间
            if (_timeInPhase >= MaxFiringDuration)
            {
                StartRecovery();
                return;
            }

            // 只要还有任意一只手保持“张开 + 掌心向前”，就持续发射
            if (IsFiringPose(handFeatures))
            {
                _noFiringPoseElapsed = 0f;
            }
            else
            {
                // 手势中断，开始累计“宽限时间”
                _noFiringPoseElapsed += deltaTime;
                if (_noFiringPoseElapsed >= LoseFiringGraceTime)
                {
                    StartRecovery();
                    return;
                }
            }

            _status.Phase = ChargeBeamPhase.Firing;
            _status.PhaseProgress01 = Mathf.Clamp01(_timeInPhase / MaxFiringDuration);

            // Firing 阶段光炮完全可见
            _status.Activation01 = 1f;

            // 起点仍然用手的中心
            Vector3 center = handFeatures.TwoHandCenter;
            Vector2 uv = new Vector2(
                Mathf.Clamp01(center.x),
                Mathf.Clamp01(center.y));
            _status.BeamOriginUV = uv;

            // 宽度主要由蓄力程度决定，长度占满屏幕
            float baseWidth = 0.05f;
            float maxWidth = 0.35f;
            float width = Mathf.Lerp(baseWidth, maxWidth, _chargingProgress01);
            _status.BeamSizeUV = new Vector2(width, 1.0f);

            // 简单先固定为 0 度（水平光束），之后可以根据手心朝向调整
            _status.RotationDeg = 0f;
        }

        private void StartRecovery()
        {
            _phase = Phase.Recovery;
            _timeInPhase = 0f;
        }

        #endregion

        #region Recovery（收尾阶段）

        private void UpdateRecovery(float deltaTime)
        {
            _status.Phase = ChargeBeamPhase.Recovery;

            float t = Mathf.Clamp01(_timeInPhase / RecoveryDuration);
            _status.PhaseProgress01 = t;

            // 让 Activation01 在收尾阶段从 1 慢慢衰减到 0，
            // 方便 shader 做消散动画。
            _status.Activation01 = 1f - t;

            if (_timeInPhase >= RecoveryDuration)
            {
                IsCompleted = true;
            }
        }

        #endregion

        /// <summary>
        /// 法术结束时的清理工作。
        /// </summary>
        public override void OnEnd()
        {
            // 清掉这个法术用到的计时 key，防止下次触发受上次残留影响
            foreach (var hand in BoundHands)
            {
                string key = "ChargeBeam:ArmingPose";
                ConditionTimer.Reset(key);
            }

            Debug.Log("[ChargeBeam] Ended.");
        }
    }
}
