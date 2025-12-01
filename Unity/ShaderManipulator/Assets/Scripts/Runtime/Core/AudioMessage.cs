using System;
using UnityEngine;

namespace ShaderDuel.Core
{
    /// <summary>
    /// 顶层 audio_level 消息，映射 Python 发送的 JSON。
    /// 结构参考 make_message + build_audio_payload：
    /// {
    ///   "type": "audio_level",
    ///   "version": "1",
    ///   "timestamp": ...,
    ///   "frame_id": ...,
    ///   "source": "c922_mic",
    ///   "payload": {
    ///       "device": { "name": "...", "sample_rate": 48000 },
    ///       "window": { "duration_sec": 0.05 },
    ///       "level":  { "dbfs": -20.3, "rms": 0.09 }
    ///   }
    /// }
    /// </summary>
    [Serializable]
    public class AudioMessage
    {
        public string type;        // "audio_level"
        public string version;     // "1"
        public double timestamp;   // Unix 时间戳（秒）
        public int frame_id;
        public string source;      // "c922_mic"
        public AudioPayload payload;
    }

    [Serializable]
    public class AudioPayload
    {
        public AudioDeviceInfo device;
        public AudioWindowInfo window;
        public AudioLevelInfo level;
    }

    [Serializable]
    public class AudioDeviceInfo
    {
        public string name;

        // 注意字段名要和 JSON 里的 "sample_rate" 一致
        public int sample_rate;
    }

    [Serializable]
    public class AudioWindowInfo
    {
        // 字段名保持 "duration_sec"
        public float duration_sec;
    }

    [Serializable]
    public class AudioLevelInfo
    {
        // 分贝（dBFS），一般是负数，0 表示满幅度
        public float dbfs;

        // 原始归一化 RMS（0~1）
        public float rms;
    }
}

