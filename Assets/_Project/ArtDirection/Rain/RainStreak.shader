// Falling rain streaks for stretched-billboard particles.
// Drops are almost invisible on their own and light up when a lamp or headlight is behind them
// (strong forward scattering), fade into the scene fog, soft-intersect geometry and are cut out
// inside the hero car (box set globally by RainAtmosphere).
Shader "CarXCase/RainStreak"
{
    Properties
    {
        _Color ("Tint", Color) = (0.52, 0.57, 0.62, 1)
        _Opacity ("Opacity", Range(0, 2)) = 0.45
        _Ambient ("Ambient light", Range(0, 2)) = 0.6
        _BaseScatter ("Side scattering", Range(0, 2)) = 0.12
        _ForwardScatter ("Back-light scattering", Range(0, 8)) = 1.6
        _ForwardPower ("Back-light sharpness", Range(1, 32)) = 6
        _SoftDistance ("Soft intersection (m)", Float) = 0.12
        _NearFade ("Fade near camera (m)", Float) = 0.4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "RainStreak"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4x4 _RainCabinWorldToLocal;
            float4 _RainCabinMin, _RainCabinMax;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity, _Ambient, _BaseScatter, _ForwardScatter, _ForwardPower;
                float _SoftDistance, _NearFade;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V
            {
                float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1; float4 screenPos : TEXCOORD2; float fog : TEXCOORD3;
            };

            V vert (A i)
            {
                V o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = i.color; o.uv = i.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half Phase(float3 lightDir, float3 viewDir)
            {
                // light travels along -lightDir; strongest when it heads straight into the camera
                return _BaseScatter + _ForwardScatter * pow(saturate(dot(lightDir, -viewDir)), _ForwardPower);
            }

            half4 frag (V i) : SV_Target
            {
                if (_RainCabinMax.x > _RainCabinMin.x)
                {
                    float3 lp = mul(_RainCabinWorldToLocal, float4(i.positionWS, 1)).xyz;
                    if (all(lp > _RainCabinMin.xyz) && all(lp < _RainCabinMax.xyz)) discard;
                }

                float2 c = i.uv * 2 - 1;
                half shape = saturate(1 - dot(c, c)); shape *= shape;

                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float eye = i.screenPos.w;
                half soft = saturate((sceneEye - eye) / max(_SoftDistance, 1e-3));
                half nearF = saturate((eye - 0.05) / max(_NearFade, 1e-3));

                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float3 light = _Ambient * SampleSH(half3(0, 1, 0));

                Light mainL = GetMainLight();
                light += min(mainL.color * Phase(mainL.direction, viewDir), 4.0);

                #if defined(_ADDITIONAL_LIGHTS) || defined(_CLUSTER_LIGHT_LOOP)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = suv;
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    // clamp: 1/d^2 explodes when a drop passes right next to a lamp -> Inf -> bloom blow-out
                    light += min(l.color * min(l.distanceAttenuation, 2.0) * Phase(l.direction, viewDir), 12.0);
                LIGHT_LOOP_END
                #endif

                half vis = ComputeFogIntensity(i.fog);
                half a = shape * _Opacity * i.color.a * soft * nearF * vis;
                float3 col = clamp(light * _Color.rgb * i.color.rgb, 0.0, 12.0);
                return half4(clamp(col * a, 0.0, 12.0), saturate(a * 0.15));
            }
            ENDHLSL
        }
    }
}
