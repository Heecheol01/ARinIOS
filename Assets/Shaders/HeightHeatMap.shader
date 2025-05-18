Shader "Unlit/HeightHeatMap"
{
    Properties
    {
        _HeightTex ("Height Texture", 2D) = "white" {}
        _RampTex ("Ramp Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent"}
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            CGPROGRAM
            #pragma target metal
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _HeightTex;
            sampler2D _RampTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 samp = tex2D(_HeightTex, i.uv);
                if (samp.a < 0.01) discard;
                
                float h = samp.r;
                fixed4 col = tex2D(_RampTex, float2(h, 0));
                col.a *= 0.4;
                return col;
            }
            ENDCG
        }
    }
}
