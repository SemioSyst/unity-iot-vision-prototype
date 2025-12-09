Shader "ShaderDuel/TunnelPixelBackground"
{
    Properties
    {
        _PixelDensity ("Pixel Density", Range(20, 400)) = 120

        _Center ("Tunnel Center", Vector) = (0.5, 0.5, 0, 0)

        _InnerColor ("Inner Color", Color) = (0.05, 0.05, 0.08, 1)
        _OuterColor ("Outer Color", Color) = (0.0, 0.0, 0.0, 1)

        _EnergyColor ("Energy Color", Color) = (0.6, 0.2, 0.9, 1)
        _EnergyIntensity ("Energy Intensity", Range(0, 2)) = 1.0

        _WallInner ("Wall Inner Radius", Range(0, 1)) = 0.25
        _WallOuter ("Wall Outer Radius", Range(0, 1)) = 0.95
        _WallEdge  ("Wall Edge Softness", Range(0, 0.5)) = 0.1
        _TunnelRadius ("Tunnel Radius", Range(0.3, 3)) = 1.2
        _TunnelCurve  ("Tunnel Brightness Curve", Range(0.2, 4)) = 1.0

        _FlowSpeed   ("Flow Speed",   Range(0, 5))  = 1.0
        _FlowRadial  ("Radial Flow",  Range(0, 10)) = 3.0
        _FlowAngular ("Angular Flow", Range(0, 10)) = 2.0

        _RadialCells  ("Radial Cells",  Range(4, 64))   = 24
        _AngularCells ("Angular Cells", Range(8, 128))  = 72

        _ActiveThreshold ("Active Threshold", Range(-1, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Background"
        }

        LOD 100

        Pass
        {
            Name "Unlit"

            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define PI      3.14159265
            #define TWO_PI  6.28318530

            float _PixelDensity;

            float4 _Center;
            float4 _InnerColor;
            float4 _OuterColor;
            float4 _EnergyColor;
            float  _EnergyIntensity;

            float _WallInner;
            float _WallOuter;
            float _WallEdge;

            float _TunnelRadius;
            float _TunnelCurve;

            float _FlowSpeed;
            float _FlowRadial;
            float _FlowAngular;

            float _RadialCells;
            float _AngularCells;
            float _ActiveThreshold;

            // 简单 hash: 输入 2D，输出 [0,1)
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

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 伪像素对齐
                float2 uv = i.uv;
                float2 pixelUV = floor(uv * _PixelDensity) / _PixelDensity;

                // 2. 隧道中心坐标
                float2 center = _Center.xy;

                // 纠正宽高比：x 方向乘以 (宽/高)
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = float2((pixelUV.x - center.x) * aspect,
                                  (pixelUV.y - center.y));

                float r = length(p) + 1e-4;
                float angle   = atan2(p.y, p.x);        // [-PI, PI]
                float angle01 = angle / TWO_PI + 0.5;   // [0,1]

                // 3. 基础隧道渐变（中心略亮、外圈接近黑）
                // r 先按半径归一化到 [0,1]
                float normR = saturate(r / _TunnelRadius);

                // 1 - normR 是“越靠近中心越大”，再通过幂次控制曲线形状
                float tunnelFactor = pow(1.0 - normR, _TunnelCurve);

                float4 baseColor = lerp(_OuterColor, _InnerColor, tunnelFactor);

                // 4. 隧道壁 mask
                float innerMask = smoothstep(_WallInner - _WallEdge, _WallInner + _WallEdge, r);
                float outerMask = 1.0 - smoothstep(_WallOuter - _WallEdge, _WallOuter + _WallEdge, r);
                float wallMask = innerMask * outerMask;

                // 5. 环×扇区格子划分
                float radialIndex  = floor(r * _RadialCells);
                float angularIndex = floor(angle01 * _AngularCells);
                float2 cellId = float2(radialIndex, angularIndex);
                float  cellRandom = hash21(cellId);

                // 6. 能量流动相位
                float t = _Time.y * _FlowSpeed;
                float flowPhase = 0.0;
                flowPhase += r * _FlowRadial;
                flowPhase += angle01 * _FlowAngular;
                flowPhase += cellRandom * TWO_PI;
                flowPhase += t;

                float flowValue = sin(flowPhase);       // [-1,1]

                // 7. 判断这一格是否“有能量被挤出”
                float active = smoothstep(_ActiveThreshold, 1.0, flowValue);
                active *= wallMask;

                // 8. 混合紫色能量与背景颜色
                float energyAmount = active * _EnergyIntensity;
                float4 finalColor = lerp(baseColor, _EnergyColor, energyAmount);

                return finalColor;
            }
            ENDCG
        }
    }
}
