using System;
using UnityEngine;
using ShaderDuel.Hands; // 如果你把 HandFeatureTypes 放到 namespace 里了

/// <summary>
/// 从 IHandsInput (HandsInputSource) 获取 HandData，
/// 转换为稳定的 HandFeatures / GlobalHandFeatures。
/// </summary>
public class HandFeatureExtractor : MonoBehaviour
{
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
    [SerializeField] private float _fingerExtendedThreshold = 0.08f;
    [SerializeField] private float _fingerBentThreshold = 0.04f;

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

        // 局部 x：小拇指 -> 食指
        Vector3 palmRight = (indexMcp - pinkyMcp).normalized;

        // 掌面法线：WRIST->INDEX_MCP 与 WRIST->PINKY_MCP 的叉积
        Vector3 v1 = (indexMcp - wrist).normalized;
        Vector3 v2 = (pinkyMcp - wrist).normalized;
        Vector3 palmNormal = Vector3.Cross(v1, v2).normalized;

        // 掌内 y：tangentUp = cross(normal, right)
        Vector3 palmTangentUp = Vector3.Cross(palmNormal, palmRight).normalized;

        // ---- 2. 平滑 + 防翻面 ----
        if (prevOpt.HasValue)
        {
            // 有上一帧数据，做平滑
            var prev = prevOpt.Value;

            // 位置平滑
            palmCenter = Vector3.Lerp(prev.PalmCenter, palmCenter, _positionSmoothing);

            // 方向平滑 + 防翻面
            palmNormal = SmoothDirection(prev.PalmNormal, palmNormal, _orientationSmoothing);
            palmRight = SmoothDirection(prev.PalmRight, palmRight, _orientationSmoothing);
            palmTangentUp = SmoothDirection(prev.PalmTangentUp, palmTangentUp, _orientationSmoothing);
        }

        features.PalmCenter = palmCenter;
        features.PalmNormal = palmNormal;
        features.PalmRight = palmRight;
        features.PalmTangentUp = palmTangentUp;

        // ---- 3. 运动：Palm & Index Tip ----
        int INDEX_TIP = 8;
        Vector3 indexTip = lm[INDEX_TIP];

        if (prevOpt.HasValue)
        {
            var prev = prevOpt.Value;
            float dt = Mathf.Max(Time.deltaTime, 1e-5f);

            Vector3 palmVel = (palmCenter - prev.PalmCenter) / dt;
            Vector3 idxVel = (indexTip - prev.IndexTipPos) / dt;

            palmVel = Vector3.Lerp(prev.PalmVelocity, palmVel, 0.5f);
            idxVel = Vector3.Lerp(prev.IndexTipVelocity, idxVel, 0.5f);

            features.PalmVelocity = palmVel;
            features.PalmSpeed = palmVel.magnitude;
            features.IndexTipPos = indexTip;
            features.IndexTipVelocity = idxVel;
            features.IndexTipSpeed = idxVel.magnitude;
        }
        else
        {
            features.PalmVelocity = Vector3.zero;
            features.PalmSpeed = 0f;
            features.IndexTipPos = indexTip;
            features.IndexTipVelocity = Vector3.zero;
            features.IndexTipSpeed = 0f;
        }

        // ---- 4. 手指弯曲状态 ----
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

    // 平滑方向 + 防翻面
    private static Vector3 SmoothDirection(Vector3 prev, Vector3 current, float alpha)
    {
        if (prev == Vector3.zero)
            return current.normalized;

        if (Vector3.Dot(prev, current) < 0f)
            current = -current;

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
        float dr = Vector3.Dot(dir, r);

        float db = -df;
        float dd = -du;
        float dl = -dr;

        float maxAbs = Mathf.Max(
            Mathf.Abs(df), Mathf.Abs(db),
            Mathf.Abs(du), Mathf.Abs(dd),
            Mathf.Abs(dr), Mathf.Abs(dl)
        );

        if (maxAbs < _orientationMinDot)
            return 0; // Unknown

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

