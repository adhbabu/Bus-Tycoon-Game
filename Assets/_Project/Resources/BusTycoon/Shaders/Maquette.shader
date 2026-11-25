// Şehir haritasındaki ünlü mekân maketleri (Game/LandmarkModels.cs): gri, mat, biraz saydam. Parlama ve halka yok.
// Önce yalnız derinlik yazılır, sonra renk: maketin arkadaki yüzleri öndekilerin içinden görünmez (saydamlık temiz kalır).
// Işık sabit yönden (kamera ışığı kullanılmaz): yan yüzler biraz koyu, üst yüzler açık. _Fade kamera uzaklığıyla gelir.
Shader "BusTycoon/Maquette"
{
    Properties
    {
        _Color ("Color", Color) = (0.76, 0.79, 0.81, 0.78)
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+30" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Lighting Off

        // Yalnız derinlik. Köşe dönüşümü renk geçişiyle aynı (UnityObjectToClipPos): derinlikler birebir tutar, çizgilenme olmaz.
        Pass
        {
            ZWrite On
            ColorMask 0
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert (float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag () : SV_Target
            {
                return 0;
            }
            ENDCG
        }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Fade;
            half _BT_Daylight;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 normal : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half3 n = normalize(i.normal);
                half3 light = normalize(half3(-0.45, 0.8, 0.38));
                half shade = 0.58 + 0.42 * saturate(dot(n, light)) + 0.08 * saturate(n.y);
                half day = saturate(_BT_Daylight);
                fixed3 c = _Color.rgb * i.color.rgb * shade * lerp(0.7, 1.0, day);
                return fixed4(c, _Color.a * i.color.a * _Fade);
            }
            ENDCG
        }
    }
    Fallback Off
}
