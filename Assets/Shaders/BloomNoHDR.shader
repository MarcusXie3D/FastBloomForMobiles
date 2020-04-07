// author : Marcus Xie

Shader "Custom/BloomNoHDR"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Threshold("Threshold Luminance", Float) = 0.5
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
                half4 sampleCol = tex2D(_MainTex, i.uv);
                fixed3 color = fixed3(1.0, 1.0, 1.0) - exp( - sampleCol.rgb * 3.0);
                fixed4 col = fixed4(color.r, color.g, color.b, 1.0);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);

                //MRT
                mrtOutput mrtO;
                mrtO.dest0 = col;
                mrtO.dest1 = _ObjectBloomStrength * col.g * step(_Threshold, col.g);
                return mrtO;
            }
            ENDCG
        }
    }
}
