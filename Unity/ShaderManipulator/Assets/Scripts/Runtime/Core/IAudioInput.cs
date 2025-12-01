using UnityEngine;

namespace ShaderDuel.Core
{
    /// <summary>
    /// 抽象的“音频输入源”接口。
    /// 任何数据来源（Python、录制回放、本地采集等）
    /// 只要实现这个接口，就可以被其它系统查询当前音量信息。
    /// </summary>
    public interface IAudioInput
    {
        /// <summary> 当前是否至少收到过一帧有效数据。 </summary>
        bool HasData { get; }

        /// <summary> 最近一帧的 frame_id（没有数据时可返回 -1）。 </summary>
        int LatestFrameId { get; }

        /// <summary> 最近一帧的时间戳（Unix 秒）。 </summary>
        double LatestTimestamp { get; }

        /// <summary>
        /// 获取最近一帧的完整 AudioMessage。
        /// </summary>
        bool TryGetLatestMessage(out AudioMessage message);

        /// <summary>
        /// 获取最近一帧的音量信息（dbfs / rms）。
        /// </summary>
        bool TryGetLatestLevel(out AudioLevelInfo level);

        /// <summary>
        /// 只关心分贝时的便捷接口。
        /// </summary>
        bool TryGetLatestDbfs(out float dbfs);
    }
}

