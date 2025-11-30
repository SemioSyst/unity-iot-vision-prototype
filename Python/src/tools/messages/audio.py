# Python/src/tools/messages/audio.py
from __future__ import annotations

from typing import Optional


def build_audio_payload(
    level_dbfs: float,
    rms: float,
    device_name: str | None = None,
    sample_rate: int | None = None,
    block_duration: float | None = None,
) -> dict:
    """
    构造音频分贝 payload.

    level_dbfs: 以 dBFS（相对满幅度 0 dB）表示的音量值，越大越响，一般是负数。
    rms:        原始归一化 RMS（0~1）。
    """
    return {
        "device": {
            "name": device_name,
            "sample_rate": sample_rate,
        },
        "window": {
            "duration_sec": block_duration,
        },
        "level": {
            "dbfs": level_dbfs,
            "rms": rms,
        },
    }
