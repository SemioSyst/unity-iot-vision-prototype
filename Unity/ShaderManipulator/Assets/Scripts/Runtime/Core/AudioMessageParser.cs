using System;
using UnityEngine;

namespace ShaderDuel.Core
{
    /// <summary>
    /// 负责把 JSON 文本解析为 AudioMessage 的工具类。
    /// 逻辑和 HandsMessageParser 基本一致，只是 type 换成 "audio_level"。
    /// </summary>
    public static class AudioMessageParser
    {
        /// <summary>
        /// 尝试将一段 JSON 解析为 AudioMessage。
        /// - 先解析头部检查 type == "audio_level"
        /// - 再解析完整结构
        /// 解析失败返回 false，不抛异常。
        /// </summary>
        public static bool TryParse(string json, out AudioMessage msg)
        {
            msg = null;

            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                // 先解析头部，过滤掉非 audio_level 消息
                var header = JsonUtility.FromJson<MessageHeader>(json);
                if (header == null || string.IsNullOrEmpty(header.type))
                    return false;

                if (!string.Equals(header.type, "audio_level", StringComparison.OrdinalIgnoreCase))
                    return false;

                // 再解析完整的 audio_level 消息
                msg = JsonUtility.FromJson<AudioMessage>(json);
                if (msg == null || msg.payload == null)
                    return false;

                // payload 基本检查
                if (msg.payload.level == null)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AudioMessageParser] 解析失败: {ex.Message}");
                msg = null;
                return false;
            }
        }
    }
}

