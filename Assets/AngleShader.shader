Shader "Custom/AngleMaskedTexture"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Angle("Angle", Range(0,180)) = 180
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _Angle;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 centeredCoords = i.uv * 2.0 - 1.0;
                float angle = atan2(centeredCoords.y, centeredCoords.x) * 57.29578; // Convert radians to degrees

                if (angle < 0.0)
                    angle += 360.0;

                float mask = step(angle, _Angle);

                half4 col = tex2D(_MainTex, i.uv);
                return col * mask;
            }
            ENDCG
        }
    }
}