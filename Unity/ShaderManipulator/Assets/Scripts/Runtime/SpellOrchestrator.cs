using System.Collections.Generic;
using UnityEngine;
using ShaderDuel.Hands;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 负责左右手跟踪 + 多法术调度的中心状态机。
    /// </summary>
    public class SpellOrchestrator : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private HandFeatureExtractor _featureExtractor;

        [Header("Tuning")]
        [Tooltip("允许短暂丢帧但仍视为手在场的最大帧数。")]
        [SerializeField] private int _maxMissingFrames = 5;

        [Tooltip("所有可用的法术定义（TODO：后续填入具体法术）。")]
        [SerializeField] private List<SpellDefinition> _spellDefinitions = new List<SpellDefinition>();

        /// <summary>左手状态。</summary>
        public HandTrackState LeftHand { get; private set; }

        /// <summary>右手状态。</summary>
        public HandTrackState RightHand { get; private set; }

        /// <summary>当前正在运行的法术实例列表。</summary>
        private readonly List<RunningSpell> _runningSpells = new List<RunningSpell>();

        private void Awake()
        {
            if (_featureExtractor == null)
            {
                Debug.LogError("[SpellOrchestrator] FeatureExtractor 未设置，请在 Inspector 里拖引用。");
                enabled = false;
                return;
            }

            LeftHand = new HandTrackState(HandSide.Left);
            RightHand = new HandTrackState(HandSide.Right);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var features = _featureExtractor.Global;

            // 1. 更新左右手的 HandTrackState（NoHand / Idle / InSpell）
            UpdateHandTrackState(LeftHand, features, isLeft: true);
            UpdateHandTrackState(RightHand, features, isLeft: false);

            // 2. 先更新已有法术实例内部 FSM
            TickRunningSpells(dt, features);

            // 3. 处理已经结束 / 取消的法术实例（释放手）
            CleanupFinishedSpells();

            // 4. 根据 features 决定是否创建新的法术实例
            TryStartNewSpells(features);
        }

        #region 手级状态更新（TODO：具体判定规则后续细化）

        private void UpdateHandTrackState(HandTrackState handState,
                                          GlobalHandFeatures features,
                                          bool isLeft)
        {
            // 1. 从全局特征中取出当前这只手的 HandFeatures
            bool hasHand;
            HandFeatures handFeatures;

            if (isLeft)
            {
                hasHand = features.HasLeftHand;
                handFeatures = features.LeftHand;
            }
            else
            {
                hasHand = features.HasRightHand;
                handFeatures = features.RightHand;
            }

            if (hasHand && handFeatures.IsTracked)
            {
                handState.Features = handFeatures;
                handState.FramesSinceSeen = 0;
            }
            else
            {
                handState.FramesSinceSeen++;
            }

            // 2. 根据是否被法术占用 + 是否在场 更新 Phase
            if (handState.CurrentSpell != null)
            {
                handState.Phase = HandTrackPhase.InSpell;
                return;
            }

            if (!handState.IsConsideredPresent(_maxMissingFrames))
            {
                handState.Phase = HandTrackPhase.NoHand;
                return;
            }

            // TODO：这里目前先简单地全部视为 Idle，
            // 以后可以在此处加入拓展逻辑。
            handState.Phase = HandTrackPhase.Idle;
        }

        #endregion

        #region 运行中的法术更新与清理

        private void TickRunningSpells(float dt, GlobalHandFeatures features)
        {
            foreach (var spell in _runningSpells)
            {
                spell.Tick(dt, features);
            }
        }

        private void CleanupFinishedSpells()
        {
            for (int i = _runningSpells.Count - 1; i >= 0; i--)
            {
                var spell = _runningSpells[i];
                if (!spell.IsCompleted && !spell.IsCancelled)
                    continue;

                // 在移除前先调用 OnEnd，并释放绑定的手
                spell.OnEnd();
                ReleaseHandsFromSpell(spell);

                _runningSpells.RemoveAt(i);
            }
        }

        private void ReleaseHandsFromSpell(RunningSpell spell)
        {
            foreach (var hand in spell.BoundHands)
            {
                if (hand.CurrentSpell == spell)
                {
                    hand.CurrentSpell = null;
                    // 下一帧 UpdateHandTrackState 会根据是否在场，把 Phase 改回 Idle / NoHand。
                }
            }
        }

        #endregion

        #region 启动新法术（调度器核心 TODO）

        private void TryStartNewSpells(GlobalHandFeatures features)
        {
            // TODO v1：先做一个非常保守的版本，只考虑单手法术 + 右手。
            // 后续：
            // 1. 根据 HandTrackState.Phase 判断哪些手可用；（Idle 才可用）
            // 2. 遍历 _spellDefinitions，按 Priority 排序；
            // 3. 对每个 SpellDefinition 调用 CanStart(left, right, features)；
            // 4. 选中一个法术，调用 CreateInstance(...) 创建 RunningSpell；
            // 5. 把涉及到的 HandTrackState.CurrentSpell 指向该实例，Phase 设为 InSpell。

            // 占位示例（不会真正创建任何法术）：
            // foreach (var def in _spellDefinitions)
            // {
            //     if (def.CanStart(LeftHand, RightHand, features))
            //     {
            //         var instance = def.CreateInstance(this, new[] { RightHand }, features);
            //         BindHandsToSpell(instance);
            //         _runningSpells.Add(instance);
            //         break;
            //     }
            // }
        }

        private void BindHandsToSpell(RunningSpell instance)
        {
            foreach (var hand in instance.BoundHands)
            {
                hand.CurrentSpell = instance;
                hand.Phase = HandTrackPhase.InSpell;
            }
        }

        #endregion
    }
}

