using UnityEngine;

/// <summary>
/// 抽象的“手部输入源”接口。
/// 任何数据来源（Python、录制回放、本地 MediaPipe 等）
/// 只要实现这个接口，就可以被后续手势系统 / Shader 控制逻辑使用。
/// </summary>
public interface IHandsInput
{
    /// <summary> 当前是否至少收到过一帧有效数据。 </summary>
    bool HasData { get; }

    /// <summary> 当前帧检测到的手的数量。 </summary>
    int HandCount { get; }

    /// <summary> 最近一帧的 frame_id（没有数据时可返回 -1）。 </summary>
    int LatestFrameId { get; }

    /// <summary> 最近一帧的时间戳（Unix 秒）。 </summary>
    double LatestTimestamp { get; }

    /// <summary>
    /// 按索引获取某一只手（0-based）。
    /// 返回 true 表示成功。
    /// </summary>
    bool TryGetHand(int index, out HandData hand);

    /// <summary>
    /// 获取“主手”（实现策略由具体实现决定，例如优先右手，没有右手取第一只）。
    /// </summary>
    bool TryGetPrimaryHand(out HandData hand);

    /// <summary> 获取左手（若存在）。 </summary>
    bool TryGetLeftHand(out HandData hand);

    /// <summary> 获取右手（若存在）。 </summary>
    bool TryGetRightHand(out HandData hand);
}
