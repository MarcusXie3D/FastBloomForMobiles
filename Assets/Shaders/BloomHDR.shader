// author : Marcus Xie

Shader "Custom/BloomHDR"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _UpmostLuminance("Upmost Luminance", Float) = 15.0
        _Exposure("Exposure", Float) = 0.2
        _Threshold("Threshold Luminance", Float) = 1.0
        _ObjectBloomStrength("Object Bloom Strength", Range (0.0, 1.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            struct mrtOutput
            {
                fixed4 dest0 : SV_Target0;
                fixed dest1 : SV_Target1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _UpmostLuminance;
            half _Exposure;
            half _Threshold;
            fixed _ObjectBloomStrength;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            mrtOutput frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);

                // imitate color values greater than 1
                half4 hdrCol = half4(col.r * _UpmostLuminance, col.g * _UpmostLuminance, col.b * _UpmostLuminance, col.a);

                //MRT
                mrtOutput mrtO;
                //  tonemapping
                fixed3 color = fixed3(1.0, 1.0, 1.0) - exp( - hdrCol.rgb * _Exposure);
                mrtO.dest0 = fixed4(color.r, color.g, color.b, hdrCol.a);
                //  compress hdr color value into [0, 1]
                // take only the value of green channel as luminance
                mrtO.dest1 = _ObjectBloomStrength * hdrCol.g * step(_Threshold, hdrCol.g) / _UpmostLuminance;
                return mrtO;
            }
            ENDCG
        }
    }
}
