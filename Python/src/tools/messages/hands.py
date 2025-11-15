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
    output = [] # 存放转换后的点列表
    # 转换为enumerate形式，方便获取点的索引
    # 遍历每个点
    for i, lm in enumerate(lm_list.landmark):
        x, y, z = float(lm.x), float(lm.y), float(lm.z)
        px, py = int(x * img_width), int(y * img_height)

        # 添加到输出列表
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

    # 初始化基本payload结构
    payload = {
        "image": {
            "width": img_width,
            "height": img_height
        },
        "hands": []
    }

    # 如果没有检测到手，直接返回空的 hands 列表，仅包含图像信息
    if results is None:
        return payload

    # 处理 multi_hand_landmarks + multi_handedness
    lm_list = results.multi_hand_landmarks
    hd_list = results.multi_handedness

    # 如果任一为空，返回空的 hands 列表
    if lm_list is None or hd_list is None:
        return payload

    # 循环逻辑注释：
    # - lm_list 和 hd_list 是并行的，长度相同
    # - 每个 lm对象 对应一个 hd对象，可以通过zip配对
    # - 变成 [(LM_left, HD_left), (LM_right, HD_right)] 的形式
    # - 然后用 enumerate 获取索引，方便赋予手的 ID
    # - 这会使数据结构变为 [(0, (LM_left, HD_left)), (1, (LM_right, HD_right))]
    # - 随后通过for解包获取索引和对象，并构造每只手的字典条目

    for idx, (lm, hd) in enumerate(zip(lm_list, hd_list)):
        label, score = _extract_label_and_score(hd)
        landmarks = _convert_landmarks_to_dict(lm, img_width, img_height)
        
        # 构造单个手的字典条目
        hand_entry = {
            "id": idx, # 手的索引 ID
            "label": label, # 左手或右手标签
            "score": score, # 置信度分数
            "landmarks": landmarks
        }
        # 添加到 hands 列表
        payload["hands"].append(hand_entry)

    return payload
