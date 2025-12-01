using System;
using UnityEngine;

namespace ShaderDuel.Core
{
    /// <summary>
    /// 音频输入源：
    /// - 从 WsClient 获取最新 JSON；
    /// - 用 AudioMessageParser 解析为 AudioMessage；
    /// - 缓存最近一帧的音频状态；
    /// - 通过 IAudioInput 接口提供查询能力。
    /// </summary>
    public class AudioInputSource : MonoBehaviour, IAudioInput
    {
        [Header("Dependencies")]
        [Tooltip("负责 WebSocket 接收 JSON 的 WsClient 组件")]
        [SerializeField] private WsClient _wsClient;

        [Header("Debug")]
        [Tooltip("是否在收到新帧时打印简单日志")]
        [SerializeField] private bool _logOnUpdate = false;

        // 最近一帧完整消息
        private AudioMessage _latestMessage;
        private int _latestFrameId = -1;
        private double _latestTimestamp;
        private bool _hasData = false;

        // IAudioInput 接口实现
        public bool HasData => _hasData;
        public int LatestFrameId => _latestFrameId;
        public double LatestTimestamp => _latestTimestamp;

        private void Update()
        {
            if (_wsClient == null)
            {
                return;
            }

            // 从 WsClient 拿当前最新 JSON
            if (!_wsClient.TryGetLatestJson(out var json) || string.IsNullOrEmpty(json))
            {
                return;
            }

            // 尝试解析为 AudioMessage
            if (!AudioMessageParser.TryParse(json, out var msg))
            {
                return; // 不是 audio_level 消息或解析失败
            }

            // 去重：如果 frame_id 没变，说明已经处理过这帧
            if (msg.frame_id == _latestFrameId)
            {
                return;
            }

            // 更新内部状态
            _latestMessage = msg;
            _latestFrameId = msg.frame_id;
            _latestTimestamp = msg.timestamp;
            _hasData = true;

            if (_logOnUpdate)
            {
                var lvl = _latestMessage.payload.level;
                Debug.Log($"[AudioInputSource] 新帧 id={_latestFrameId}, dbfs={lvl.dbfs:F1}, rms={lvl.rms:F3}");
            }
        }

        // =============================
        // IAudioInput 接口实现部分
        // =============================

        public bool TryGetLatestMessage(out AudioMessage message)
        {
            message = null;
            if (!_hasData || _latestMessage == null)
                return false;

            message = _latestMessage;
            return true;
        }

        public bool TryGetLatestLevel(out AudioLevelInfo level)
        {
            level = default;

            if (!_hasData || _latestMessage?.payload?.level == null)
                return false;

            level = _latestMessage.payload.level;
            return true;
        }

        public bool TryGetLatestDbfs(out float dbfs)
        {
            dbfs = 0f;

            if (!_hasData || _latestMessage?.payload?.level == null)
                return false;

            dbfs = _latestMessage.payload.level.dbfs;
            return true;
        }
    }
}

