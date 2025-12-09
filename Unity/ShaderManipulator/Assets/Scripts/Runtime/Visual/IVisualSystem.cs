using UnityEngine;

namespace ShaderDuel.Visual
{
    /// <summary>
    /// 为了保持可拓展&不把逻辑全堆在一个脚本里，将系统拆成多个“小系统”，每个系统专门处理一部分视觉逻辑。
    /// 使用这个接口来定义视觉子系统的行为。
    /// 由继承了这个接口的类来实现具体的视觉逻辑，并将数据通过 VisualFrameState 输出给各个 QuadController。
    /// 各个 QuadController 会把 VisualFrameState 映射到 shader。
    /// VisualOrchestrator 会在每一帧调用所有注册的 IVisualSystem 实例的 UpdateVisuals 方法，
    /// </summary>
    public interface IVisualSystem
    {
        void UpdateVisuals(in VisualFrameInput input, ref VisualFrameState state);
    }

}