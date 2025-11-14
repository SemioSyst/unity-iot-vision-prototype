import asyncio
import logging

from tools.ws_bridge import WsBridge


async def main():
    logging.basicConfig(level=logging.INFO, format="[%(levelname)s] %(message)s")

    bridge = WsBridge()

    async def dummy_sender():
        """
        模拟其它模块：每 0.5 秒往 outgoing 队列塞一条消息。
        将来你可以在这里换成 MediaPipe/YOLO 的输出。
        """
        i = 0
        while True:
            bridge.send_json({
                "type": "dummy",
                "value": i,
            })
            i += 1
            await asyncio.sleep(0.5)

    async def dummy_receiver():
        """
        如果有客户端（例如 Unity）也给 Python 发消息，这里可以打印出来。
        目前没有的话，它就会一直挂起。
        """
        while True:
            msg = await bridge.recv_text()
            print("收到来自客户端的数据:", msg)

    # 同时跑：
    # - WebSocket 服务器
    # - 一个“假发送者”
    # - 一个“假接收者”（能打印 Unity 发来的东西）
    await asyncio.gather(
        bridge.run_forever(),
        dummy_sender(),
        dummy_receiver(),
    )


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("退出 test_ws_server")
