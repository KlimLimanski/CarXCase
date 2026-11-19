// 3D key prompts floating in the scene (Heavy Rain / Detroit).
// Pass "clear": pushes depth to the far plane inside the glyph's footprint so the scene never hides it.
// Pass "lit": draws the solid key with its own depth test, so its frame, plate and letter occlude each other.
Shader "Hidden/CarX/InteractionGlyph"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        [Toggle] _ClearDepth ("Clear depth", Float) = 0
        _ZTestMode ("ZTest", Float) = 4
        _ColorMaskMode ("Color mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent+400" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Back
            ZWrite On
            ZTest [_ZTestMode]
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask [_ColorMaskMode]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _ClearDepth;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 normal : TEXCOORD0; float3 world : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i, out float depth : SV_Depth) : SV_Target
            {
                if (_ClearDepth > .5)
                {
                    #if defined(UNITY_REVERSED_Z)
                        depth = 0;
                    #else
                        depth = 1;
                    #endif
                    return 0;
                }
                depth = i.pos.z;
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
