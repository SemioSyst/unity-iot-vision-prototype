using System.Collections.Generic;
using UnityEngine;

namespace ShaderDuel.Gameplay
{
    /// <summary>
    /// 条件计时器：用字符串 key 为某个“条件”建立一条时间轴，
    /// 根据每帧传入的布尔值自动累积「连续为 true 的时间」。
    /// 典型用法：
    /// float held = ConditionTimer.Update("Right:FistForward", cond);
    /// if (held >= 0.3f) { /* 进入某个输入窗口 */ }
    /// </summary>
    public static class ConditionTimer
    {
        /// <summary>
        /// 每个 key 对应一条「条件持续时间」。
        /// key 可以是任意有区分度的字符串，例如：
        /// "Right:FistForward", "Spell:SmallBlast:Prepare" 等。
        /// </summary>
        private static readonly Dictionary<string, float> _durations
            = new Dictionary<string, float>();

        /// <summary>
        /// 每个 key 对应一条「条件宽限时间」。
        /// 用于在条件变为 false 后仍然允许短暂保留为 true 的时间窗口。
        /// </summary>
        private static readonly Dictionary<string, float> _graceRemain
            = new Dictionary<string, float>();

        /// <summary>
        /// 更新某个条件的计时，并返回该条件当前已经为 true 持续了多久（秒）。
        /// 
        /// - 如果 isTrue == true，则在上一帧的基础上累加 Time.deltaTime；
        /// - 如果 isTrue == false，则计时清零，从 0 重新开始。
        /// </summary>
        /// <param name="key">该条件的唯一标识字符串。</param>
        /// <param name="isTrue">本帧条件是否为真。</param>
        /// <returns>该条件当前连续为 true 的时间（秒）。</returns>
        public static float Update(string key, bool isTrue)
        {
            float current = 0f;
            _durations.TryGetValue(key, out current);

            if (isTrue)
            {
                current += Time.deltaTime;
            }
            else
            {
                current = 0f;
            }

            _durations[key] = current;
            return current;
        }

        /// <summary>
        /// 更新某个条件的计时，并返回该条件当前已经为 true 持续了多久（秒）。
        /// 
        /// - 如果 isTrue == true，则在上一帧的基础上累加 Time.deltaTime；
        /// - 如果 isTrue == false，则先检查是否在宽限时间内：
        ///   若在宽限时间内，则不清零计时，只减少宽限时间；
        ///   若超出宽限时间，则真正清零计时，从 0 重新开始。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="isTrue"></param>
        /// <param name="falseGraceSeconds"></param>
        /// <returns></returns>
        public static float UpdateWithGrace(string key, bool isTrue, float falseGraceSeconds)
        {
            // 当前累计时长
            _durations.TryGetValue(key, out var current);

            // 最近一次 cond 为 true 之后，剩余的“可容忍 false 时间”
            _graceRemain.TryGetValue(key, out var grace);

            if (isTrue)
            {
                // 条件为真时：累加时间，并把宽限时间重置为 falseGraceSeconds。
                current += Time.deltaTime;
                grace = falseGraceSeconds;
            }
            else
            {
                if (grace > 0f)
                {
                    // 条件刚刚变 false 但在宽限窗口内：不清零，只减少宽限时间。
                    grace -= Time.deltaTime;
                    if (grace < 0f) grace = 0f;
                }
                else
                {
                    // 超过宽限时间了：真正清零。
                    current = 0f;
                }
            }

            _durations[key] = current;
            _graceRemain[key] = grace;

            return current;
        }


        /// <summary>
        /// 只读访问某个条件当前的计时，不会根据本帧状态做更新。
        /// 如果该 key 从未被更新过，则返回 0。
        /// </summary>
        public static float Get(string key)
        {
            if (_durations.TryGetValue(key, out var value))
            {
                return value;
            }

            return 0f;
        }

        /// <summary>
        /// 将某个条件的计时强制重置为 0。
        /// （通常不需要显式调用，因为传 isTrue=false 给 Update
        /// 本身就会清零计时；这个方法用于“无论这一帧真假都想手动归零”的场景。）
        /// </summary>
        public static void Reset(string key)
        {
            _durations[key] = 0f;
        }

        /// <summary>
        /// 删除某个条件对应的记录。
        /// 下次 Update / Get 时会按从未出现过的 key 处理。
        /// </summary>
        public static void Clear(string key)
        {
            _durations.Remove(key);
        }

        /// <summary>
        /// 清空所有条件的计时数据。
        /// 一般只在切换场景、重开对局这类“大重置”场景使用。
        /// </summary>
        public static void ClearAll()
        {
            _durations.Clear();
        }
    }
}
