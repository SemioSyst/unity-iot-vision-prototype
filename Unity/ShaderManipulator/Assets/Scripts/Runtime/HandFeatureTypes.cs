using UnityEngine;

namespace ShaderDuel.Hands
{
    // 手指弯曲状态
    public enum FingerState
    {
        Unknown = 0,
        Extended = 1,
        Bent = 2
    }

    // 掌心法线的大方向
    public enum PalmNormalOrientation
    {
        Unknown = 0,
        Forward = 1,
        Backward = 2,
        Up = 3,
        Down = 4,
        Left = 5,
        Right = 6,
    }

    // 掌内“上方向”（palmTangentUp / 攻击主轴）的大方向
    public enum PalmTangentOrientation
    {
        Unknown = 0,
        Forward = 1,
        Backward = 2,
        Up = 3,
        Down = 4,
        Left = 5,
        Right = 6,
    }

    // 哪只手
    public enum Handedness
    {
        Unknown = 0,
        Left = 1,
        Right = 2
    }

    /// <summary>
    /// 单只手的一帧特征。
    /// </summary>
    public struct HandFeatures
    {
        public Handedness Handedness;
        public bool IsTracked;         // 这一帧有没有检测到
        public int FramesSinceSeen;   // 连续丢了几帧（0 表示当前就看到了）

        // 空间信息（屏幕空间坐标，0-1 或者你喜欢的坐标系）
        public Vector3 PalmCenter;       // 手心位置（归一化 or 世界/屏幕）
        public Vector3 PalmRight;        // 掌内 x 轴
        public Vector3 PalmNormal;       // 掌面法线
        public Vector3 PalmTangentUp;    // 掌内 y 轴（攻击主轴）

        // 运动信息
        public Vector3 PalmVelocity;
        public float PalmSpeed;

        public Vector3 IndexTipPos;
        public Vector3 IndexTipVelocity;
        public float IndexTipSpeed;

        // 姿态语义
        public PalmNormalOrientation NormalOrientation;
        public PalmTangentOrientation TangentOrientation;

        // 手指弯曲状态（0–4: 拇指→小拇指）
        public FingerState ThumbState;
        public FingerState IndexState;
        public FingerState MiddleState;
        public FingerState RingState;
        public FingerState PinkyState;

        // 简单的组合姿态
        public bool IsFist;
        public bool IsOpenPalm;
        public bool IsPinch;   // 先留接口，初版可以不算
    }

    /// <summary>
    /// 当前帧的双手整体信息（全局）。
    /// </summary>
    public struct GlobalHandFeatures
    {
        public HandFeatures LeftHand;
        public HandFeatures RightHand;

        public bool HasLeftHand;
        public bool HasRightHand;
        public float TwoHandDistance;
        public Vector3 TwoHandCenter;
    }
}

