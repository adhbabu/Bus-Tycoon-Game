// Harita yüzeyi (WorldRenderer): düz, ışıksız harita dokusu (sade harita dili; Tools/build_map.py --style flat).
// Tek canlı ayrıntı: kıyıda ağır ağır dışa doğru yayılan ince açık halkalar (Mini Motorways'teki gibi). Deniz maskesi
// _WaterTex: R su, G kıyıya yakınlık, B açık deniz (Tools/build_map.py). Gölge, sis, bulut ve parıltı yok: hem sade
// hem ekran kartına hafif.
// Genel değerler (GraphicsQuality): _BT_Waves (0 kapalı .. 1). _Effects 0 olan malzemede (çevre haritası) kapalı.
// _BT_DarkMap (0..1, GameUI): koyu harita. Açık renkli dokudan hesaplanır: deniz (mavi tonlu açık renk) laciverte döner;
// kara koyu arduvaz olur, üstündeki koyu ayrıntılar (sınır, nehir, kabartma) açılır; doygun renkler (hat değil, harita
// renkleri) korunur. Aynı hesap genel haritada, döşemelerde ve çevre haritasında: dikiş olmaz.
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
            half _BT_DarkMap;

            half3 DarkTheme(half3 c)
            {
                half mx = max(c.r, max(c.g, c.b));
                half mn = min(c.r, min(c.g, c.b));
                half L = (mx + mn) * 0.5;
                half S = (mx - mn) / (1.0 - abs(2.0 * L - 1.0) + 1e-4);
                half sea = smoothstep(0.06, 0.12, c.b - c.r) * smoothstep(-0.04, 0.0, c.b - c.g) * smoothstep(0.5, 0.58, L);
                half colored = saturate((S - 0.35) * 2.2) * step(L, 0.75);
                half outL = lerp(0.115 + 0.62 * (1.0 - L), L, colored);
                half3 land = ((c - L) * (0.35 + 0.65 * colored) + outL) * half3(0.93, 1.0, 1.04);
                half3 navy = half3(0.055, 0.135, 0.19) + (L - 0.78) * 0.25;
                return lerp(land, navy, sea);
            }

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

                UNITY_BRANCH
                if (_BT_DarkMap > 0.0) col = lerp(col, DarkTheme(col), _BT_DarkMap);

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
                    col = lerp(col, half3(1.0, 1.0, 1.0), ring * (0.35 - 0.2 * _BT_DarkMap) * strength);
                }
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}
