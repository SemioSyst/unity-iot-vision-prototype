from __future__ import annotations
from typing import Any, List, Dict


def _extract_label_and_score(handedness) -> tuple[str, float]:
    """
    从 MediaPipe handedness 结构中提取标签（Left/Right）和置信度。
    handedness 是一个包含 classification 的对象。
    """
    if handedness is None or not handedness.classification:
        return "Unknown", 0.0

    cls = handedness.classification[0]
    return cls.label, float(cls.score)


def _convert_landmarks_to_dict(
    lm_list,
    img_width: int,
    img_height: int
) -> List[Dict[str, float]]:
    """
    将 MediaPipe 的 landmarks（21 points）转成 dict 列表。
    包含归一化坐标 (x, y, z) 和像素坐标 (px, py)。
    """
    output = []
    for i, lm in enumerate(lm_list.landmark):
        x, y, z = float(lm.x), float(lm.y), float(lm.z)
        px, py = int(x * img_width), int(y * img_height)

        output.append({
            "i": i,
            "x": x,
            "y": y,
            "z": z,
            "px": px,
            "py": py
        })
    return output


def build_hands_payload(results: Any, img_width: int, img_height: int) -> dict:
    """
    构造 hand payload，符合我们定义的 JSON schema。

    返回:
    {
        "image": { "width": W, "height": H },
        "hands": [
            {
                "id": int,
                "label": "Right" / "Left",
                "score": float,
                "landmarks": [ { ...21 点... } ]
            }
        ]
    }
    """

    payload = {
        "image": {
            "width": img_width,
            "height": img_height
        },
        "hands": []
    }

    if results is None:
        return payload

    # 处理 multi_hand_landmarks + multi_handedness
    lm_list = results.multi_hand_landmarks
    hd_list = results.multi_handedness

    if lm_list is None or hd_list is None:
        return payload

    for idx, (lm, hd) in enumerate(zip(lm_list, hd_list)):
        label, score = _extract_label_and_score(hd)
        landmarks = _convert_landmarks_to_dict(lm, img_width, img_height)

        hand_entry = {
            "id": idx,
            "label": label,
            "score": score,
            "landmarks": landmarks
        }

        payload["hands"].append(hand_entry)

    return payload
