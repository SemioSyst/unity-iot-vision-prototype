using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Unity 侧 WebSocket 客户端：
/// - 后台 Task 连接 Python 服务器，持续接收 JSON 文本；
/// - 把消息放进一个小缓冲队列；
/// - 主线程在 Update 里拉取最新一条供其它脚本使用。
/// </summary>
public class WsClient : MonoBehaviour
{
    [Header("WebSocket Settings")]
    [Tooltip("Python 服务器地址")]
    public string serverUrl = "ws://127.0.0.1:8765";

    [Tooltip("消息缓冲队列的最大容量，超过则丢掉最旧的")]
    public int maxBufferedMessages = 3;

    // 后台 WebSocket 对象和取消令牌
    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;

    // 接收循环的 Task（方便调试/查看状态）
    private Task _receiveLoopTask;

    // 线程安全的消息队列：后台线程写入，主线程读取
    private readonly ConcurrentQueue<string> _messageQueue = new();

    // 只在主线程访问的“当前最新一条消息”
    private string _latestJson;

    /// <summary>
    /// 对外暴露一个只读属性，方便其它脚本查询连接状态。
    /// </summary>
    public bool IsConnected =>
        _ws != null && _ws.State == WebSocketState.Open;

    private void Awake()
    {
        // 确保失焦时 Unity 也继续跑（比如 Python 在后台）
        Application.runInBackground = true;

        _cts = new CancellationTokenSource();
        _ws = new ClientWebSocket();

        // 启动异步连接+接收，不等待（_ = 表示“有意忽略返回值”）
        _receiveLoopTask = RunWebSocketAsync(_cts.Token);
    }

    /// <summary>
    /// 主异步流程：连接服务器，然后进入接收循环。
    /// </summary>
    private async Task RunWebSocketAsync(CancellationToken ct)
    {
        try
        {
            var uri = new Uri(serverUrl);
            Debug.Log($"[WsClient] 尝试连接 {serverUrl} ...");
            await _ws.ConnectAsync(uri, ct);
            Debug.Log("[WsClient] 已连接到服务器.");

            // 进入接收循环
            await ReceiveLoopAsync(_ws, ct);
        }
        catch (OperationCanceledException)
        {
            // 正常关闭时会走这里，不需要报错
            Debug.Log("[WsClient] 连接任务被取消.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WsClient] WebSocket 异常: {ex}");
        }
    }

    /// <summary>
    /// 持续接收服务器发来的文本消息，并放入队列。
    /// </summary>
    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        // 单次消息最大 64KB，够你 JSON 用了；不够可以调大。
        var buffer = new byte[64 * 1024];

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            var segment = new ArraySegment<byte>(buffer);

            WebSocketReceiveResult result;
            int totalBytes = 0;

            try
            {
                // 一条消息可能被拆成多帧，这里做一个简单的组装
                using var ms = new System.IO.MemoryStream();
                do
                {
                    result = await ws.ReceiveAsync(segment, ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("[WsClient] 服务器请求关闭连接.");
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                            "Closing as requested", ct);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                    totalBytes += result.Count;

                    if (totalBytes > buffer.Length)
                    {
                        Debug.LogWarning("[WsClient] 收到的消息太大，可能被截断.");
                        break;
                    }

                } while (!result.EndOfMessage);

                // 转成字符串（UTF-8）
                string json = Encoding.UTF8.GetString(ms.ToArray());

                // 放进队列，并做容量限制
                EnqueueMessage(json);
            }
            catch (OperationCanceledException)
            {
                // 正常取消
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WsClient] 接收异常: {ex}");
                // 出问题了可以选择 break 退出循环，也可以尝试重连（后面可以加）
                break;
            }
        }
    }

    /// <summary>
    /// 把新消息放进队列，并限制队列长度在 maxBufferedMessages 以内。
    /// </summary>
    private void EnqueueMessage(string json)
    {
        _messageQueue.Enqueue(json);

        // 超出容量时丢弃旧消息，保证“最新为先，不积压”
        while (_messageQueue.Count > maxBufferedMessages &&
               _messageQueue.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Update 在 Unity 主线程每帧调用：
    /// - 把队列里的消息全部取出，只保留最后一条作为当前帧使用。
    /// </summary>
    private void Update()
    {
        // 从队列里把所有消息取出来，只留下最新的
        while (_messageQueue.TryDequeue(out var json))
        {
            _latestJson = json;
        }

        // 这里先简单做个可视化测试：有新消息时每秒打一次日志之类的
        // 当前先不解析 JSON，等后面设计数据结构时再处理。
    }

    /// <summary>
    /// 给其它脚本提供的接口：尝试取出当前最新一条 JSON。
    /// 如果当前没有数据，返回 false。
    /// </summary>
    public bool TryGetLatestJson(out string json)
    {
        json = _latestJson;
        return json != null;
    }

    private async void OnDestroy()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        if (_ws != null)
        {
            try
            {
                if (_ws.State == WebSocketState.Open)
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                        "Client closing", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WsClient] 关闭连接时异常: {ex}");
            }

            _ws.Dispose();
            _ws = null;
        }
    }
}

