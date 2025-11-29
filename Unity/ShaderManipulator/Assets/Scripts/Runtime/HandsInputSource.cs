using System;
using UnityEngine;

namespace ShaderDuel.Core
{
    /// <summary>
    /// 手部输入源：
    /// - 从 WsClient 获取最新 JSON；
    /// - 用 HandsMessageParser 解析为 HandsMessage；
    /// - 缓存最近一帧的 hand 状态；
    /// - 通过 IHandsInput 接口提供查询能力。
    /// </summary>
    public class HandsInputSource : MonoBehaviour, IHandsInput
    {
        [Header("Dependencies")]
        [Tooltip("负责 WebSocket 接收 JSON 的 WsClient 组件")]
        [SerializeField] private WsClient _wsClient;

        [Header("Debug")]
        [Tooltip("是否在收到新帧时打印简单日志")]
        [SerializeField] private bool _logOnUpdate = false;

        // 最近一帧完整消息
        private HandsMessage _latestMessage;
        private int _latestFrameId = -1;
        private double _latestTimestamp;
        private bool _hasData = false;

        // 为方便主手查询做的缓存
        private HandData _primaryHandCache;
        private bool _hasPrimaryHand = false;

        // IHandsInput 接口实现
        public bool HasData => _hasData;
        public int HandCount => (_latestMessage?.payload?.hands)?.Length ?? 0;
        public int LatestFrameId => _latestFrameId;
        public double LatestTimestamp => _latestTimestamp;

        /// <summary>
        /// 每帧从 WsClient 拉取最新 JSON，尝试解析并更新内部状态。
        /// </summary>
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

            // 尝试解析为 HandsMessage
            if (!HandsMessageParser.TryParse(json, out var msg))
            {
                return; // 不是 hands 消息或解析失败
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

            RebuildPrimaryHandCache();

            if (_logOnUpdate)
            {
                Debug.Log($"[HandsInputSource] 新帧 id={_latestFrameId}, hands={HandCount}");
            }

            // 这里以后可以加 OnHandsUpdated 事件广播（若需要）
        }

        /// <summary>
        /// 重新计算“主手”缓存。
        /// 策略：优先右手，其次左手，最后取第一只。
        /// </summary>
        private void RebuildPrimaryHandCache()
        {
            // 重置缓存，默认无主手
            _hasPrimaryHand = false;

            // payload没有装载手部数据就直接返回
            var hands = _latestMessage?.payload?.hands;
            if (hands == null || hands.Length == 0)
            {
                return;
            }

            HandData candidate = null;

            // 先找右手
            foreach (var hand in hands)
            {
                if (hand == null) continue;
                if (string.Equals(hand.label, "right", StringComparison.OrdinalIgnoreCase))
                {
                    candidate = hand;
                    break;
                }
            }

            // 没有右手就找左手
            if (candidate == null)
            {
                foreach (var hand in hands)
                {
                    if (hand == null) continue;
                    if (string.Equals(hand.label, "left", StringComparison.OrdinalIgnoreCase))
                    {
                        candidate = hand;
                        break;
                    }
                }
            }

            // 还没有就取第一只非空
            if (candidate == null)
            {
                foreach (var hand in hands)
                {
                    if (hand != null)
                    {
                        candidate = hand;
                        break;
                    }
                }
            }

            // 若有候选手则更新缓存，标记有效
            // 没有候选手说明全是 null，保持无主手状态
            if (candidate != null)
            {
                _primaryHandCache = candidate;
                _hasPrimaryHand = true;
            }
        }

        // =============================
        // IHandsInput 接口实现部分
        // =============================

        public bool TryGetHand(int index, out HandData hand)
        {
            hand = null;

            // 获取手数组
            var hands = _latestMessage?.payload?.hands;
            if (!_hasData || hands == null || index < 0 || index >= hands.Length)
            {
                return false;
            }

            // 返回指定索引的手数据（左手或右手）
            hand = hands[index];
            return hand != null;
        }

        public bool TryGetPrimaryHand(out HandData hand)
        {
            // 若无数据或无主手，直接返回 false
            hand = null;
            if (!_hasData || !_hasPrimaryHand)
                return false;

            // 返回缓存的主手数据
            hand = _primaryHandCache;
            return hand != null;
        }

        public bool TryGetLeftHand(out HandData hand)
        {
            hand = null;

            var hands = _latestMessage?.payload?.hands;
            if (!_hasData || hands == null)
                return false;

            // 遍历查找左手
            foreach (var h in hands)
            {
                // 跳过空手数据
                if (h == null) continue;
                // 比较 handedness 字段，若是左手则获取数据并返回 true
                if (string.Equals(h.label, "left", StringComparison.OrdinalIgnoreCase))
                {
                    hand = h;
                    return true;
                }
            }

            // 若未找到左手则返回 false
            return false;
        }

        public bool TryGetRightHand(out HandData hand)
        {
            hand = null;

            var hands = _latestMessage?.payload?.hands;
            if (!_hasData || hands == null)
                return false;

            // 遍历查找右手
            foreach (var h in hands)
            {
                // 跳过空手数据
                if (h == null) continue;
                // 比较 handedness 字段，若是右手则获取数据并返回 true
                if (string.Equals(h.label, "right", StringComparison.OrdinalIgnoreCase))
                {
                    hand = h;
                    return true;
                }
            }

            // 若未找到右手则返回 false
            return false;
        }
    }
}
