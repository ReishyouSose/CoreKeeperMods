Shader "Zenith/ZenithBladeLight"
{
    Properties
    {
        _Image0 ("Shape", 2D) = "white" {}
        _Image1 ("Erosion", 2D) = "gray" {}
        _Data ("Shader Data", Vector) = (4, 0, 0, 4)
        _Opacity ("Opacity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        // 原版使用的是非预乘的 Alpha 混合，或者 Additive 混合
        Blend SrcAlpha OneMinusSrcAlpha 

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _Image0;
            sampler2D _Image1;
            float4 _Data;
            float _Opacity;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv; 
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 coord = i.uv;
                float4 color = i.color;
                
                // 1. 还原原版的 s 和 u 计算
                float s = coord.x * _Data.x + _Data.y; // 对应原版 s = coord.x * 4
                float f = frac(abs(s));
                // 原版有一个符号处理，如果 s > 0，取 f，否则取 -f
                float u = (s >= 0.0) ? f : -f;
                
                // 2. 还原原版的 threshold
                float threshold = coord.x * 1.5 - 0.5;
                
                // 3. 还原原版的 fade
                float fade = 1.0 - s / _Data.w; // _Data.w = 4
                
                // 4. 取样
                float2 uv = float2(u, coord.y);
                float4 maskTex = tex2D(_Image1, uv);
                float4 base = tex2D(_Image0, uv);
                
                // 5. 核心颜色混合 (原版只取 base.x 即 R 通道)
                float4 col = base.x * color * _Opacity;
                
                // 6. 侵蚀遮罩 (原版的 saturate 两倍反差)
                col *= saturate(2.0 * (maskTex.x - threshold));
                
                // 7. 最终渐隐
                return col * fade;
            }
            ENDCG
        }
    }
}