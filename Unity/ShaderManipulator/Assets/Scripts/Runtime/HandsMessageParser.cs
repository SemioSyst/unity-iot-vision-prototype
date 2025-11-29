using System;
using UnityEngine;

/// <summary>
/// 只包含 type 的轻量头部，用于预判消息类型。
/// </summary>
[Serializable]
public class MessageHeader
{
    public string type;
}

/// <summary>
/// 负责把 JSON 文本解析为 HandsMessage 的工具类。
/// </summary>
public static class HandsMessageParser
{
    /// <summary>
    /// 尝试将一段 JSON 解析为 HandsMessage。
    /// - 先解析头部检查 type == "hands"
    /// - 再解析完整结构
    /// 解析失败返回 false，不抛异常。
    /// </summary>
    public static bool TryParse(string json, out HandsMessage msg)
    {
        msg = null;

        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            // 先解析头部，过滤掉非 hands 消息
            var header = JsonUtility.FromJson<MessageHeader>(json);
            if (header == null || string.IsNullOrEmpty(header.type))
                return false;

            if (!string.Equals(header.type, "hands", StringComparison.OrdinalIgnoreCase))
                return false;

            // 再解析完整的 hands 消息
            // 通过 JsonUtility 解析为自定义类 HandsMessage
            // 注意：JsonUtility 在解析数组时，如果数组字段在 JSON 中缺失，会被解析为 null
            msg = JsonUtility.FromJson<HandsMessage>(json);
            if (msg == null || msg.payload == null)
                return false;

            // 一些基本健壮性检查
            if (msg.payload.hands == null || msg.payload.hands.Length == 0)
                return true; // 没有检测到手，也算合法消息

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HandsMessageParser] 解析失败: {ex.Message}");
            msg = null;
            return false;
        }
    }
}
