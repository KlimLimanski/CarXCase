// Soft additive light cone: lamp/headlight light scattered by rain mist.
// Mesh: unit cone with the apex at the origin, opening along +Z, uv.y = 0 at apex .. 1 at the base.
Shader "CarXCase/FogLightCone"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.64, 0.32, 1)
        _Intensity ("Intensity", Float) = 0.05
        _EdgeSoftness ("Edge softness", Range(0.5, 8)) = 2.5
        _Falloff ("Falloff along the beam", Range(0.2, 6)) = 1.6
        _SoftDepth ("Soft intersection (m)", Float) = 0.8
        _NearFade ("Fade near camera (m)", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "FogLightCone"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color; half _Intensity, _EdgeSoftness, _Falloff; float _SoftDepth, _NearFade;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; float4 screenPos : TEXCOORD3; float fog : TEXCOORD4; };

            V vert (A i)
            {
                V o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv; o.screenPos = ComputeScreenPos(o.positionCS); o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half thick = pow(saturate(abs(dot(normalize(i.normalWS), v))), _EdgeSoftness);
                half along = pow(saturate(1 - i.uv.y), _Falloff) * smoothstep(0.0, 0.06, i.uv.y);
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float eye = i.screenPos.w;
                half soft = saturate((sceneEye - eye) / max(_SoftDepth, 1e-3));
                half nearF = saturate((eye - 0.2) / max(_NearFade, 1e-3));
                half vis = ComputeFogIntensity(i.fog);
                half3 col = _Color.rgb * (_Intensity * thick * along * soft * nearF * lerp(0.35, 1.0, vis));
                return half4(clamp(col, 0.0, 4.0), 1);
            }
            ENDHLSL
        }
    }
}
