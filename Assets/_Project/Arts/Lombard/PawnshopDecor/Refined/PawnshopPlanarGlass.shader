Shader "CarXCase/Pawnshop Planar Glass"
{
    Properties
    {
        [NoScaleOffset] _MirrorTex("Planar reflection", 2D) = "black" {}
        _Tint("Glass tint", Color) = (.76, .86, .91, 1)
        _Strength("Reflection strength", Range(0, 1)) = .49
        _Haze("Glass density", Range(0, 1)) = .045
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+35" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Planar Glass"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MirrorTex);
            SAMPLER(sampler_MirrorTex);
            float4x4 _MirrorVP;
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Strength;
                half _Haze;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 reflected = mul(_MirrorVP, float4(input.positionWS, 1.0));
                float2 uv = reflected.xy / max(reflected.w, 0.0001) * .5 + .5;
                float2 border = min(uv, 1.0 - uv);
                half edge = saturate(min(border.x, border.y) * 60.0) * step(.001, reflected.w);
                half facing = saturate(abs(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS))));
                half fresnel = .48 + .52 * Pow4(1.0 - facing);
                half weight = _Strength * fresnel * edge;
                half3 reflection = SAMPLE_TEXTURE2D(_MirrorTex, sampler_MirrorTex, saturate(uv)).rgb;
                half3 haze = _Tint.rgb * .19;
                half opacity = max(.001, _Haze + weight);
                return half4((haze * _Haze + reflection * _Tint.rgb * weight) / opacity, opacity);
            }
            ENDHLSL
        }
    }
}
