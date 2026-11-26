// Şehir içi harita (Game/CityView.cs): Tools/build_city.py'nin ürettiği koyu temalı sokak haritası dokusu.
// Işıktan etkilenmez; gece-gündüz döngüsünde (_BT_Daylight, WorldRenderer) biraz kararır ama hep okunur kalır.
// Kenarlarda harita yumuşakça kararır: dokunun bittiği yer sert bir çizgi gibi görünmesin.
Shader "BusTycoon/CityMap"
{
    Properties
    {
        _MainTex ("Map", 2D) = "black" {}
        _Edge ("Edge fade (uv)", Float) = 0.015
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Lighting Off
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Edge;
            half _BT_Daylight;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 c = tex2D(_MainTex, i.uv).rgb;
                // Gece: en çok %35 kararır (_BT_Daylight 1 gündüz, 0 gece; WorldRenderer açılışta 1 yazar)
                half day = saturate(_BT_Daylight);
                c *= lerp(0.65, 1.0, day);
                float2 d = min(i.uv, 1.0 - i.uv);
                float edge = saturate(min(d.x, d.y) / max(_Edge, 1e-4));
                c *= lerp(0.35, 1.0, edge);
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
