using System;
using UnityEngine;

/// <summary>
/// 顶层 hands 消息，映射 Python 发送的 JSON。
/// </summary>
namespace ShaderDuel.Core
{
    [Serializable]
    public class HandsMessage
    {
        public string type;        // "hands"
        public string version;     // "1"
        public double timestamp;   // Unix 时间戳（秒）
        public int frame_id;
        public string source;      // "mediapipe_hands"
        public HandsPayload payload;
    }

    [Serializable]
    public class HandsPayload
    {
        public ImageInfo image;
        public HandData[] hands;
    }

    [Serializable]
    public class HandData
    {
        public string handedness;      // "left" / "right"
        public Landmark[] landmarks;   // 21 个关键点
    }

    /// <summary>
    /// 图像尺寸信息（struct：小而纯数据）
    /// </summary>
    [Serializable]
    public struct ImageInfo
    {
        public int width;
        public int height;
    }

    /// <summary>
    /// 单个关键点坐标（struct：值类型，避免误改引用）
    /// </summary>
    [Serializable]
    public struct Landmark
    {
        // 归一化坐标（0~1，MediaPipe 原始输出）
        public float x;
        public float y;
        public float z;

        // 像素坐标（在 Python 里根据 image.width/height 算出来的，不代表unity的屏幕大小）
        public int px;
        public int py;
    }
}