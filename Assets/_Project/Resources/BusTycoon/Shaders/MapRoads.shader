// Harita yolları: köşe rengi, ışıksız, saydam. Şeridin merkezi POSITION'da, kenara uzaklığı (1x ölçekte, dünya XZ)
// TEXCOORD0'da; kalınlık _WidthScale ile çarpılır. Böylece yakınlaştırırken yol ağı yeniden örülmez (takılma olmaz).
// _Color: gece sönükleşmesi ve uzaktan bakınca soluklaşma (WorldRenderer).
Shader "BusTycoon/MapRoads"
{
    Properties
    {
        _WidthScale ("Width Scale", Float) = 1
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Lighting Off
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            float _WidthScale;
            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 offset : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(0)
            };

            v2f vert (appdata v)
            {
                v2f o;
                float4 p = v.vertex;
                p.xz += v.offset * _WidthScale;
                o.pos = UnityObjectToClipPos(p);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = i.color * _Color;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
