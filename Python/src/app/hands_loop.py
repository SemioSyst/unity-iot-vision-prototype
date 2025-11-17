from __future__ import annotations

import asyncio
import logging
from typing import Optional

import cv2
import mediapipe as mp

from Python.src.tools.ws_bridge import WsBridge
from Python.src.tools.messages.base import make_message
from Python.src.tools.messages.hands import build_hands_payload


logger = logging.getLogger(__name__)


# 摄像头 & 推理相关参数（可以视情况调整）
CAM_INDEX = 0
CAP_WIDTH = 1280
CAP_HEIGHT = 720
TARGET_FPS = 60

INFER_WIDTH = 640    # 推理分辨率（与 demo2 一致：小图推理）
INFER_HEIGHT = 360


async def hands_loop(
    bridge: WsBridge,
    cam_index: int = CAM_INDEX,
    debug_show: bool = False,
) -> None:
    """
    高性能 Hands 捕捉 + JSON 发送循环。

    - 使用 MJPG 压缩 + 1280x720 采集
    - 640x360 缩小图送入 MediaPipe Hands
    - 不画图、不显示窗口（除非 debug_show=True）
    - 每帧构造 hands payload -> 顶层 message -> 通过 WsBridge.send_json 广播
    """

    logger.info("初始化 MediaPipe Hands")
    mp_hands = mp.solutions.hands
    hands = mp_hands.Hands(
        model_complexity=0,             # 低复杂度模型，速度快
        max_num_hands=2,
        min_detection_confidence=0.7,
        min_tracking_confidence=0.7,
    )

    logger.info("打开摄像头 index=%d", cam_index)
    cap = cv2.VideoCapture(cam_index, cv2.CAP_DSHOW)

    # 摄像头配置：MJPG + 分辨率 + 目标 FPS
    cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"))
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, CAP_WIDTH)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, CAP_HEIGHT)
    cap.set(cv2.CAP_PROP_FPS, TARGET_FPS)

    if not cap.isOpened():
        logger.error("无法打开摄像头 %d", cam_index)
        return

    frame_id = 0

    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                logger.warning("读取摄像头帧失败，退出 hands_loop")
                break

            # 镜像（自拍视角）
            frame = cv2.flip(frame, 1)

            # ---------------------------------------
            # 1. 生成缩小版图像用于推理（demo2 核心）
            # ---------------------------------------
            small = cv2.resize(frame, (INFER_WIDTH, INFER_HEIGHT))
            small_rgb = cv2.cvtColor(small, cv2.COLOR_BGR2RGB)
            small_rgb.flags.writeable = False
            results = hands.process(small_rgb)
            small_rgb.flags.writeable = True

            # ---------------------------------------
            # 2. 构造 payload + 顶层 message
            #    注意：这里 img_width/height 仍然用原始 1280x720，
            #    因为 MediaPipe 的 landmark 坐标是基于“输入图像尺寸”的归一化。
            #    我们此处统一约定：以采集分辨率 CAP_WIDTH/HEIGHT 作为逻辑尺寸。
            # ---------------------------------------
            # 这里有一个设计点：
            # - 因为我们输入给 Hands 的是 small(640x360)，
            #   MediaPipe 的 lm.x/lm.y 是相对小图的归一化坐标。
            # - 但我们希望 JSON 里 px/py 是基于“最终逻辑坐标系”的像素值。
            #   你可以选择：
            #   A) 直接用小图尺寸 INFER_WIDTH/HEIGHT
            #   B) 映射回大图 CAP_WIDTH/HEIGHT
            #
            # 为了和 Unity 一致，建议这里用 CAP_WIDTH/HEIGHT。
            #
            # 换算方式：px = x * INFER_WIDTH * (CAP_WIDTH / INFER_WIDTH)
            #          => px = x * CAP_WIDTH
            #   所以实际上用 CAP_WIDTH/HEIGHT 也是正确的。
            #
            # 因此我们直接传 CAP_WIDTH/CAP_HEIGHT 给 build_hands_payload。
            payload = build_hands_payload(results, CAP_WIDTH, CAP_HEIGHT)
            msg = make_message(
                msg_type="hands",
                payload=payload,
                frame_id=frame_id,
                source="mediapipe_hands",
            )

            # 通过 WebSocket 广播给所有客户端（例如 Unity）
            bridge.send_json(msg)
            frame_id += 1

            # ---------------------------------------
            # 3. 可选：debug 显示窗口（仅调试用）
            #    这里我们可以简单地显示原始 frame，不绘制关键点，
            #    只是用于观察延迟 / 流畅度。
            # ---------------------------------------
            if debug_show:
                cv2.imshow("Hands Debug (raw camera)", frame)
                # 这里仍然用非阻塞 waitKey
                if (cv2.waitKey(1) & 0xFF) == ord("q"):
                    logger.info("检测到 'q' 键，退出 hands_loop")
                    break

            # ---------------------------------------
            # 4. 让出控制权给其它协程（桥服务器）
            # ---------------------------------------
            # Hands 推理本身是同步阻塞的，这里用一个很短的 sleep
            # 让 asyncio 有机会调度 WsBridge 的发送/接收任务。
            await asyncio.sleep(0)  # 等价于让出时间片

    finally:
        logger.info("hands_loop 结束，释放资源")
        cap.release()
        cv2.destroyAllWindows()
        hands.close()
