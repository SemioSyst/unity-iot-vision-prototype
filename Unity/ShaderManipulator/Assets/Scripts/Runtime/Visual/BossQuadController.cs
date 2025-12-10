using ShaderDuel.Gameplay; // 如果 EnemyPhase 在别处，按实际命名空间改
using ShaderDuel.Visual;
using UnityEngine;

/// <summary>
/// 负责把 BossLayerState 中的数据写入 BossQuad 的材质，
/// Shader 里可以根据这些数值来做阶段/血量/攻击等视觉效果。
/// </summary>
[RequireComponent(typeof(Renderer))]
public sealed class BossQuadController : MonoBehaviour
{
    [Header("Target Renderer")]
    [SerializeField]
    private Renderer targetRenderer;

    [Header("Shader Property Names")]
    [SerializeField] private string enemyIdProperty = "_EnemyId";
    [SerializeField] private string enemyPhaseProperty = "_EnemyPhase";
    [SerializeField] private string enemyPhaseProgressProperty = "_EnemyPhaseProgress01";
    [SerializeField] private string enemyHealth01Property = "_EnemyHealth01";
    [SerializeField] private string enemyHealthMaxProperty = "_EnemyHealthMax";
    [SerializeField] private string enemyAttackCharge01Property = "_EnemyAttackCharge01";
    [SerializeField] private string enemyAttackHitPulseProperty = "_EnemyAttackHitPulse01";
    // NEW: 被光炮命中的脉冲
    [SerializeField] private string enemyHitByBeamPulseProperty = "_EnemyHitByBeamPulse01";

    private Material _material;

    private int _enemyIdID;
    private int _enemyPhaseID;
    private int _enemyPhaseProgressID;
    private int _enemyHealth01ID;
    private int _enemyHealthMaxID;
    private int _enemyAttackCharge01ID;
    private int _enemyAttackHitPulseID;
    private int _enemyHitByBeamPulseID;

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (targetRenderer == null)
        {
            Debug.LogError("[BossQuadController] 找不到 Renderer，脚本将被禁用。", this);
            enabled = false;
            return;
        }

        // 生成当前物体专用材质实例
        _material = targetRenderer.material;
        if (_material == null)
        {
            Debug.LogError("[BossQuadController] Renderer 上没有材质。", this);
            enabled = false;
            return;
        }

        // 缓存属性 ID，方便以后改名字
        _enemyIdID = Shader.PropertyToID(enemyIdProperty);
        _enemyPhaseID = Shader.PropertyToID(enemyPhaseProperty);
        _enemyPhaseProgressID = Shader.PropertyToID(enemyPhaseProgressProperty);
        _enemyHealth01ID = Shader.PropertyToID(enemyHealth01Property);
        _enemyHealthMaxID = Shader.PropertyToID(enemyHealthMaxProperty);
        _enemyAttackCharge01ID = Shader.PropertyToID(enemyAttackCharge01Property);
        _enemyAttackHitPulseID = Shader.PropertyToID(enemyAttackHitPulseProperty);
        _enemyHitByBeamPulseID = Shader.PropertyToID(enemyHitByBeamPulseProperty);
    }

    /// <summary>
    /// 由 VisualOrchestrator 在每帧调用，
    /// 将 BossLayerState 映射为 Shader 参数。
    /// </summary>
    public void ApplyState(in BossLayerState state)
    {
        if (_material == null) return;

        // 敌人 ID
        _material.SetInt(_enemyIdID, state.EnemyId);

        // EnemyPhase 作为 int 传给 Shader（HLSL 里用 float 比较）
        _material.SetInt(_enemyPhaseID, (int)state.EnemyPhase);

        // 阶段内部进度
        _material.SetFloat(_enemyPhaseProgressID, state.EnemyPhaseProgress01);

        // 血量
        _material.SetFloat(_enemyHealth01ID, state.EnemyHealth01);
        _material.SetFloat(_enemyHealthMaxID, state.EnemyHealthMax);

        // 攻击相关
        _material.SetFloat(_enemyAttackCharge01ID, state.EnemyAttackCharge01);
        _material.SetFloat(_enemyAttackHitPulseID, state.EnemyAttackHitPulse01);

        // NEW: 被光炮命中脉冲
        _material.SetFloat(_enemyHitByBeamPulseID, state.EnemyHitByBeamPulse01);
    }
}
