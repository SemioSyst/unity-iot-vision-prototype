import asyncio
import websockets

async def main():
    uri = "ws://127.0.0.1:8765"
    async with websockets.connect(uri) as ws:
        print("✅ 已连接到服务器")
        # 接收几条消息看看
        for _ in range(5):
            msg = await ws.recv()
            print("收到:", msg)

        # 也可以发一条消息回去
        await ws.send("hello from client")
        print("已发送一条测试消息")

asyncio.run(main())
