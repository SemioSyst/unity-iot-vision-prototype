using UnityEngine;

namespace ShaderDuel.Audio
{
    /// <summary>
    /// 单帧的音频特征。
    /// 对标 HandFeatures 的结构，但内容更简单。
    /// </summary>
    [System.Serializable]
    public struct AudioFeatures
    {
        // 原始输入
        public float Dbfs;       // 分贝（dBFS）
        public float Rms;        // 原始归一化音量

        // 平滑值
        public float SmoothedDbfs;
        public float SmoothedRms;

        // 速度（变化率）
        public float DbfsDelta;
        public float RmsDelta;

        // 基本状态标志
        public bool IsLoud;      // dBFS > 阈值
        public bool IsSilent;    // dBFS < 某低阈值

        // 帧数据
        public int FrameId;
        public double Timestamp;

        // 距离上次观测帧多少帧（类似 FramesSinceSeen）
        public int FramesSinceSeen;
        public bool IsTracked;
    }

    /// <summary>
    /// 全局音频特征（例如可以扩展 stereo 通道）
    /// 当前系统只支持单通道，所以只保留一个字段。
    /// </summary>
    [System.Serializable]
    public struct GlobalAudioFeatures
    {
        public AudioFeatures Main;
        public bool HasAudio;
    }
}

