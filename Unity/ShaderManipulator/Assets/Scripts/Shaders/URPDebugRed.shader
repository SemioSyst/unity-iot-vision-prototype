Shader "Custom/URPDebugRed"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"    = "Opaque"
            "Queue"         = "Geometry"
            "RenderPipeline"= "UniversalRenderPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off          // 两面都画
            ZWrite On         // 写深度，挡住后面的板
            Blend One Zero    // 不透明

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            // 关键：把材质属性放到 UnityPerMaterial 里
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return _BaseColor;   // 纯色输出
            }

            ENDHLSL
        }
    }
}
