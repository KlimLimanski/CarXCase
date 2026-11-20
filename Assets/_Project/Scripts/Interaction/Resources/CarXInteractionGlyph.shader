// 3D key prompts projected from their interaction points. Separate render queues
// layer the plate, frame and lettering without modifying the scene depth.
Shader "Hidden/CarX/InteractionGlyph"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+400" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Back
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz, back = UNITY_MATRIX_V[2].xyz;
                // Key light from the upper left of the lens, plus a soft fill and a rim on the bevels.
                float3 key = normalize(-.45 * right + .65 * up + .9 * back);
                float diffuse = saturate(dot(n, key));
                float facing = saturate(dot(n, back));
                float rim = pow(1 - facing, 3) * .35;
                float light = .38 + .7 * diffuse + rim;
                return fixed4(_Color.rgb * light, _Color.a);
            }
            ENDCG
        }
    }
}
