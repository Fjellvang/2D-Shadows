Shader "Unlit/TestShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Angle("Angle", Range(0,180)) = 180
        _Tint("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Angle;
            float4 _Tint;


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                //UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                float dist = distance(i.uv, float2(0.5, 0.5));
                //// apply fog
                //UNITY_APPLY_FOG(i.fogCoord, col);

                float2 centeredCoords = i.uv * 2.0 - 1.0;
                float angle = atan2(centeredCoords.y, centeredCoords.x) * 57.29578; // Convert radians to degrees

                float mask = step(-_Angle, angle) * step(angle, _Angle);
                //float mask = step(180.0 - _Angle, angle) * step(angle, 180.0 + _Angle);

                return col * (1-dist*2) * (1-mask) * _Tint;
            }
            ENDCG
        }
    }
}
