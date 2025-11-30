# Python/src/app/audio_loop.py
from __future__ import annotations

import asyncio
import logging
from typing import Optional

import numpy as np
import sounddevice as sd

from Python.src.tools.ws_bridge import WsBridge
from Python.src.tools.messages.base import make_message
from Python.src.tools.messages.audio import build_audio_payload

logger = logging.getLogger(__name__)

# 只保留“每多久算一次音量”
BLOCK_DURATION = 0.05  # 每 50 ms 计算一次音量


def find_c922_device_index(preferred_hostapis=("Windows WASAPI", "Windows DirectSound")):
    devices = sd.query_devices()
    hostapis = sd.query_hostapis()

    candidates = []
    for idx, dev in enumerate(devices):
        if dev["max_input_channels"] <= 0:
            continue
        if "C922 Pro Stream Webcam" in dev["name"]:
            hostapi_name = hostapis[dev["hostapi"]]["name"]
            candidates.append((idx, dev["name"], hostapi_name))

    if not candidates:
        return None

    # 按首选 hostapi 排序
    for pref in preferred_hostapis:
        for idx, name, hostapi_name in candidates:
            if hostapi_name == pref:
                return idx

    # 实在不行就随便拿第一个
    return candidates[0][0]


async def audio_loop(
    bridge: WsBridge,
    device: Optional[int | str] = None,
) -> None:
    """
    采集麦克风音量，计算 dBFS，通过 WebSocket 周期发送给 Unity。
    """
    # 1. 先决定用哪个设备
    if device is None:
        device = find_c922_device_index()
    logger.info("初始化音频输入 device=%r", device)

    try:
        # 2. 查这个设备的默认采样率，并用它来算 block_size
        if device is None:
            # 真找不到 C922 的话，就用系统默认输入设备
            device_info = sd.query_devices(kind="input")
            device_index = sd.default.device[0]
        else:
            device_info = sd.query_devices(device, "input")
            device_index = device

        sample_rate = int(device_info["default_samplerate"])
        block_size = int(sample_rate * BLOCK_DURATION)

        logger.info(
            "使用音频设备 #%s: %s, samplerate=%s, block_size=%s",
            device_index,
            device_info["name"],
            sample_rate,
            block_size,
        )

        # 3. 用“设备默认采样率”和对应的 block_size 打开输入流
        stream = sd.InputStream(
            device=device_index,
            channels=1,          # 单声道即可
            samplerate=sample_rate,
            blocksize=block_size,
            dtype="float32",     # [-1, 1]
        )
    except Exception as e:
        logger.error("无法打开音频输入设备: %r", e)
        return

    frame_id = 0

    with stream:
        device_name = device_info["name"]
        logger.info("音频输入设备已打开: %s", device_name)

        try:
            while True:
                # 读取一个 block（阻塞式），长度就是 block_size
                audio_block, _ = stream.read(block_size)   # shape: (N, 1)
                samples = audio_block[:, 0]

                # 计算 RMS，避免除零
                rms = float(np.sqrt(np.mean(samples ** 2)))
                rms = max(rms, 1e-8)

                # dBFS: 0 dBFS 表示满幅度（|sample|==1），一般结果是负数
                level_dbfs = 20.0 * float(np.log10(rms))

                payload = build_audio_payload(
                    level_dbfs=level_dbfs,
                    rms=rms,
                    device_name=device_name,
                    sample_rate=sample_rate,          # 用刚刚查到的采样率
                    block_duration=BLOCK_DURATION,
                )

                msg = make_message(
                    msg_type="audio_level",
                    payload=payload,
                    frame_id=frame_id,
                    source="c922_mic",
                )

                bridge.send_json(msg)
                frame_id += 1

                # 把控制权交还给事件循环，避免饿死 WsBridge
                await asyncio.sleep(0)
        finally:
            logger.info("audio_loop 结束，关闭音频输入")
