using UnityEngine;

/// <summary>
/// 简单的调试脚本：
/// - 每隔一段时间从 IHandsInput 拉取数据；
/// - 在 Console 打印当前检测到的手的数量，
///   以及主手某一个关键点的坐标（x,y,z,px,py）。
/// </summary>
public class HandsDebugPrinter : MonoBehaviour
{
    [Tooltip("从场景中拖一个 HandsInputSource 进来")]
    [SerializeField] private HandsInputSource _handsSource;

    [Tooltip("打印间隔（秒），避免每帧刷屏")]
    [SerializeField] private float _logInterval = 0.5f;

    private float _timer;

    private void Update()
    {
        if (_handsSource == null) return;
        if (!_handsSource.HasData) return;

        _timer += Time.deltaTime;
        if (_timer < _logInterval) return;
        _timer = 0f;

        int count = _handsSource.HandCount;
        if (count == 0)
        {
            Debug.Log("[HandsDebugPrinter] 当前没有检测到手。");
            return;
        }

        if (_handsSource.TryGetPrimaryHand(out var hand))
        {
            // 以 index 8（食指尖）为例
            int lmIndex = 8;

            if (hand.landmarks != null && hand.landmarks.Length > lmIndex)
            {
                Landmark lm = hand.landmarks[lmIndex];

                Debug.Log(
                    $"[HandsDebugPrinter] hands={count}, " +
                    $"primary={hand.handedness}, " +
                    $"lm[{lmIndex}] => x={lm.x:F3}, y={lm.y:F3}, z={lm.z:F3}, " +
                    $"px={lm.px}, py={lm.py}"
                );
            }
            else
            {
                Debug.Log($"[HandsDebugPrinter] hands={count}, 但主手 landmarks 数据不足。");
            }
        }
        else
        {
            Debug.Log($"[HandsDebugPrinter] hands={count}, 但未找到主手。");
        }
    }
}

