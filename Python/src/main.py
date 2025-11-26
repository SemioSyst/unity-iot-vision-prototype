from __future__ import annotations

import asyncio
import logging

from Python.src.tools.ws_bridge import WsBridge
from Python.src.app.hands_loop import hands_loop


async def main():
    logging.basicConfig(
        # 设置日志格式和级别
        # 不显示debug级别日志，避免过多输出
        level=logging.INFO,
        format="[%(levelname)s] %(name)s: %(message)s"
    )

    bridge = WsBridge(host="127.0.0.1", port=8765)

    # 将来可以在这里把更多 loop 加进来，例如:
    # from Python.src.app.yolo_loop import yolo_loop
    # await asyncio.gather(bridge.run_forever(), hands_loop(bridge), yolo_loop(bridge))
    await asyncio.gather(
        bridge.run_forever(),
        hands_loop(bridge, debug_show=False),
    )


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("退出程序")
