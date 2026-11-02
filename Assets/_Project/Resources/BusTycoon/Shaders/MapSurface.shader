// Harita yüzeyi (WorldRenderer): düz, ışıksız harita dokusu (sade harita dili; Tools/build_map.py --style flat).
// Tek canlı ayrıntı: kıyıda ağır ağır dışa doğru yayılan ince açık halkalar (Mini Motorways'teki gibi). Deniz maskesi
// _WaterTex: R su, G kıyıya yakınlık, B açık deniz (Tools/build_map.py). Gölge, sis, bulut ve parıltı yok: hem sade
// hem ekran kartına hafif.
// Genel değerler (GraphicsQuality): _BT_Waves (0 kapalı .. 1). _Effects 0 olan malzemede (çevre haritası) kapalı.
// BT_TILE: yakın plan döşemesi (Game/MapTiles.cs). Döşeme dokusu, alttaki genel haritaya (_OverviewTex) _TileBlend
// oranında karışır (belirerek gelir) ve harita kenarına yaklaşınca söner; derinlikte biraz öne çekilir (_ZOffset).
// Deniz maskesi ve kenar sönmesi dünya konumundan hesaplanan harita UV'siyle okunur (_MapRect: x, z, genişlik, derinlik).
Shader "BusTycoon/MapSurface"
{
    Properties
    {
        _MainTex ("Map", 2D) = "white" {}
        _WaterTex ("Water Mask (R water, G shore, B open sea)", 2D) = "black" {}
        _Effects ("Shore Rings", Range(0, 1)) = 1
        _MapRect ("Map Rect (x, z, width, depth)", Vector) = (0, 0, 1, 1)
        _OverviewTex ("Overview Map (tiles)", 2D) = "white" {}
        _TileBlend ("Tile Blend", Range(0, 1)) = 1
        _ZOffset ("Depth Offset", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Offset [_ZOffset], [_ZOffset]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ BT_TILE
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _WaterTex;
            half _Effects;
            float4 _MapRect;
            sampler2D _OverviewTex;
            half _TileBlend;
            half _BT_Waves;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 mapUV = (i.world - _MapRect.xy) / max(_MapRect.zw, 1e-4);   // harita UV'si (döşemede de)
                float2 e = min(mapUV, 1.0 - mapUV);
            #ifdef BT_TILE
                // Genel haritanın kenar şeridi çevre haritasına karışır; döşeme orada söner (dikiş olmaz)
                half tileWeight = _TileBlend * saturate(min(e.x, e.y) * 25.0 - 0.02);
                half3 col = lerp(tex2D(_OverviewTex, mapUV).rgb, tex2D(_MainTex, i.uv).rgb, tileWeight);
            #else
                half3 col = tex2D(_MainTex, i.uv).rgb;
            #endif

                // Kıyı halkaları: kıyıdan açığa doğru ağır ağır ilerleyen, giderek sönen ince açık çizgiler
                half strength = _Effects * _BT_Waves;
                UNITY_BRANCH
                if (strength > 0.0)
                {
                    half edge = saturate(min(e.x, e.y) * 30.0);   // harita kenarında söner
                    half3 mask = tex2D(_WaterTex, mapUV).rgb;
                    half shore = mask.g * mask.r * edge;
                    half phase = frac(shore * 3.0 + _Time.y * 0.05);
                    half ring = saturate(1.0 - abs(phase - 0.5) * 9.0) * shore * shore;
                    col = lerp(col, half3(1.0, 1.0, 1.0), ring * 0.35 * strength);
                }
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}
