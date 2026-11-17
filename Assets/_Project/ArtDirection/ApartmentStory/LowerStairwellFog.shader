Shader "CarXCase/Interiors/Lower Stairwell Fog"
{
    Properties
    {
        _FogColor ("Fog color", Color) = (0.016, 0.019, 0.023, 1)
        _Density ("Density", Float) = 3.2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _Density;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(output.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float DensityRamp(float3 positionOS)
            {
                float spread = smoothstep(-0.49, 0.14, positionOS.z + positionOS.y * 0.12);
                return spread * lerp(1.0, 0.80, saturate(positionOS.y + 0.5));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The cube's back face is the end of the ray. Stop sooner at opaque geometry.
                float3 eye = GetCameraPositionWS();
                float3 eyeOS = TransformWorldToObject(eye);
                float3 ray = input.positionOS - eyeOS;
                float3 inverseRay = lerp(-1.0, 1.0, step(0.0, ray)) / max(abs(ray), 0.00001);
                float3 nearPlanes = min((-0.5 - eyeOS) * inverseRay, (0.5 - eyeOS) * inverseRay);
                float enter = max(0.0, max(nearPlanes.x, max(nearPlanes.y, nearPlanes.z)));

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float backDepth = -TransformWorldToView(input.positionWS).z;
                float leave = min(1.0, sceneDepth / max(backDepth, 0.00001));
                float segment = max(0.0, leave - enter);

                float3 entryPoint = eyeOS + ray * enter;
                float3 endPoint = eyeOS + ray * leave;
                float density = (DensityRamp(entryPoint) + 4.0 * DensityRamp((entryPoint + endPoint) * 0.5)
                    + DensityRamp(endPoint)) / 6.0;
                float opticalDepth = density * _Density * segment * distance(eye, input.positionWS);
                return half4(_FogColor.rgb, 1.0 - exp(-opticalDepth));
            }
            ENDHLSL
        }
    }
}
