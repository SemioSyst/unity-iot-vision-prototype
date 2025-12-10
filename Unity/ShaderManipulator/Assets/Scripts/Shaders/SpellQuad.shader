Shader "ShaderDuel/SpellQuad"
{
    Properties
    {
        // 掌心能量核颜色 & 拖尾颜色
        _CoreColor   ("Core Color", Color) = (0.9, 0.7, 1.0, 1.0)
        _TrailColor  ("Trail Color", Color) = (0.8, 0.5, 1.0, 1.0)

        // 能量核大小/边缘硬度
        _CoreRadius  ("Core Radius", Range(0.01, 0.3)) = 0.08
        _CoreEdge    ("Core Edge Sharpness", Range(0.5, 4.0)) = 2.0

        // 像素感 & glitch 相关
        _PixelDensity   ("Pixel Density", Range(20, 400)) = 140
        _GlitchScale    ("Glitch Cell Scale", Range(5, 200)) = 60
        _GlitchSpeed    ("Glitch Speed", Range(0, 20)) = 6
        _GlitchStrength ("Glitch Strength", Range(0, 1)) = 0.8

        // 拖尾参数
        _TrailLength   ("Trail Length", Range(0.02, 0.6)) = 0.18
        _TrailWidth    ("Trail Width", Range(0.005, 0.2)) = 0.04
        _TrailSoftness ("Trail Softness", Range(0.5, 4.0)) = 2.0

        // 整体强度
        _GlobalIntensity ("Global Intensity", Range(0, 3)) = 1.0

        // ---- Energy Wall & Combat ----
        _HasEnergyWall              ("Has Energy Wall", Float) = 0
        _WallCenterUV               ("Wall Center UV", Vector) = (0.5, 0.5, 0, 0)
        _WallSizeUV                 ("Wall Size UV", Vector)   = (0.4, 0.4, 0, 0)
        _WallPhase                  ("Wall Phase", Float)      = 0

        _ShieldBoostByBossAttack01  ("Shield Boost By Boss Attack", Range(0,1)) = 0
        _Guarded                    ("Guarded", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        LOD 100

        Pass
        {
            Name "SpellLayer"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha   // 透明混合

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define WALLPHASE_ARMED      0.0
            #define WALLPHASE_CHANNELING 1.0
            #define WALLPHASE_FADE       2.0

            #define PI      3.14159265
            #define TWO_PI  6.28318530

            // ==== 来自 SpellQuadController 的属性 ====
            // 注意：名字必须和 SpellQuadController 里的字符串一致
            float4 _LeftPalmPos;      // xy = 0~1 屏幕坐标
            float4 _RightPalmPos;
            float  _LeftPalmVisible;
            float  _RightPalmVisible;

            // ==== 掌心视觉参数 ====
            float4 _CoreColor;
            float4 _TrailColor;

            float  _CoreRadius;
            float  _CoreEdge;

            float  _PixelDensity;
            float  _GlitchScale;
            float  _GlitchSpeed;
            float  _GlitchStrength;

            float  _TrailLength;
            float  _TrailWidth;
            float  _TrailSoftness;

            float  _GlobalIntensity;

            // ==== Energy Wall & Combat ====
            float  _HasEnergyWall;
            float4 _WallCenterUV;
            float4 _WallSizeUV;
            float  _WallPhase;

            float  _ShieldBoostByBossAttack01;
            float  _Guarded;

            // 2D hash，用于 glitch / 像素闪烁
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
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

            // Armed 阶段：掌心的蓝色像素能量球
            float4 RenderBluePalmOrb(float2 uv, float2 palmPos01, float visible)
            {
                if (visible <= 0.0001)
                    return float4(0,0,0,0);

                // 像素化
                float2 pixelUV = floor(uv * _PixelDensity) / _PixelDensity;

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = float2((pixelUV.x - palmPos01.x) * aspect,
                                  (pixelUV.y - palmPos01.y));
                float r = length(p) + 1e-6;

                float radius   = _CoreRadius * 0.8;  // 比默认掌心稍小一点
                float coreMask = saturate(1.0 - r / radius);
                coreMask = pow(coreMask, _CoreEdge);

                // 轻微呼吸 + glitch 抖动，用 _Time 自己驱动
                float t = _Time.y;
                float breathe = 0.85 + 0.15 * sin(t * 2.0 + palmPos01.x * 10.0);

                float2 glitchCell = floor(pixelUV * (_GlitchScale * 0.7));
                float n = hash21(glitchCell + _Time.y * (_GlitchSpeed * 0.7));
                float glitchMask = lerp(0.7, 1.3, n);

                coreMask *= breathe * glitchMask;

                // 蓝色能量
                float3 colA = float3(0.1, 0.3, 0.7);
                float3 colB = float3(0.4, 0.8, 1.4);
                float3 col  = lerp(colA, colB, coreMask);

                float alpha = coreMask * visible * _GlobalIntensity;
                return float4(col, alpha);
            }

                        // 计算一块矩形盾牌的主体 mask（0~1）
            float ShieldBodyMask(float2 uv)
            {
                float2 center = _WallCenterUV.xy;
                float2 halfSize = max(_WallSizeUV.xy * 0.5, float2(1e-4, 1e-4));

                float2 p = (uv - center) / halfSize;
                float d = max(abs(p.x), abs(p.y)); // 方形体积

                float mask = smoothstep(1.0, 0.9, d);
                return mask;
            }

            // Channeling 阶段：展开中的护盾（用 _Time 做循环“展开感”）
            float4 RenderWallChanneling(float2 uv)
            {
                float body = ShieldBodyMask(uv);
                if (body <= 0.001)
                    return float4(0,0,0,0);

                float2 pixelUV = floor(uv * _PixelDensity) / _PixelDensity;

                // 盾面内部的流动噪声
                float2 flowUV = pixelUV * 25.0;
                float t = _Time.y;
                float band = sin(flowUV.y * 3.0 + t * 1.8);
                float n   = hash21(flowUV + t * 0.5);

                float flow = saturate(0.4 + 0.3 * band + 0.3 * n);

                // 基础蓝色
                float3 baseColA = float3(0.15, 0.3, 0.6);
                float3 baseColB = float3(0.3, 0.7, 1.0);
                float3 col = lerp(baseColA, baseColB, flow);

                // Boss 攻击时的整体加亮
                float boost = saturate(_ShieldBoostByBossAttack01);
                col *= (1.0 + boost * 2.0);

                // 叠加几圈波纹（从中心扩散）
                float2 center = _WallCenterUV.xy;
                float dist = length(uv - center);
                float ripplePhase = dist * 40.0 - t * 8.0;
                float ripple = sin(ripplePhase);
                ripple = smoothstep(0.7, 1.0, ripple);  // 只保留波峰
                ripple *= exp(-dist * 6.0);             // 越远越淡

                // 波纹强度也受 boost 控制：Boss 攻击时更明显
                col += ripple * boost * 1.5;

                float alpha = body * 0.75 * _GlobalIntensity;
                return float4(col, alpha);
            }

            // Fade 阶段：像素崩解（用 _Time 自己走一段 0~1）
            float4 RenderWallFade(float2 uv)
            {
                float body = ShieldBodyMask(uv);
                if (body <= 0.001)
                    return float4(0,0,0,0);

                // 盾体上的像素格子
                float2 cell = floor(uv * (_PixelDensity * 0.6));
                float rnd = hash21(cell);

                // 用时间做一个循环的 fadeT（0~1 再回0）
                float t = _Time.y;
                float fadeT = saturate(0.5 + 0.5 * sin(t * 1.5)); // 简单循环

                // fadeT 越大，被删掉的像素越多
                if (rnd < fadeT)
                    return float4(0,0,0,0);

                // 残留的像素，随 fadeT 逐渐变暗、透明
                float3 col = float3(0.3, 0.7, 1.0) * (1.0 - fadeT);
                float alpha = body * (1.0 - fadeT) * 0.8 * _GlobalIntensity;

                return float4(col, alpha);
            }

            // 渲染能量墙效果总调度
            float4 RenderEnergyWall(float2 uv)
            {
                if (_HasEnergyWall < 0.5)
                    return float4(0,0,0,0);

                // 把 Phase 近似取整
                float phase = round(_WallPhase);

                // Armed：掌心蓝色能量球（不画 trail）
                if (abs(phase - WALLPHASE_ARMED) < 0.5)
                {
                    float4 col = 0;
                    col += RenderBluePalmOrb(uv, _LeftPalmPos.xy,  _LeftPalmVisible);
                    col += RenderBluePalmOrb(uv, _RightPalmPos.xy, _RightPalmVisible);
                    return col;
                }

                // Channeling：展开的护盾 + Boss 攻击波纹
                if (abs(phase - WALLPHASE_CHANNELING) < 0.5)
                {
                    return RenderWallChanneling(uv);
                }

                // Fade：像素崩解
                if (abs(phase - WALLPHASE_FADE) < 0.5)
                {
                    return RenderWallFade(uv);
                }

                // 其它未知 Phase 就先透明
                return float4(0,0,0,0);
            }


            // 渲染单只掌心的能量核 + 拖尾
            // uv        : 当前像素的屏幕坐标 (0~1)
            // palmPos01 : 掌心坐标 (0~1)
            // visible   : 该掌心的可见度 [0,1]
            // 渲染单只掌心的能量核 + 像素拖尾（胶囊形）
            float4 RenderPalm(float2 uv, float2 palmPos01, float visible)
            {
                if (visible <= 0.0001)
                    return float4(0,0,0,0);

                // --- 1. 像素化坐标 ---
                float2 pixelUV = floor(uv * _PixelDensity) / _PixelDensity;

                // --- 2. 能量核（圆形，考虑宽高比） ---
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 pCore = float2((pixelUV.x - palmPos01.x) * aspect,
                                      (pixelUV.y - palmPos01.y));
                float rCore = length(pCore) + 1e-6;

                float coreNorm = saturate(1.0 - rCore / _CoreRadius);
                coreNorm = pow(coreNorm, _CoreEdge);  // 控制边缘硬/软

                // --- 3. glitch 像素闪烁（基于像素格子） ---
                float2 glitchCell = floor(pixelUV * _GlitchScale);
                float  n = hash21(glitchCell + _Time.y * _GlitchSpeed);
                float  glitchMask = lerp(1.0, n * 2.0, _GlitchStrength);
                coreNorm *= glitchMask;

                // --- 4. 拖尾（胶囊形：线段 + 圆头），同样用像素坐标 ---
                // 拖尾方向：从屏幕中心指向掌心（即能量往外拖）
                float2 center = float2(0.5, 0.5);
                float2 dirTrail = normalize(palmPos01 - center + 1e-6); // uv 空间

                // 将当前像素转到以掌心为起点的坐标系（uv 空间）
                float2 v = pixelUV - palmPos01;

                // 线段参数：0 到 _TrailLength
                float proj = dot(v, dirTrail);                           // 在拖尾方向上的投影长度
                float t = clamp(proj, 0.0, _TrailLength);                // 限制在线段范围内
                float2 closest = dirTrail * t;                           // 线段上最近点
                float2 diff = v - closest;                               // 到线段的向量差
                float distToSegment = length(diff);                      // 到线段（含两端圆头）的距离

                // 胶囊半径用 TrailWidth 控制
                float trailRadius = _TrailWidth;
                float trailNorm = saturate(1.0 - distToSegment / trailRadius);

                // 沿长度方向再做一次淡出（越远越弱）
                float along01 = saturate(t / _TrailLength);
                float lengthFade = 1.0 - pow(along01, _TrailSoftness);
                trailNorm *= lengthFade;

                // --- 5. 核心 + 拖尾的合成 ---
                float coreAlpha  = coreNorm;
                float trailAlpha = trailNorm * 0.7;  // 拖尾略淡

                // 颜色合成：先按各自 alpha 叠加，再归一化，避免中间区域太爆
                float alphaCombined = coreAlpha + trailAlpha;
                alphaCombined = saturate(alphaCombined);

                float3 colCombined = float3(0,0,0);
                if (alphaCombined > 1e-5)
                {
                    float3 coreCol  = _CoreColor.rgb;
                    float3 trailCol = _TrailColor.rgb;

                    // 按贡献占比混合颜色
                    float wCore  = coreAlpha  / alphaCombined;
                    float wTrail = trailAlpha / alphaCombined;

                    colCombined = coreCol * wCore + trailCol * wTrail;
                }

                // --- 6. 掌心 visible & 全局强度 ---
                colCombined  *= visible * _GlobalIntensity;
                alphaCombined *= visible * _GlobalIntensity;

                return float4(colCombined, alphaCombined);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float4 spellColor = float4(0,0,0,0);

                bool hasWall = (_HasEnergyWall > 0.5);

                // 如果当前没有任何激活的法术 → 显示 Idle 掌心特效
                if (!hasWall)
                {
                    spellColor += RenderPalm(uv, _LeftPalmPos.xy,  _LeftPalmVisible);
                    spellColor += RenderPalm(uv, _RightPalmPos.xy, _RightPalmVisible);
                }
                else
                {
                    // 当前有能量墙 → 只显示能量墙，不再显示 Idle 掌心特效
                    spellColor += RenderEnergyWall(uv);
                }

                spellColor.a = saturate(spellColor.a);
                return spellColor;
            }

            ENDCG
        }
    }
}
