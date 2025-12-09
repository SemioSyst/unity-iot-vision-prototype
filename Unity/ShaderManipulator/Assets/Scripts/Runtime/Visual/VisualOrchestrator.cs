using ShaderDuel.Core;
using ShaderDuel.Gameplay;
using ShaderDuel.Hands;
using ShaderDuel.Visual;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责把上游输入（手势 / 音频 / 战斗上下文）
/// 在每一帧打包成 VisualFrameInput，
/// 然后交给一组 IVisualSystem 来填写 VisualFrameState。
/// 之后再由各个 QuadController 把 VisualFrameState 映射到 shader。
/// </summary>
public sealed class VisualOrchestrator : MonoBehaviour
{
    [Header("Upstream inputs")]
    [SerializeField] private HandsInputSource handsInputSource;
    [SerializeField] private AudioInputSource audioInputSource;
    [SerializeField] private CombatDriver combatDriver;
    [SerializeField] private HandFeatureExtractor HandFeatureExtractor;

    [Header("Layer Controllers")]
    [SerializeField] private SpellQuadController spellQuadController;
    [SerializeField] private BossQuadController bossQuadController;
    // [SerializeField] private BackgroundQuadController backgroundQuadController;
    // [SerializeField] private OverlayQuadController overlayQuadController;

    // 之后会在这里注册各种视觉系统：
    // EnemyBossVisualSystem / SpellVisualSystem / AudioBackgroundPulseSystem 等
    private readonly List<IVisualSystem> _visualSystems = new List<IVisualSystem>();

    [Header("Debug")]
    [SerializeField] private bool logOnceOnStart = true;
    private bool _logged;

    private void Awake()
    {
        // 这里只做最基础的引用检查，方便你一开始排错
        if (handsInputSource == null)
            Debug.LogWarning("[VisualOrchestrator] HandsInputSource 未绑定。");
        if (audioInputSource == null)
            Debug.LogWarning("[VisualOrchestrator] AudioInputSource 未绑定。");
        if (combatDriver == null)
            Debug.LogWarning("[VisualOrchestrator] CombatDriver 未绑定。");

        // TODO: 在后续步骤里，在这里向 _visualSystems 添加各个具体 IVisualSystem 实例。
        // 例如：
        // _visualSystems.Add(new EnemyBossVisualSystem());
        // _visualSystems.Add(new SpellVisualSystem());
        _visualSystems.Add(new HandPalmMarkerSystem());
        _visualSystems.Add(new EnemyBossVisualSystem());
    }

    private void Update()
    {
        // 输入源缺失时直接跳过，避免 NullReference 把 Console 淹没
        if (handsInputSource == null || audioInputSource == null || combatDriver == null)
            return;

        // 1. 组装这一帧的输入快照
        var input = new VisualFrameInput(
            handsInputSource,
            audioInputSource,
            combatDriver.Context,
            HandFeatureExtractor.Global,
            Time.deltaTime,
            Time.time
        );

        // 2. 初始化这一帧的视觉状态（默认值全 0）
        var state = new VisualFrameState();

        // 3. 依次让所有视觉系统根据 input 填写 / 修改 state
        for (int i = 0; i < _visualSystems.Count; i++)
        {
            _visualSystems[i].UpdateVisuals(in input, ref state);
        }

        // 4. TODO：在后续步骤里，把 state 传给各个 QuadController.ApplyState(...)
        // 例如：
        // bossQuadController.ApplyState(state.Boss);
        // spellQuadController.ApplyState(state.Spell);
        // backgroundQuadController.ApplyState(state.Background);
        // overlayQuadController.ApplyState(state.Overlay);

        // 若有对应的 QuadController，则应用状态
        // Boss Quad Controller
        if (bossQuadController != null)
        {
            bossQuadController.ApplyState(in state.Boss);
        }

        // Spell Quad Controller
        if (spellQuadController != null)
        {
            spellQuadController.ApplyState(in state.Spell);
        }

        // 5. 初始阶段的简单 Debug：确认管线在跑
        if (logOnceOnStart && !_logged)
        {
            _logged = true;
            Debug.Log("[VisualOrchestrator] Visual pipeline ticking. " +
                      "Hands / Audio / HandsFeatures / Combat 已打包进 VisualFrameInput。");
        }
    }
}
