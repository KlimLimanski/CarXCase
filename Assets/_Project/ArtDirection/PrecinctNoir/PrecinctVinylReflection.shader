Shader "CarXCase/Interiors/Precinct Vinyl Reflection"
{
    Properties
    {
        [Normal] _DetailNormal ("Vinyl relief", 2D) = "bump" {}
        _VinylWear ("Wear in alpha", 2D) = "white" {}
        _ReflectionStrength ("Polish", Range(0, 1.5)) = 1.22
        _ReflectionBlur ("Reflection blur", Range(0, 6)) = 1.16
        _ReflectionDistortion ("Relief parallax", Range(0, .08)) = .023
        [HideInInspector] _FloorReflectionTex ("Reflection", 2D) = "black" {}
        [HideInInspector] _FloorReflectionAvailable ("Reflection available", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-12" }
        Pass
        {
            Name "Polished Vinyl"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_DetailNormal); SAMPLER(sampler_DetailNormal);
            TEXTURE2D(_VinylWear); SAMPLER(sampler_VinylWear);
            TEXTURE2D(_FloorReflectionTex); SAMPLER(sampler_FloorReflectionTex);

            CBUFFER_START(UnityPerMaterial)
                float _ReflectionStrength;
                float _ReflectionBlur;
                float _ReflectionDistortion;
                float _FloorReflectionAvailable;
                float4x4 _FloorReflectionVP;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_FloorReflectionAvailable < 0.5) return 0;

                // Each repeat contains two half-metre vinyl tiles on either axis.
                float2 textureUV = input.positionWS.xz;
                half3 detail = UnpackNormal(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, textureUV));
                half wear = SAMPLE_TEXTURE2D(_VinylWear, sampler_VinylWear, textureUV).a;
                half cosine = saturate(GetWorldSpaceNormalizeViewDir(input.positionWS).y);
                half grazing = 1.0h - cosine;
                half fresnel = 0.046h + 0.954h * grazing * grazing * grazing;
                half mask = lerp(0.56h, 1.0h, wear);

                float3 projectedPoint = input.positionWS;
                projectedPoint.xz += detail.xy * _ReflectionDistortion * (1 + grazing);
                float4 reflected = mul(_FloorReflectionVP, float4(projectedPoint, 1));
                float2 uv = reflected.xy / max(reflected.w, 0.001) * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif
                float2 edge = min(uv, 1.0 - uv);
                half coverage = smoothstep(0, .025, min(edge.x, edge.y)) * step(.001, reflected.w);
                half roughness = _ReflectionBlur + (1.0h - wear) * 2.0h;
                half3 color = SAMPLE_TEXTURE2D_LOD(_FloorReflectionTex, sampler_FloorReflectionTex, saturate(uv), roughness).rgb;

                half alpha = min(.67h, fresnel * mask * _ReflectionStrength) * coverage;
                return half4(min(color, 8.0h) * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
