// 天顶剑刀光。像素部分对照你给的 Zenith.fx（pass 名 FinalFractalVertex）。
// 泰拉贴图 V 从上往下增大；Unity 的 V 从下往上增大。顶点里把剑身方向的 V 翻成 1-across，
// 这样剑尖仍采到形状图亮边，剑根采到暗边。沿刀光的 U 不翻，左右两边都是朝右为正。
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
                float3 uv : TEXCOORD0;
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
                // 这里的 uv.x 是 0~1 的进度，uv.y 是剑根到剑尖的横向进度
                o.uv = float2(v.uv.x, v.uv.y / max(0.001, v.uv.z)); 
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 coord = i.uv;
                float4 color = i.color;
                
                // 1. 纯正的渐隐：随着进度 x 增加，整体变暗
                float fade = 1.0 - coord.x;
                
                // 2. 取形状图（剑的实体形状），不折叠 U，直接采样
                float4 base = tex2D(_Image0, coord);
                
                // 3. 取噪声图，如果想让噪声流动，可以用 _Time.x 让 X 轴滚动
                float noise = tex2D(_Image1, float2(coord.x * 2.0 - _Time.x * 2.0, coord.y)).r;
                
                // 4. 只在尾部削去一点形状，模拟“被侵蚀”的感觉
                float erosion = smoothstep(0.1, 0.6, noise * (1.0 - coord.x)); 
                
                // 5. 叠加颜色
                float4 col = base.r * color; // 形状只乘一次 color
                
                // 6. 应用侵蚀和渐隐
                col *= fade;
                col *= 1.0 - saturate(erosion); // 侵蚀掉尾部
                
                return col;
            }
            ENDCG
        }
    }
}