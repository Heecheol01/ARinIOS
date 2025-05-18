Shader "Hidden/AR/EncodeHeight"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            ZWrite Off
            Cull Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _BallPos;
            float4 _Up;

            float4 _Center;
            float4 _Forward;
            float4 _Side;
            float _Halfw;
            float _Len;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 rel = i.worldPos - _Center.xyz;
                float x = dot(rel, _Side.xyz);
                float z = dot(rel, _Forward.xyz);
                if (abs(x) > _Halfw || z < -_Len * 0.5 || z > _Len * 0.5)
                    discard;
                
                float3 diff = i.worldPos - _BallPos.xyz;
                float3 horiz = diff - dot(diff, _Up.xyz) * _Up.xyz;
                float distH = length(horiz) + 1e-4;
                
                float dh = dot(i.worldPos, _Up.xyz) - _BallPos.y;
                if (distH < 0.05)
                    dh = 0;

                float rawSlope = dh / distH;
                if (abs(rawSlope) < 0.02)
                    rawSlope = 0;
                
                float t = saturate(rawSlope * 0.5 + 0.5);
                return fixed4(t, 0, 0, 1);
            }
            ENDCG
        }
    }
Fallback Off
}
