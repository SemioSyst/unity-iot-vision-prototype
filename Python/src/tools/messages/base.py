from __future__ import annotations
import time
from typing import Any, Optional


def make_message(
    msg_type: str,
    payload: dict,
    frame_id: Optional[int] = None,
    source: str = "mediapipe",
    version: int = 1,
) -> dict:
    """
    构造顶层标准消息结构。
    所有的 payload（hands, yolo, pose...）都使用这个函数包上一层。
    """
    return {
        "type": msg_type,
        "version": version,
        "timestamp": time.time(),   # 秒（float）
        "frame_id": frame_id,
        "source": source,
        "payload": payload,
    }
