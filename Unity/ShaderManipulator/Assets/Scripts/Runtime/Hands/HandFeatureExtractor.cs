using System;
using UnityEngine;
using ShaderDuel.Core;   // 为了 IHandsInput / HandData 等

namespace ShaderDuel.Hands
{
    /// <summary>
    /// 从 IHandsInput (HandsInputSource) 获取 HandData，
    /// 转换为稳定的 HandFeatures / GlobalHandFeatures。
    /// </summary>
    public class HandFeatureExtractor : MonoBehaviour
    {
        // Inspector 面板配置

        [Header("Input Source")]
        [Tooltip("实现了 IHandsInput 的组件，一般就是 HandsInputSource")]
        [SerializeField] private MonoBehaviour _inputBehaviour;

        private IHandsInput _handsInput;

        [Header("Smoothing")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _positionSmoothing = 0.3f;

        [Range(0.01f, 1f)]
        [SerializeField] private float _orientationSmoothing = 0.25f;

        [Header("Finger / Orientation Thresholds")]
        [SerializeField] private float _fingerExtendedThreshold = 0.15f;
        [SerializeField] private float _fingerBentThreshold = 0.1f;

        [SerializeField] private float _orientationMinDot = 0.35f;

        // 对外暴露：当前帧的全局手部特征
        public GlobalHandFeatures Global { get; private set; }

        // 上一帧缓存，用于速度和滤波
        private HandFeatures _prevLeft;
        private HandFeatures _prevRight;
        private bool _hasPrevLeft;
        private bool _hasPrevRight;

        private Camera _mainCam;

        private void Awake()
        {
            // 获取主摄像机引用
            _mainCam = Camera.main;

            if (_inputBehaviour != null)
            {
                _handsInput = _inputBehaviour as IHandsInput;
                if (_handsInput == null)
                {
                    Debug.LogError("[HandFeatureExtractor] 绑定的组件没有实现 IHandsInput 接口。");
                }
            }
        }
        //private float _logTimer; // 测试用的计时器，用于控制日志打印频率，非正式，正式debug工具请用 HandFeaturesDebugView
        private void Update()
        {
            if (_handsInput == null || !_handsInput.HasData)
            {
                UpdateNoHands();
                return;
            }

            GlobalHandFeatures result = default;

            // 左手
            if (_handsInput.TryGetLeftHand(out var leftHandData) && leftHandData != null)
            {
                var lm = ExtractNormalizedLandmarks(leftHandData);
                if (lm != null && lm.Length >= 21)
                {
                    result.LeftHand = ComputeHandFeatures(
                        lm,
                        Handedness.Left,
                        _hasPrevLeft ? (HandFeatures?)_prevLeft : null
                    );
                    result.HasLeftHand = true;
                    // 更新上一帧缓存为当前帧
                    _prevLeft = result.LeftHand;
                    _hasPrevLeft = true;

                    //LogHand("Left", result.LeftHand, result.HasLeftHand);
                }
                else
                {
                   // Debug.LogWarning("[HandFeatureExtractor] 左手 landmarks 数据不足，无法计算特征。");
                }
            }
            else
            {
                // 没看到左手：延续上一帧状态，标记为未跟踪
                if (_hasPrevLeft)
                {
                    _prevLeft.FramesSinceSeen++;
                    _prevLeft.IsTracked = false;
                    result.LeftHand = _prevLeft;
                }
                //Debug.Log("[HandFeatureExtractor] 未检测到左手。");
            }

            // 右手
            if (_handsInput.TryGetRightHand(out var rightHandData) && rightHandData != null)
            {
                var lm = ExtractNormalizedLandmarks(rightHandData);
                if (lm != null && lm.Length >= 21)
                {
                    result.RightHand = ComputeHandFeatures(
                        lm,
                        Handedness.Right,
                        _hasPrevRight ? (HandFeatures?)_prevRight : null
                    );
                    result.HasRightHand = true;
                    // 更新上一帧缓存为当前帧
                    _prevRight = result.RightHand;
                    _hasPrevRight = true;

                    //LogHand("Right", result.RightHand, result.HasRightHand);
                }
                else
                {
                    //Debug.LogWarning("[HandFeatureExtractor] 右手 landmarks 数据不足，无法计算特征。");
                }
            }
            else
            {
                // 没看到右手：延续上一帧状态，标记为未跟踪
                if (_hasPrevRight)
                {
                    _prevRight.FramesSinceSeen++;
                    _prevRight.IsTracked = false;
                    result.RightHand = _prevRight;
                }
                //Debug.Log("[HandFeatureExtractor] 未检测到右手。");
            }

            // 双手全局信息
            if (result.HasLeftHand && result.HasRightHand)
            {
                result.TwoHandCenter =
                    0.5f * (result.LeftHand.PalmCenter + result.RightHand.PalmCenter);
                result.TwoHandDistance =
                    Vector3.Distance(result.LeftHand.PalmCenter, result.RightHand.PalmCenter);
            }

            Global = result;
        }

        /*
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
        */

        private void UpdateNoHands()
        {
            // 简单处理：如果这一帧输入源没新数据，就把上一次状态标记为“没看到手”
            GlobalHandFeatures result = default;

            if (_hasPrevLeft)
            {
                _prevLeft.FramesSinceSeen++;
                _prevLeft.IsTracked = false;
                result.LeftHand = _prevLeft;
                result.HasLeftHand = false; // 这一帧实际没检测到，只是沿用历史
            }

            if (_hasPrevRight)
            {
                _prevRight.FramesSinceSeen++;
                _prevRight.IsTracked = false;
                result.RightHand = _prevRight;
                result.HasRightHand = false;
            }

            Global = result;
        }

        /// <summary>
        /// 从 21 个归一化点位计算一只手的所有特征。
        /// </summary>
        private HandFeatures ComputeHandFeatures(
            Vector3[] lm,
            Handedness handedness,
            HandFeatures? prevOpt)
        {
            // 初始化结果储存容器
            var features = new HandFeatures
            {
                Handedness = handedness,
                IsTracked = true,
                FramesSinceSeen = 0
            };

            if (lm == null || lm.Length < 21)
                return features;

            // ---- 1. 基本点位：WRIST, INDEX_MCP, PINKY_MCP ----
            Vector3 wrist = lm[0];
            Vector3 indexMcp = lm[5];
            Vector3 pinkyMcp = lm[17];

            Vector3 palmCenter = (wrist + indexMcp + pinkyMcp) / 3f;


            // 建立局部掌心坐标系：palmRight, palmNormal, palmTangentUp
            Vector3 vIndex = (indexMcp - wrist).normalized;
            Vector3 vPinky = (pinkyMcp - wrist).normalized;

            Vector3 palmRight;
            Vector3 palmNormal;
            Vector3 palmTangentUp;

            if (handedness == Handedness.Right)
            {
                // 右手：x 轴指向“食指 -> 小指”或“掌的右边”
                palmRight = (pinkyMcp - indexMcp).normalized;

                // normal = vIndex × vPinky
                palmNormal = Vector3.Cross(vIndex, vPinky).normalized;
            }
            else // Left
            {
                // 左手：保持“局部 x 轴是手掌右侧”的语义，所以反过来
                palmRight = (indexMcp - pinkyMcp).normalized;

                // normal = vPinky × vIndex（交换顺序）
                palmNormal = Vector3.Cross(vPinky, vIndex).normalized;
            }

            // y 轴：tangentUp = normal × right
            palmTangentUp = Vector3.Cross(palmNormal, palmRight).normalized;

            // ---- 2. 平滑 + 防翻面 ----
            if (prevOpt.HasValue)
            {
                // 有上一帧数据，做平滑
                var prev = prevOpt.Value;

                // 位置平滑
                palmCenter = Vector3.Lerp(prev.PalmCenter, palmCenter, _positionSmoothing);

                // 方向平滑
                palmNormal = SmoothDirection(prev.PalmNormal, palmNormal, _orientationSmoothing);
                palmRight = SmoothDirection(prev.PalmRight, palmRight, _orientationSmoothing);
                palmTangentUp = SmoothDirection(prev.PalmTangentUp, palmTangentUp, _orientationSmoothing);
            }

            // 写入结果
            features.PalmCenter = palmCenter;
            features.PalmNormal = palmNormal;
            features.PalmRight = palmRight;
            features.PalmTangentUp = palmTangentUp;

            // ---- 3. 运动：Palm & Index Tip ----
            // 获取食指指尖landmark
            int INDEX_TIP = 8;
            Vector3 indexTip = lm[INDEX_TIP];

            if (prevOpt.HasValue)
            {
                // 有上一帧数据，计算速度
                var prev = prevOpt.Value;
                float dt = Mathf.Max(Time.deltaTime, 1e-5f);

                // 计算掌心和食指指尖的速度
                Vector3 palmVel = (palmCenter - prev.PalmCenter) / dt;
                Vector3 idxVel = (indexTip - prev.IndexTipPos) / dt;

                // 速度平滑
                palmVel = Vector3.Lerp(prev.PalmVelocity, palmVel, 0.5f);
                idxVel = Vector3.Lerp(prev.IndexTipVelocity, idxVel, 0.5f);

                // 写入结果
                features.PalmVelocity = palmVel;
                features.PalmSpeed = palmVel.magnitude;
                features.IndexTipPos = indexTip;
                features.IndexTipVelocity = idxVel;
                features.IndexTipSpeed = idxVel.magnitude;
            }
            else
            {
                // 没有上一帧数据，速度设为零
                features.PalmVelocity = Vector3.zero;
                features.PalmSpeed = 0f;
                features.IndexTipPos = indexTip;
                features.IndexTipVelocity = Vector3.zero;
                features.IndexTipSpeed = 0f;
            }

            // ---- 4. 手指弯曲状态 ----
            // 通过指尖到掌心的距离判断手指状态
            // 越远越可能是伸直，越近越可能是弯曲
            // 具体阈值在 Inspector 面板配置
            // 注意：这里用的是归一化坐标系下的距离
            // 以后可以改进为更复杂的角度判断
            // 计算并写入各手指状态，每根手指都有 Extended / Bent / Unknown 三种状态
            ComputeFingerStates(lm, ref features);

            // ---- 5. 组合姿态 ----
            features.IsFist = IsFist(ref features);
            features.IsOpenPalm = IsOpenPalm(ref features);
            features.IsPinch = false; // 以后实现

            // ---- 6. 姿态枚举：Normal & Tangent ----
            if (_mainCam != null)
            {
                features.NormalOrientation =
                    (PalmNormalOrientation)ClassifyOrientationEnum(palmNormal, _mainCam);

                features.TangentOrientation =
                    (PalmTangentOrientation)ClassifyOrientationEnum(palmTangentUp, _mainCam);
            }
            else
            {
                features.NormalOrientation = PalmNormalOrientation.Unknown;
                features.TangentOrientation = PalmTangentOrientation.Unknown;
            }

            return features;
        }

        /// <summary>
        /// 把 HandData 里的 21 个点转换成 Vector3[n]，x/y/z 使用归一化坐标。
        /// </summary>
        private Vector3[] ExtractNormalizedLandmarks(HandData hand)
        {
            if (hand == null || hand.landmarks == null || hand.landmarks.Length == 0)
            {
                return null;
            }

            var src = hand.landmarks;
            var lm = new Vector3[src.Length];

            for (int i = 0; i < src.Length; i++)
            {
                // x/y/z 已经是 [0,1] 归一化坐标，对应 MediaPipe 的输出
                lm[i] = new Vector3(src[i].x, src[i].y, src[i].z);
            }

            return lm;
        }

        // 平滑方向
        private static Vector3 SmoothDirection(Vector3 prev, Vector3 current, float alpha)
        {
            // 如果没有上一帧，直接归一化返回当前方向
            if (prev == Vector3.zero)
                return current.normalized;

            // 为防止真实翻面被错认成异常值，暂时注释掉这段代码
            // 考虑未来使用严格的异常值剔除函数替代
            
            // 防止翻面：如果夹角大于约101度，就反转当前方向
            if (Vector3.Dot(prev, current) < -0.2f)
                current = -current;
            

            // 线性插值平滑
            var smoothed = Vector3.Lerp(prev, current, alpha);
            return smoothed.normalized;
        }

        // 输出一个“最大 dot 是哪个方向”的离散枚举（Forward/Back/Up/Down/Left/Right/Unknown）
        private int ClassifyOrientationEnum(Vector3 dir, Camera cam)
        {
            if (dir == Vector3.zero) return 0; // Unknown

            dir = dir.normalized;
            Vector3 f = cam.transform.forward;
            Vector3 u = cam.transform.up;
            Vector3 r = cam.transform.right;

            float df = Vector3.Dot(dir, f);
            float du = Vector3.Dot(dir, u);
            float dl = Vector3.Dot(dir, r);

            float db = -df;
            float dd = -du;
            float dr = -dl;

            float maxAbs = Mathf.Max(
                Mathf.Abs(df), Mathf.Abs(db),
                Mathf.Abs(du), Mathf.Abs(dd),
                Mathf.Abs(dr), Mathf.Abs(dl)
            );

            // 如果最大值都不够大，认为方向不明确
            // 阈值在 Inspector 面板配置，控制严格度
            if (maxAbs < _orientationMinDot)
                return 0; // Unknown

            // 判断是哪个方向
            if (Mathf.Approximately(maxAbs, Mathf.Abs(df)))
                return df > 0 ? 1 : 2; // Forward / Backward

            if (Mathf.Approximately(maxAbs, Mathf.Abs(du)))
                return du > 0 ? 3 : 4; // Up / Down

            // 否则左右
            return dr > 0 ? 6 : 5; // Right : Left
        }

        // ---- 手指状态（距离手心） ----
        private void ComputeFingerStates(Vector3[] lm, ref HandFeatures f)
        {
            Vector3 palm = f.PalmCenter;

            float thumbDist = Vector3.Distance(lm[4], palm);
            float indexDist = Vector3.Distance(lm[8], palm);
            float middleDist = Vector3.Distance(lm[12], palm);
            float ringDist = Vector3.Distance(lm[16], palm);
            float pinkyDist = Vector3.Distance(lm[20], palm);

            f.ThumbState = ClassifyFingerDistance(thumbDist);
            f.IndexState = ClassifyFingerDistance(indexDist);
            f.MiddleState = ClassifyFingerDistance(middleDist);
            f.RingState = ClassifyFingerDistance(ringDist);
            f.PinkyState = ClassifyFingerDistance(pinkyDist);
        }

        private FingerState ClassifyFingerDistance(float dist)
        {
            if (dist > _fingerExtendedThreshold) return FingerState.Extended;
            if (dist < _fingerBentThreshold) return FingerState.Bent;
            return FingerState.Unknown;
        }

        private bool IsFist(ref HandFeatures f)
        {
            int bentCount = 0;
            if (f.ThumbState == FingerState.Bent) bentCount++;
            if (f.IndexState == FingerState.Bent) bentCount++;
            if (f.MiddleState == FingerState.Bent) bentCount++;
            if (f.RingState == FingerState.Bent) bentCount++;
            if (f.PinkyState == FingerState.Bent) bentCount++;
            return bentCount >= 4;
        }

        private bool IsOpenPalm(ref HandFeatures f)
        {
            int extCount = 0;
            if (f.ThumbState == FingerState.Extended) extCount++;
            if (f.IndexState == FingerState.Extended) extCount++;
            if (f.MiddleState == FingerState.Extended) extCount++;
            if (f.RingState == FingerState.Extended) extCount++;
            if (f.PinkyState == FingerState.Extended) extCount++;
            return extCount >= 4;
        }
    }
}
