// Harita yüzeyi (WorldRenderer): ışıksız harita dokusu + canlı ayrıntılar. Eskiden "Unlit/Texture" idi.
//  · Deniz (deniz maskesi _WaterTex: R su, G kıyıya yakınlık, B açık deniz — Tools/build_map.py):
//    kayan iki gürültüyle dalga parıltısı, güneş yansıması ve kıyıya vuran köpük şeritleri
//  · Arazi: yakınlaşınca beliren ince doku (keskinlik algısı)
//  · Bulut gölgeleri: haritanın üzerinde ağır ağır kayan yumuşak gölgeler (yalnız Yüksek kalite)
//  · Gölgeler: duraklar ve otobüsler haritaya gölge düşürür; ufukta pus (Built-in sis)
// Kalite ve gün ışığı genel değerlerle gelir (GraphicsQuality, WorldRenderer.SetDaylight): _BT_Detail, _BT_Waves,
// _BT_Clouds (0..1), _BT_Daylight (1 gündüz, 0 gece). Değer verilmemişse 0: yalnız doku çizilir.
// _Effects 0 olan malzemede (çevre haritası) deniz ve arazi efektleri kapalıdır; bulut, gölge ve pus sürer.
Shader "BusTycoon/MapSurface"
{
    Properties
    {
        _MainTex ("Map", 2D) = "white" {}
        _WaterTex ("Water Mask (R water, G shore, B open sea)", 2D) = "black" {}
        _NoiseTex ("Noise (R/G fine, B clouds)", 2D) = "gray" {}
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.42
        _Effects ("Sea And Land Effects", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _WaterTex;
            sampler2D _NoiseTex;
            half _ShadowStrength;
            half _Effects;
            half _BT_Detail, _BT_Waves, _BT_Clouds, _BT_Daylight;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                SHADOW_COORDS(3)
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half3 col = tex2D(_MainTex, i.uv).rgb;
                float3 toCamera = _WorldSpaceCameraPos - i.world;
                float dist = length(toCamera);
                float2 wp = i.world.xz;
                float t = _Time.y;
                half day = saturate(_BT_Daylight);

                // Yakın plan (kamera ~16 birimden yakın: tam, ~40 birimde yok): ince ayrıntılar kırpışmasın
                half near = saturate((40.0 - dist) / 24.0);

                // Dallanma içinde doku okumak için türevler dışarıda hesaplanır (tex2Dgrad): zayıf cihazda
                // kapalı efektlerin doku okumaları hiç yapılmaz.
                float2 wdx = ddx(wp), wdy = ddy(wp);
                float2 udx = ddx(i.uv), udy = ddy(i.uv);

                UNITY_BRANCH
                if (_Effects > 0.0 && (_BT_Waves > 0.0 || _BT_Detail > 0.0))
                {
                    half3 mask = tex2Dgrad(_WaterTex, i.uv, udx, udy).rgb;
                    // Harita kenarında efektler söner: çevre haritasına dikişsiz karışır
                    float2 e = min(i.uv, 1.0 - i.uv);
                    half edge = saturate(min(e.x, e.y) * 30.0);
                    half water = mask.r * edge;

                    UNITY_BRANCH
                    if (water > 0.004 && _BT_Waves > 0.0)
                    {
                        half n1 = tex2Dgrad(_NoiseTex, wp * 0.23 + t * float2(0.011, 0.007), wdx * 0.23, wdy * 0.23).r;
                        half n2 = tex2Dgrad(_NoiseTex, wp * 0.41 + t * float2(-0.009, 0.012), wdx * 0.41, wdy * 0.41).g;
                        half amp = _BT_Waves * (0.45 + 0.55 * mask.b);   // açık denizde dalga büyük

                        // Dalga parıltısı (ışık-gölge kıpırtısı)
                        col += water * (n1 + n2 - 1.0) * 0.05 * amp;

                        // Güneş yansıması: dalgalardan sahte yüzey normali
                        float3 normal = normalize(float3((n1 - 0.5) * 1.1, 1.0, (n2 - 0.5) * 1.1));
                        float3 view = toCamera / max(dist, 0.001);
                        float3 halfDir = normalize(normalize(_WorldSpaceLightPos0.xyz) + view);
                        half glint = pow(saturate(dot(normal, halfDir)), 36.0);
                        col += water * glint * 0.5 * amp * day * saturate(_LightColor0.rgb);

                        // Kıyı köpüğü: kıyıya doğru ilerleyen ince şeritler + kıyı dibinde açık ton
                        half shore = mask.g * edge;
                        half phase = frac(shore * 2.2 - t * 0.11 + n1 * 0.4);
                        half band = saturate(1.0 - abs(phase - 0.5) * 6.0) * shore;
                        half foam = (shore * shore * 0.35 + band * 0.45 * (0.35 + 0.65 * near)) * amp;
                        col = lerp(col, half3(0.82, 0.9, 0.94), saturate(foam) * 0.42);
                    }

                    UNITY_BRANCH
                    if (_BT_Detail > 0.0 && near > 0.0)
                    {
                        half land = 1.0 - mask.r;
                        half d = tex2Dgrad(_NoiseTex, wp * 1.35, wdx * 1.35, wdy * 1.35).r * 0.6 +
                                 tex2Dgrad(_NoiseTex, wp * 4.3, wdx * 4.3, wdy * 4.3).g * 0.4;
                        col *= 1.0 + (d - 0.5) * 0.17 * land * near * _BT_Detail;
                    }
                }

                // Bulut gölgeleri: iki ölçekte, farklı yönlere kayan yumuşak lekeler
                UNITY_BRANCH
                if (_BT_Clouds > 0.0)
                {
                    half c1 = tex2Dgrad(_NoiseTex, wp * 0.019 + t * float2(0.0032, 0.0011), wdx * 0.019, wdy * 0.019).b;
                    half c2 = tex2Dgrad(_NoiseTex, wp * 0.033 - t * float2(0.0010, 0.0024), wdx * 0.033, wdy * 0.033).b;
                    half cloud = smoothstep(0.5, 0.78, c1 * 0.65 + c2 * 0.35);
                    col *= 1.0 - cloud * 0.16 * _BT_Clouds * day;
                }

                // Duraklar ve otobüsler haritaya gölge düşürür (gece söner)
                half shadow = SHADOW_ATTENUATION(i);
                col *= lerp(1.0, shadow, _ShadowStrength * (0.35 + 0.65 * day));

                fixed4 c = fixed4(col, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }

        // Kamera derinlik dokusuna girsin diye (ekran uzayı gölgeler bu derinliği kullanır); harita gölge düşürmez.
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }

            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2fShadow
            {
                V2F_SHADOW_CASTER;
            };

            v2fShadow vertShadow (appdata_base v)
            {
                v2fShadow o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 fragShadow (v2fShadow i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}
