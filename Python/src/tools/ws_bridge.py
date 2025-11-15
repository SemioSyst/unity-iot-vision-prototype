import asyncio
import json
import logging
from typing import Any, Set

import websockets
from websockets.server import WebSocketServerProtocol


logger = logging.getLogger(__name__)


class WsBridge:
    """
    一个简单的 WebSocket 服务器封装：
    - 作为“中心”监听某个端口（默认 ws://127.0.0.1:8765）
    - 维护当前所有连接的客户端（例如 Unity）
    - 提供 outgoing / incoming 队列供其它模块使用
    """

    def __init__(self, host: str = "127.0.0.1", port: int = 8765) -> None:
        self.host = host
        self.port = port

        # 当前连接的客户端集合
        self._clients: Set[WebSocketServerProtocol] = set()

        # 供外部使用的队列：
        # - 你把要发送的消息塞进 outgoing（字符串）
        # - 收到的消息会被放进 incoming（字符串）
        self.outgoing: "asyncio.Queue[str]" = asyncio.Queue()
        self.incoming: "asyncio.Queue[str]" = asyncio.Queue()

        self._server: websockets.server.Serve | None = None
        self._sender_task: asyncio.Task | None = None

    # ---------- 对外调用的便捷方法 ----------

    def send_text(self, text: str) -> None:
        """
        将一条文本消息排队，稍后会广播给所有已连接的客户端。

        注意：这是一个快速调用函数，不是协程，可以在普通代码里直接用。
        """
        # put_nowait: 如果队列没有被 await 消费，这里也不会卡住。
        self.outgoing.put_nowait(text)

    def send_json(self, obj: Any) -> None:
        """
        将一个 Python 对象（dict / list 等）编码为 JSON 字符串并排队发送。
        """
        try:
            text = json.dumps(obj)
        except TypeError as e:
            logger.error("send_json 失败，数据不可被 JSON 序列化: %r", e)
            raise
        self.send_text(text)

    async def recv_text(self) -> str:
        """
        从 incoming 队列里取出一条文本消息（协程，需 await）。

        一般在测试或调试脚本里用：
            msg = await bridge.recv_text()
        """
        return await self.incoming.get()

    # ---------- 内部逻辑：接入 / 收发循环 ----------

    async def _handler(self, websocket: WebSocketServerProtocol) -> None:
        """
        每当有一个客户端连进来，就会跑一个 handler 协程。
        负责：
        - 把该连接加入 clients 集合；
        - 持续读取客户端发来的消息，丢进 incoming 队列；
        - 连接断开时把它移出集合。
        """
        client_name = f"{websocket.remote_address}"
        logger.info("客户端连接: %s", client_name)
        self._clients.add(websocket)
        try:
            async for message in websocket:
                # 当前我们只保存原始文本；后续可以再做 JSON 解析封装。
                await self.incoming.put(message)
        except websockets.ConnectionClosed:
            logger.info("客户端断开: %s", client_name)
        finally:
            self._clients.discard(websocket)

    async def _sender_loop(self) -> None:
        """
        独立的发送协程：
        - 从 outgoing 队列里取出消息；
        - 广播给当前所有已连接的客户端。
        """
        logger.info("发送循环启动")
        while True:
            text = await self.outgoing.get()
            if not self._clients:
                # 当前还没有客户端连接，丢弃或缓存视需求而定。
                logger.debug("无客户端连接，丢弃消息: %s", text[:80])
                continue

            # 并发发送到所有客户端
            to_remove = []
            for ws in self._clients:
                try:
                    await ws.send(text)
                except websockets.ConnectionClosed:
                    logger.info("发送失败，连接已关闭，将移除客户端: %s", ws.remote_address)
                    to_remove.append(ws)
            for ws in to_remove:
                self._clients.discard(ws)

    # ---------- 对外总入口 ----------

    async def run_forever(self) -> None:
        """
        启动 WebSocket 服务器和发送循环，并阻塞当前协程（一般用 asyncio.run 来跑）。

        示例：
            bridge = WsBridge()
            asyncio.run(bridge.run_forever())
        """
        logger.info("启动 WebSocket 服务器 ws://%s:%d", self.host, self.port)
        self._server = websockets.serve(self._handler, self.host, self.port) # 建立服务器

        # 启动服务器
        await self._server

        # 启动发送任务
        self._sender_task = asyncio.create_task(self._sender_loop())

        # run_forever: 等待直到程序结束（实际上 websockets.serve 会一直存在）
        # 这里简单地阻塞当前协程：
        await asyncio.Future()  # 等价于“睡死在这里”，直到被取消


# 如果你直接运行这个文件，可以快速测试 echo/心跳
async def _demo() -> None:
    """
    一个简单的自测 demo：
    - 启动 WsBridge
    - 每隔 1 秒发送一条 JSON 心跳消息
    """
    logging.basicConfig(level=logging.INFO, format="[%(levelname)s] %(message)s")
    bridge = WsBridge()

    async def heartbeat():
        i = 0
        while True:
            payload = {
                "type": "heartbeat",
                "count": i,
            }
            bridge.send_json(payload)
            i += 1
            await asyncio.sleep(1.0)

    # 启动服务器和心跳任务，使用 asyncio.gather 并行运行
    await asyncio.gather(
        bridge.run_forever(),
        heartbeat()
    )

if __name__ == "__main__":
    try:
        asyncio.run(_demo())
    except KeyboardInterrupt:
        print("停止服务器")
