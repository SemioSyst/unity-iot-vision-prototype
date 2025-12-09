Shader "ShaderDuel/BossQuad"
{
    Properties
    {
        // 基础配色
        _CoreDarkColor   ("Core Dark Color", Color)   = (0.02, 0.02, 0.04, 1)
        _CoreBrightColor ("Core Bright Color", Color) = (0.4, 0.2, 0.8, 1)
        _FlameColor      ("Flame Color", Color)       = (0.9, 0.7, 1.0, 1)
        _EyeColor        ("Eye Color", Color)         = (1.0, 0.95, 0.9, 1)

        // 几何尺寸 & 像素感
        _BossRadius      ("Boss Radius", Range(0.1, 0.6)) = 0.28
        _PixelDensity    ("Pixel Density", Range(40, 400)) = 180
        _FlameThickness  ("Flame Ring Thickness", Range(0.01, 0.2)) = 0.08

        // 眼睛参数
        _EyeSeparation   ("Eye Separation (UV)", Range(0.0, 0.6)) = 0.20
        _EyeHeight       ("Eye Vertical Offset", Range(-0.3, 0.3)) = 0.03
        _EyeSize         ("Eye Size", Range(0.02, 0.4)) = 0.18
        _EyeRoundness    ("Eye Bottom Roundness", Range(0.0, 1.0)) = 0.6
        _EyeTiltDeg      ("Eye Tilt Angle (deg)", Range(-45, 45)) = 10.0

        // 浮动动画
        _FloatAmplitude  ("Float Amplitude", Range(0.0, 0.3)) = 0.05
        _FloatSpeed      ("Float Speed", Range(0.0, 10.0)) = 1.5
        _EyeLagPhase     ("Eye Lag Phase (0-1)", Range(0.0, 1.0)) = 0.15

        // 火焰 glitch
        _FlameNoiseScale ("Flame Noise Scale", Range(4, 100)) = 30
        _FlameNoiseSpeed ("Flame Noise Speed", Range(0, 20)) = 6
        _FlamePulse      ("Flame Pulse Strength", Range(0, 2)) = 1.0

        // 出场/死亡的像素崩解
        _SpawnFadeSoftness ("Spawn Fade Softness", Range(0.1, 5.0)) = 2.0
        _DeathPixelScale    ("Death Pixel Scale", Range(10, 200)) = 80

        // 整体透明度缩放
        _GlobalAlpha    ("Global Alpha", Range(0, 1)) = 1.0

        // ==== 阶段枚举映射（和 C# EnemyPhase 对应） ====
        // 你可以在 Inspector 里改成和 enum 一致的数值
        _PhaseSpawning  ("Phase Value - Spawning", Float) = 0
        _PhaseAlive     ("Phase Value - Alive",    Float) = 1
        _PhaseAttacking ("Phase Value - Attacking",Float) = 2
        _PhaseDying     ("Phase Value - Dying",    Float) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        LOD 200

        Pass
        {
            Name "BossLayer"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define PI      3.14159265
            #define TWO_PI  6.28318530

            // ==== 来自 BossQuadController 的状态 ====
            float _EnemyId;
            float _EnemyPhase;
            float _EnemyPhaseProgress01;
            float _EnemyHealth01;
            float _EnemyHealthMax;
            float _EnemyAttackCharge01;
            float _EnemyAttackHitPulse01;

            // ==== 颜色 & 几何参数 ====
            float4 _CoreDarkColor;
            float4 _CoreBrightColor;
            float4 _FlameColor;
            float4 _EyeColor;

            float  _BossRadius;
            float  _PixelDensity;
            float  _FlameThickness;

            float  _EyeSeparation;
            float  _EyeHeight;
            float  _EyeSize;
            float  _EyeRoundness;
            float  _EyeTiltDeg;

            float  _FloatAmplitude;
            float  _FloatSpeed;
            float  _EyeLagPhase;

            float  _FlameNoiseScale;
            float  _FlameNoiseSpeed;
            float  _FlamePulse;

            float  _SpawnFadeSoftness;
            float  _DeathPixelScale;

            float  _GlobalAlpha;

            float  _PhaseSpawning;
            float  _PhaseAlive;
            float  _PhaseAttacking;
            float  _PhaseDying;

            // ---- 小工具：hash / 噪声 ----
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            // 极简 value 噪声（分片平滑）
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));

                float2 u = f*f*(3.0 - 2.0*f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // 计算当前阶段是否为 targetPhase（返回 0/1）
            float IsPhase(float phaseValue, float targetPhase)
            {
                return step(0.5, 1.0 - abs(phaseValue - targetPhase)); // 两者相等时≈1
            }

            // 眼睛 SDF：倒三角 + 圆角底
            // 返回 [0,1] 的“填充度”：1 在眼睛内部，0 在外部
            float EyeShapeMask(float2 pLocal, float size, float roundness)
            {
                // 防止 size 为 0
                size = max(size, 1e-5);

                // 归一化到一个固定工作区：大致 [-1,1] × [-1,1]
                float2 q = pLocal / size;

                // ---- 基础倒三角：底部窄，顶部宽 ----
                // y ∈ [-1,1]
                float insideVert = step(-1.0, q.y) * step(q.y, 1.0);

                // 让横向宽度随高度变化：底部窄，上方宽
                // t = 0 (底部 y=-1) → halfWidth ≈ 0.2
                // t = 1 (顶部 y= 1) → halfWidth ≈ 1.0
                float t = saturate((q.y + 1.0) * 0.5);          // [-1,1] → [0,1]
                float halfWidth = lerp(0.2, 1.0, t);            // 可按需要改比例
                float insideHoriz = step(abs(q.x), halfWidth);  // |x| <= halfWidth

                float triMask = insideVert * insideHoriz;

                // ---- 圆角底：在底部加一个大圆角 ----
                float r = lerp(0.0, 0.8, saturate(roundness));  // 圆角半径范围 [0,0.8]
                float2 circleCenter = float2(0.0, -1.0 + r);    // 靠近底边
                float distCircle = length(q - circleCenter);
                float circleMask = step(distCircle, r);

                // 三角 ∪ 圆角
                float baseMask = max(triMask, circleMask);

                // ---- 边缘柔化，让像素边缘不那么硬 ----
                float feather = 0.15; // 软边宽度，可调

                // 仍然用你现在的 edgeDist 粗略估计边缘距离
                float edgeX    = abs(abs(q.x) - halfWidth);
                float edgeYTop = abs(q.y - 1.0);
                float edgeYBot = abs(q.y + 1.0);
                float edgeDist = min(edgeX, min(edgeYTop, edgeYBot));

                // 距离轮廓 0 时 = 0，超过 feather 后 ≈ 1
                float soft = smoothstep(0.0, feather, edgeDist);

                return baseMask * soft;
            }


            // 渲染核心+火焰（不含眼睛）
            float4 RenderCoreAndFlames(float2 uv, float2 bossCenter, float bossRadius,
                                       float phaseSpawn, float phaseAlive, float phaseAttack, float phaseDie)
            {
                // 像素化 UV
                float2 pixelUV = floor(uv * _PixelDensity) / _PixelDensity;

                // 宽高比校正 + 圆心坐标
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = float2((pixelUV.x - bossCenter.x) * aspect,
                                  (pixelUV.y - bossCenter.y));

                float dist = length(p); // 到球心距离

                // 出场阶段：用 EnemyPhaseProgress01 控制整体缩放
                float spawnMask = IsPhase(_EnemyPhase, phaseSpawn);
                float spawnScale = lerp(0.2, 1.0, _EnemyPhaseProgress01); // 由小到正常
                float effectiveRadius = bossRadius * lerp(1.0, spawnScale, spawnMask);


                // ==== 攻击相关：只在 Attacking 阶段启用 ====
                float attackPhaseMask = IsPhase(_EnemyPhase, phaseAttack);

                // 蓄力曲线（0~1），只在 Attacking 阶段有效
                float attackCharge = saturate(_EnemyAttackCharge01) * attackPhaseMask;

                // 最后一小段冲刺（例如 0.8~1 之间）
                float spikeStart  = 0.8;
                float spikeT      = saturate((attackCharge - spikeStart) / (1.0 - spikeStart)); // 0~1

                float coreSpikeScale = lerp(1.0, 1.25, spikeT); // 冲刺时最多放大到 1.25 倍
                effectiveRadius *= coreSpikeScale;

                // 死亡阶段：半径坍缩
                float dieMask = IsPhase(_EnemyPhase, phaseDie);
                float dieScale = lerp(1.0, 0.0, _EnemyPhaseProgress01);
                effectiveRadius *= lerp(1.0, dieScale, dieMask);

                // 内核 / 外圈判断
                float coreRegion = smoothstep(effectiveRadius, effectiveRadius * 0.4, dist);   // 0 在内核中心，1 在外

                // 基础颜色：深色核心 + 随血量与蓄力渐亮
                float healthFactor = _EnemyHealth01; // 0~1
                float chargeFactor = _EnemyAttackCharge01;

                // 攻击命中瞬间的 pulse
                float hitPulse = _EnemyAttackHitPulse01;

                float brightness01 = saturate(0.2 + 0.6 * healthFactor + 0.4 * chargeFactor + 0.8 * hitPulse);
                float4 coreColor = lerp(_CoreDarkColor, _CoreBrightColor, brightness01);

                // 基于蓄力的整体增亮（普通蓄力期：越蓄越亮）
                float coreChargeBoost = 1.0 + attackCharge * 0.8;   // 蓄力最大多 80% 亮度

                // 冲刺段额外增强
                float coreSpikeBoost  = 1.0 + spikeT * 1.2;        // 最后一小段再多 120%

                // 命中瞬间的爆亮（_EnemyAttackHitPulse01 由 C# 做短暂衰减）
                float coreHitBoost    = 1.0 + _EnemyAttackHitPulse01 * 2.0;

                // 合在一起
                float coreAttackBoost = coreChargeBoost * coreSpikeBoost * coreHitBoost;

                // 应用到核心颜色
                coreColor.rgb *= coreAttackBoost;


                // 为了黑洞感，中心更暗，边缘略亮
                float centerDark = saturate(1.0 - (dist / (effectiveRadius + 1e-4)));
                float4 coreFinal = lerp(coreColor, _CoreDarkColor * 0.2, centerDark);

                // ==== 火焰环 ====
                float radiusNorm = dist / (effectiveRadius + 1e-4);

                float ringCenter = 1.0;
                float ringInner = 1.0 - (_FlameThickness * 0.5);
                float ringOuter = 1.0 + (_FlameThickness * 0.5);

                // 基本环形 mask（在 ringInner ~ ringOuter 之间）
                float ringMask = smoothstep(ringOuter, ringCenter, radiusNorm) *
                                 (1.0 - smoothstep(ringCenter, ringInner, radiusNorm));

                // 噪声驱动火焰
                float2 flameUV = p / effectiveRadius; // 归一化极坐标用
                float angle = atan2(p.y, p.x);        // [-PI,PI]
                float flameNoise = valueNoise(float2(angle * _FlameNoiseScale, _Time.y * _FlameNoiseSpeed));
                float radialJitter = (flameNoise - 0.5) * 0.25; // 随机向外抖动一点

                float flameRadius = radiusNorm + radialJitter;
                float flameMask = smoothstep(ringOuter + 0.2, ringCenter, flameRadius) *
                                  (1.0 - smoothstep(ringCenter, ringInner - 0.1, flameRadius));

                // 蓄力阶段火焰更强
                float attackMask = IsPhase(_EnemyPhase, phaseAttack);

                // 普通蓄力期：火焰整体加强
                float flameChargeBoost = 1.0 + attackCharge * _FlamePulse; // _FlamePulse 材质可调

                // 冲刺期额外加强
                float flameSpikeBoost  = 1.0 + spikeT * 2.0;               // 最后一段再翻倍

                // 命中爆亮
                float flameHitBoost    = 1.0 + _EnemyAttackHitPulse01 * 2.5;

                // 合并
                float flameAttackBoost = flameChargeBoost * flameSpikeBoost * flameHitBoost;

                // 只在 Attacking 阶段生效
                flameAttackBoost = lerp(1.0, flameAttackBoost, attackMask);

                // 应用
                flameMask *= flameAttackBoost;

                // 出场阶段火焰从无到有
                float spawnFlame = lerp(0.0, 1.0, _EnemyPhaseProgress01);
                flameMask *= lerp(1.0, spawnFlame, spawnMask);

                // 死亡阶段火焰崩解：用更粗的像素噪声让部分块突然消失
                float deathFlameMask = 1.0;
                if (dieMask > 0.5)
                {
                    float2 deathCell = floor(pixelUV * (_DeathPixelScale * (1.0 + _EnemyPhaseProgress01 * 2.0)));
                    float deathRand = hash21(deathCell);
                    float cutoff = _EnemyPhaseProgress01;  // 越后越多块被删掉
                    deathFlameMask = step(cutoff, deathRand);
                }

                float3 flameCol = _FlameColor.rgb * flameMask * deathFlameMask;

                // 内核 alpha
                float coreAlpha = coreRegion;

                // 火焰 alpha（叠加到外圈）
                float flameAlpha = flameMask * 0.9 * deathFlameMask;

                float3 finalCol = coreFinal.rgb * coreAlpha + flameCol;
                float finalAlpha = coreAlpha + flameAlpha;

                return float4(finalCol, finalAlpha);
            }

            // 渲染眼睛（两个对称的像素倒三角眼）
            float4 RenderEyes(float2 uv, float2 bossCenter, float bossRadius,
                              float phaseSpawn, float phaseAlive, float phaseAttack, float phaseDie)
            {
                // 先做上下浮动（核心和眼睛共享的“整体偏移”）
                float t = _Time.y * _FloatSpeed;
                float floatOffset = sin(t) * _FloatAmplitude;

                // 眼睛比球体稍微滞后一点
                float eyePhaseOffset = _EyeLagPhase * TWO_PI;
                float eyeOffset = sin(t + eyePhaseOffset) * _FloatAmplitude;

                // Boss 中心在 uv 空间默认 (0.5,0.5) 上下浮动
                float2 bossCenterAnim = bossCenter + float2(0, floatOffset);

                // 眼睛整体中心位置（再加上自己的滞后）
                float2 eyeBaseCenter = bossCenter + float2(0, floatOffset + eyeOffset + _EyeHeight);

                // 像素化坐标
                float2 pixelUV = floor(uv * _PixelDensity) / _PixelDensity;

                // 宽高比校正
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // ---- 限制眼睛只在 Boss 球体范围内 ----
                //float2 pToBoss = float2((pixelUV.x - bossCenterAnim.x) * aspect,
                //                        (pixelUV.y - bossCenterAnim.y));
                //float distToBoss = length(pToBoss);
                //if (distToBoss > bossRadius * 1.05)
                //{
                //    return float4(0,0,0,0); // 超出球体外圈就不画眼睛
                //}

                // 左右眼中心（以 boss 中心为参考）
                float dx = _EyeSeparation * 0.5;
                float2 leftEyeCenterUV  = eyeBaseCenter + float2(-dx, 0);
                float2 rightEyeCenterUV = eyeBaseCenter + float2( dx, 0);

                // 将当前像素转到每只眼睛的局部空间
                float2 toLeft  = float2((pixelUV.x - leftEyeCenterUV.x) * aspect,
                                        (pixelUV.y - leftEyeCenterUV.y));
                float2 toRight = float2((pixelUV.x - rightEyeCenterUV.x) * aspect,
                                        (pixelUV.y - rightEyeCenterUV.y));

                float angleRad = radians(_EyeTiltDeg);
                float c = cos(angleRad);
                float s = sin(angleRad);

                // 左眼：+angle（比如顺时针）
                // 旋转矩阵 R(+a) = [[ c, -s ],
                //                    [ s,  c ]]
                float2 pL = float2(
                    c * toLeft.x  - s * toLeft.y,
                    s * toLeft.x  + c * toLeft.y
                );

                // 右眼：-angle（反方向）
                // 旋转矩阵 R(-a) = [[  c,  s ],
                //                    [ -s, c ]]
                float2 pR = float2(
                    c * toRight.x + s * toRight.y,
                   -s * toRight.x + c * toRight.y
                );

                // 出场/死亡控制眼睛开合程度：0=闭眼,1=完全睁开
                float spawnMask = IsPhase(_EnemyPhase, phaseSpawn);
                float dieMask   = IsPhase(_EnemyPhase, phaseDie);
                float aliveMask = IsPhase(_EnemyPhase, phaseAlive) + IsPhase(_EnemyPhase, phaseAttack);

                float openSpawn = _EnemyPhaseProgress01;               // 出场时逐渐睁开
                float closeDie  = 1.0 - _EnemyPhaseProgress01;         // 死亡时逐渐闭合
                float openAlive = 1.0;                                 // 常态完全睁开

                float eyeOpen = 0.0;
                eyeOpen += spawnMask * openSpawn;
                eyeOpen += aliveMask * openAlive;
                eyeOpen += dieMask   * closeDie;
                eyeOpen = saturate(eyeOpen);

                // 蓄力时眼睛轻微缩小（用 EnemyAttackCharge01）
                float chargeShrink = lerp(1.0, 0.8, _EnemyAttackCharge01);

                // 在最后一小段冲刺放大（靠 AttackCharge01 的最后 0.2）
                float spikeStart = 0.8;
                float spikeT = saturate((_EnemyAttackCharge01 - spikeStart) / (1.0 - spikeStart));
                float spikeScale = lerp(1.0, 1.4, spikeT); // 突然放大

                float eyeScale = _EyeSize * eyeOpen * chargeShrink * spikeScale;

                // 用 EyeShapeMask 计算左右眼填充度
                float maskL = EyeShapeMask(pL, eyeScale, _EyeRoundness);
                float maskR = EyeShapeMask(pR, eyeScale, _EyeRoundness);

                float eyeMask = saturate(maskL + maskR);

                // 攻击命中时让眼睛也爆亮
                float hitPulse = _EnemyAttackHitPulse01;
                float brightness = 1.0 + hitPulse * 1.5;

                float3 eyeColor = _EyeColor.rgb * eyeMask * brightness;
                float eyeAlpha  = eyeMask;

                return float4(eyeColor, eyeAlpha);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Boss 默认中心在屏幕正中
                float2 bossCenter = float2(0.5, 0.5);
                float  bossRadius = _BossRadius;

                // 每帧累积颜色/alpha
                float4 col = float4(0,0,0,0);

                // 依次渲染核心+火焰、眼睛
                float4 coreFlame = RenderCoreAndFlames(uv, bossCenter, bossRadius,
                                                       _PhaseSpawning, _PhaseAlive, _PhaseAttacking, _PhaseDying);
                float4 eyes      = RenderEyes(uv, bossCenter, bossRadius,
                                              _PhaseSpawning, _PhaseAlive, _PhaseAttacking, _PhaseDying);

                // 简单 alpha 混合（眼睛在上）
                col.rgb += coreFlame.rgb;
                col.a   += coreFlame.a;

                col.rgb = lerp(col.rgb, eyes.rgb, eyes.a); // 眼睛不遮掉整个球，只是颜色覆盖
                col.a   = saturate(max(col.a, eyes.a));

                // 全局 alpha & 阶段淡入淡出
                float phaseAlpha = 1.0;

                // 出场：从 0 → 1
                float spawnMask = IsPhase(_EnemyPhase, _PhaseSpawning);
                phaseAlpha = lerp(phaseAlpha, _EnemyPhaseProgress01, spawnMask);

                // 死亡：从 1 → 0
                float dieMask = IsPhase(_EnemyPhase, _PhaseDying);
                phaseAlpha = lerp(phaseAlpha, 1.0 - _EnemyPhaseProgress01, dieMask);

                col.a *= phaseAlpha * _GlobalAlpha;
                // 不再额外乘 col.rgb *= col.a;

                return col;
            }
            ENDCG
        }
    }
}
