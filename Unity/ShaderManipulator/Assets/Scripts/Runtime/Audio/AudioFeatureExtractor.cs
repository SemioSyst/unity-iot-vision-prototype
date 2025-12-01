using UnityEngine;
using ShaderDuel.Core;     // 为了 IAudioInput
using ShaderDuel.Audio;

namespace ShaderDuel.Audio
{
    /// <summary>
    /// 负责从 IAudioInput 获取音频数据，并抽取更稳定的特征。
    /// 对标 HandFeatureExtractor。
    /// </summary>
    public class AudioFeatureExtractor : MonoBehaviour
    {
        [Header("Input Source")]
        [SerializeField] private MonoBehaviour _inputBehaviour;
        private IAudioInput _audioInput;

        [Header("Smoothing")]
        [Range(0.01f, 1f)]
        [SerializeField] private float _smoothing = 0.35f;

        [Header("Semantic Thresholds")]
        [Tooltip("认为'声音很大'的分贝阈值，例如 -20 dBFS")]
        [SerializeField] private float _loudThreshold = -20f;

        [Tooltip("认为'几乎静音'的分贝阈值，例如 -50 dBFS")]
        [SerializeField] private float _silentThreshold = -50f;

        // 上一帧缓存
        private AudioFeatures _prev;
        private bool _hasPrev;

        // 对外暴露
        public GlobalAudioFeatures Global { get; private set; }

        private void Awake()
        {
            if (_inputBehaviour != null)
            {
                _audioInput = _inputBehaviour as IAudioInput;
                if (_audioInput == null)
                {
                    Debug.LogError("[AudioFeatureExtractor] 绑定组件没有实现 IAudioInput。");
                }
            }
        }

        private void Update()
        {
            if (_audioInput == null || !_audioInput.HasData)
            {
                UpdateNoAudio();
                return;
            }

            if (!_audioInput.TryGetLatestLevel(out var levelInfo))
            {
                UpdateNoAudio();
                return;
            }

            // 新建特征对象
            AudioFeatures f = default;
            f.Dbfs = levelInfo.dbfs;
            f.Rms = levelInfo.rms;

            // Frame info
            _audioInput.TryGetLatestMessage(out var msg);
            f.FrameId = msg.frame_id;
            f.Timestamp = msg.timestamp;
            f.IsTracked = true;
            f.FramesSinceSeen = 0;

            // 平滑（与 HandFeatureExtractor 一样：Lerp）
            if (_hasPrev)
            {
                f.SmoothedDbfs = Mathf.Lerp(_prev.SmoothedDbfs, f.Dbfs, _smoothing);
                f.SmoothedRms = Mathf.Lerp(_prev.SmoothedRms, f.Rms, _smoothing);

                float dt = Mathf.Max(Time.deltaTime, 1e-5f);
                f.DbfsDelta = (f.SmoothedDbfs - _prev.SmoothedDbfs) / dt;
                f.RmsDelta = (f.SmoothedRms - _prev.SmoothedRms) / dt;
            }
            else
            {
                f.SmoothedDbfs = f.Dbfs;
                f.SmoothedRms = f.Rms;
                f.DbfsDelta = 0f;
                f.RmsDelta = 0f;
            }

            // 语义标签
            f.IsLoud = f.SmoothedDbfs > _loudThreshold;
            f.IsSilent = f.SmoothedDbfs < _silentThreshold;

            // 保存
            Global = new GlobalAudioFeatures
            {
                Main = f,
                HasAudio = true
            };

            _prev = f;
            _hasPrev = true;
        }

        private void UpdateNoAudio()
        {
            // 延续上一帧，并标记未跟踪
            if (_hasPrev)
            {
                _prev.FramesSinceSeen++;
                _prev.IsTracked = false;

                Global = new GlobalAudioFeatures
                {
                    Main = _prev,
                    HasAudio = false
                };
            }
            else
            {
                Global = default;
            }
        }
    }
}

