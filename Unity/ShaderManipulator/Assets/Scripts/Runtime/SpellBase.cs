using UnityEngine;
using ShaderDuel.Hands;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 法术需要的手数：单手 / 双手。
    /// （后面如果要支持三手怪物可以再扩展枚举）
    /// </summary>
    public enum SpellHandRequirement
    {
        SingleHand,
        DualHand
    }

    /// <summary>
    /// 法术定义基类：描述“需要几只手、优先级、入场条件”等。
    /// 具体每个法术可以从这里派生，重写 CanStart / CreateInstance。
    /// （后面你愿意的话可以改成 ScriptableObject 配置）
    /// </summary>
    public abstract class SpellDefinition
    {
        /// <summary>法术的标识符（比如 "small_blast"）。</summary>
        public abstract string Id { get; }

        /// <summary>需要的手数。</summary>
        public abstract SpellHandRequirement HandRequirement { get; }

        /// <summary>当多个法术都满足条件时，用于排序选择。</summary>
        public virtual int Priority => 0;

        /// <summary>
        /// 调度器调用：当前这一帧，是否允许从给定手状态启动这个法术。
        /// 注意：这里只做“粗判定”，具体 FSM 逻辑由 RunningSpell 内部完成。
        /// </summary>
        public abstract bool CanStart(HandTrackState left, HandTrackState right, GlobalHandFeatures features);

        /// <summary>
        /// 调度器调用：正式创建一个运行中的法术实例。
        /// hands 参数应包含会被该法术锁定的手（1 手或 2 手）。
        /// </summary>
        public abstract RunningSpell CreateInstance(SpellOrchestrator orchestrator,
                                                    HandTrackState[] hands,
                                                    GlobalHandFeatures features);
    }

    /// <summary>
    /// 运行中的法术实例基类。内部维护自己的小 FSM（TODO）。
    /// </summary>
    public abstract class RunningSpell
    {
        public readonly SpellDefinition Definition;
        public readonly HandTrackState[] BoundHands;

        /// <summary>法术是否已经自然结束。</summary>
        public bool IsCompleted { get; protected set; }

        /// <summary>法术是否被玩家 / 系统取消。</summary>
        public bool IsCancelled { get; protected set; }

        /// <summary>用于传给视觉层的简单输出（可扩展）。</summary>
        public SpellRuntimeStatus RuntimeStatus;

        protected readonly SpellOrchestrator Orchestrator;

        protected RunningSpell(SpellDefinition definition,
                               SpellOrchestrator orchestrator,
                               HandTrackState[] boundHands)
        {
            Definition = definition;
            Orchestrator = orchestrator;
            BoundHands = boundHands;
        }

        /// <summary>
        /// 每帧更新内部 FSM 逻辑。
        /// TODO：具体法术在这里实现“准备/蓄力/释放/冷却”等阶段。
        /// </summary>
        public abstract void Tick(float deltaTime, GlobalHandFeatures features);

        /// <summary>
        /// 法术实例被销毁前的清理逻辑（如重置某些状态、发事件）。
        /// </summary>
        public virtual void OnEnd() { }
    }

    /// <summary>
    /// 提供给视觉层 / 上层逻辑使用的法术运行时输出。
    /// 暂时只包含一个 Phase 和 Charge，后续可以扩展。
    /// </summary>
    public struct SpellRuntimeStatus
    {
        public string SpellId;     // 当前法术 Id
        public float NormalizedCharge; // 0–1，蓄力程度
        public Vector3 AimDirection;   // 当前攻击方向
        // TODO: 以后可以加入更多字段（是否为强力版、当前阶段等）
    }
}

