using UnityEngine;

namespace ShaderDuel.Audio
{
    /// <summary>
    /// 音频特征调试可视化：
    /// - 定时打印 dBFS/RMS/Smoothed 值；
    /// - 在 Scene 中画音量变化趋势线。
    /// </summary>
    public class AudioFeaturesDebugView : MonoBehaviour
    {
        [Header("Extractor")]
        [SerializeField] private AudioFeatureExtractor _extractor;

        [Header("Logging")]
        [SerializeField] private bool _log = true;

        [SerializeField] private float _logInterval = 0.5f;
        private float _logTimer;

        [Header("Gizmos")]
        [SerializeField] private bool _drawGizmos = true;

        [Tooltip("横向拉伸曲线长度")]
        [SerializeField] private float _timeScale = 3f;

        [Tooltip("垂向缩放分贝曲线")]
        [SerializeField] private float _dbScale = 0.05f;

        [SerializeField] private Vector3 _offset = new Vector3(0, 0, 0);

        private float _prevY;

        private void Update()
        {
            if (!_log || _extractor == null) return;

            _logTimer += Time.deltaTime;
            if (_logTimer < _logInterval) return;
            _logTimer = 0f;

            var g = _extractor.Global;
            if (!g.HasAudio) return;

            var f = g.Main;
            Debug.Log($"[AudioDebug] dB={f.Dbfs:F1}, smoothed={f.SmoothedDbfs:F1}, Δ={f.DbfsDelta:F1}, loud={f.IsLoud}");
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos || _extractor == null) return;

            var g = _extractor.Global;
            if (!g.HasAudio) return;

            var f = g.Main;

            Vector3 pos = transform.position + _offset;
            float y = f.SmoothedDbfs * _dbScale;

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pos + new Vector3(0, y, 0), 0.03f);

            Gizmos.DrawLine(
                pos + new Vector3(-_timeScale * Time.deltaTime, _prevY, 0),
                pos + new Vector3(0, y, 0)
            );

            _prevY = y;
        }
    }
}

