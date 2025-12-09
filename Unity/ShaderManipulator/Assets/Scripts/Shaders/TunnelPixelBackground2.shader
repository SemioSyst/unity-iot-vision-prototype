Shader "Custom/TunnelPixelBackgroundURP"
{
    Properties
    {
        _InnerColor   ("Inner Color",  Color) = (0.05, 0.00, 0.20, 1)
        _OuterColor   ("Outer Color",  Color) = (0.40, 0.00, 0.60, 1)
        _EnergyColor  ("Energy Color", Color) = (1.00, 0.80, 0.20, 1)

        _TunnelSpeed  ("Tunnel Speed",  Float) = 1.0
        _EnergyAmount ("Energy Amount", Range(0, 1)) = 0.7

        // 中心位置（0–1 UV 空间）
        _Center       ("Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        // 中心半径，控制「洞」的大小
        _CenterRadius ("Center Radius", Float) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Background"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            HLSLPROGRAM

            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _InnerColor;
                float4 _OuterColor;
                float4 _EnergyColor;

                float  _TunnelSpeed;
                float  _EnergyAmount;

                float4 _Center;       // xy 用；zw 忽略
                float  _CenterRadius;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                // ---- 基础径向渐变（从中心到外面） ----
                float2 dir  = uv - _Center.xy;
                float  dist = length(dir);

                // 0 在中心、1 在外圈
                float t = saturate( (dist - _CenterRadius) / (1.0 - _CenterRadius) );
                float4 baseColor = lerp(_InnerColor, _OuterColor, t);

                // ---- 简单的“能量波动” ----
                // 用距离 + 角度 + 时间 做一个动的波纹
                float angle = atan2(dir.y, dir.x);          // -PI ~ PI
                float time  = _Time.y * _TunnelSpeed;

                float wave = sin(10.0 * dist - 4.0 * time + angle * 2.0);
                float energy = saturate(wave * 0.5 + 0.5);  // 映射到 0–1

                float4 color = lerp(baseColor, _EnergyColor, energy * _EnergyAmount);

                // 完全 Unlit，不参与光照
                return color;
            }

            ENDHLSL
        }
    }
}
