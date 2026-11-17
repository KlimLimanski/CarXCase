Shader "CarXCase/CarMirror"
{
    Properties
    {
        _MirrorTex ("Reflection", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.78, 0.8, 0.82, 1)
        _EdgeDarken ("Edge darkening", Range(0, 1)) = 0.35
        _Grime ("Grime", Range(0, 1)) = 0.06
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Mirror"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MirrorTex); SAMPLER(sampler_MirrorTex);
            float4x4 _MirrorVP;
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint; half _EdgeDarken; half _Grime;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            V vert (A i)
            {
                V o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS); o.uv = i.uv; return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            half4 frag (V i) : SV_Target
            {
                float4 c = mul(_MirrorVP, float4(i.positionWS, 1));
                float2 uv = c.xy / c.w * 0.5 + 0.5;
                half3 col = SAMPLE_TEXTURE2D(_MirrorTex, sampler_MirrorTex, uv).rgb * _Tint.rgb;
                float2 e = min(i.uv, 1 - i.uv);
                half edge = smoothstep(0.0, 0.08, min(e.x, e.y));
                col *= lerp(1 - _EdgeDarken, 1, edge);
                col += _Grime * 0.02 * hash(floor(i.uv * 220));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
