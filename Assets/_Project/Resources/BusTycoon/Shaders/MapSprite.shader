// Haritadaki düz işaretler (Game/MapSprites.cs): şehir rozetleri (daire) ve otobüsler (kapsül). Hepsi tek örgüde,
// tek çizim çağrısında. Şekil ve kenar çizgisi parça gölgelendiricisinde mesafe alanıyla (SDF) çizilir: her boyda
// keskin ve yumuşak kenarlı.
//  POSITION  şeklin merkezi (daire) ya da köşesi (kapsül, CPU'da yere yatık yerleştirilir)
//  TEXCOORD0 yerel koordinat (-1..1)
//  TEXCOORD1 x: yarıçap (daire, kameraya dönük büyütme; kapsülde 0), y: şekil (0 daire, 1 kapsül),
//            z: kapsül en/boy oranı, w: kenar çizgisinin başladığı oran (0..1)
//  COLOR     dolgu rengi;  TEXCOORD2 kenar çizgisi rengi
Shader "BusTycoon/MapSprite"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Lighting Off
        Cull Off
        ZWrite Off
        ZTest Always // düz harita: örtecek bir şey yok; daire yere gömülüp kesilmesin
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 local : TEXCOORD0;
                float4 shape : TEXCOORD1;
                float4 outline : TEXCOORD2;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 local : TEXCOORD0;
                float4 shape : TEXCOORD1;
                fixed4 outline : TEXCOORD2;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                if (v.shape.y < 0.5)
                {
                    // Daire: kameraya dönük (her açıdan tam yuvarlak)
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up = UNITY_MATRIX_V[1].xyz;
                    world += (right * v.local.x + up * v.local.y) * v.shape.x;
                }
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1.0));
                o.local = v.local;
                o.shape = v.shape;
                o.outline = v.outline;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d;
                if (i.shape.y < 0.5)
                {
                    d = length(i.local);
                }
                else
                {
                    // Kapsül: uzun eksende düz gövde, uçlarda yarım daire
                    float aspect = max(i.shape.z, 1.0);
                    float px = max(abs(i.local.x) * aspect - (aspect - 1.0), 0.0);
                    d = length(float2(px, i.local.y));
                }
                float aa = max(fwidth(d), 1e-4) * 1.2;
                float shape = 1.0 - smoothstep(1.0 - aa, 1.0, d);
                float ring = smoothstep(i.shape.w - aa, i.shape.w, d);
                fixed4 c = lerp(i.color, i.outline, ring);
                c.a *= shape;
                return c * _Tint;
            }
            ENDCG
        }
    }
}
