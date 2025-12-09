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

                // 最终颜色/透明度累加器
                float4 spellColor = float4(0,0,0,0);

                // === 掌心法术层：左右手 ===
                spellColor += RenderPalm(uv, _LeftPalmPos.xy,  _LeftPalmVisible);
                spellColor += RenderPalm(uv, _RightPalmPos.xy, _RightPalmVisible);

                // === TODO: 这里可以继续叠加其它法术效果 ===
                // 例如：
                // spellColor = RenderEnergyWall(spellColor, uv, ...);
                // spellColor = RenderProjectile(spellColor, uv, ...);

                // 限制 alpha 在 [0,1]
                spellColor.a = saturate(spellColor.a);
                return spellColor;
            }
            ENDCG
        }
    }
}
