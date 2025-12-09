Shader "Custom/DebugRed"
{
    SubShader
    {
        // 普通不透明几何体
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        // 关闭剔除和深度写入，永远画在前面
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            // 固定管线，直接填一个颜色
            Color (1, 0, 0, 1)
        }
    }
}
