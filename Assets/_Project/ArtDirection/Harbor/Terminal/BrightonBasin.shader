Shader "Project/Harbor/Basin Water"
{
    Properties
    {
        _DeepColor ("Channel water", Color) = (0.13,0.21,0.22,1)
        [Normal] _WaveNormal ("Wind ripples", 2D) = "bump" {}
        _SkyPanorama ("Overcast sky", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_WaveNormal); SAMPLER(sampler_WaveNormal);
            TEXTURE2D(_SkyPanorama); SAMPLER(sampler_SkyPanorama);
            TEXTURE2D(_PuddleReflectionTex); SAMPLER(sampler_PuddleReflectionTex);
            float4x4 _PuddleReflectionVP;
            float _PuddleReflectionOn;
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
            CBUFFER_END

            struct Varyings
            {
                float4 clip : SV_POSITION;
                float3 world : TEXCOORD0;
                half fog : TEXCOORD1;
            };

            Varyings Vert(float4 vertex : POSITION)
            {
                Varyings o;
                o.world = TransformObjectToWorld(vertex.xyz);
                o.clip = TransformWorldToHClip(o.world);
                o.fog = ComputeFogFactor(o.clip.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.world.xz;
                float t = _Time.y;
                half2 broad = UnpackNormal(SAMPLE_TEXTURE2D(_WaveNormal, sampler_WaveNormal,
                    p * 0.059 + t * float2(0.0034,0.0021))).xy;
                float2 crossUV = float2(p.x*0.8-p.y*0.6, p.x*0.6+p.y*0.8);
                half2 fine = UnpackNormal(SAMPLE_TEXTURE2D(_WaveNormal, sampler_WaveNormal,
                    crossUV * 0.147 + t * float2(-0.004,0.003))).xy;
                half2 slope = broad * 0.73 + float2(fine.x*0.8+fine.y*0.6, -fine.x*0.6+fine.y*0.8) * 0.32;
                float3 normal = normalize(float3(slope.x,1,slope.y));
                float3 view = GetWorldSpaceNormalizeViewDir(i.world);
                float3 ray = reflect(-view,normal);
                float2 pano = float2(0.5 - atan2(ray.z,ray.x)/(2*PI), 1.0-acos(saturate(ray.y))/PI);
                half3 reflection = SAMPLE_TEXTURE2D(_SkyPanorama,sampler_SkyPanorama,pano).rgb;
                // The road reflection camera mirrors at the quay datum (y=0).
                // Project the water through that same camera only for its matching main view.
                if (_PuddleReflectionOn > 0.5)
                {
                    float3 eye = GetCameraPositionWS();
                    eye.y = -eye.y;
                    float4 projectedEye = mul(_PuddleReflectionVP,float4(eye,1));
                    float cameraMatch = 1.0-smoothstep(0.12,0.7,length(projectedEye.xyw));
                    float4 clip = mul(_PuddleReflectionVP,float4(p.x,-i.world.y,p.y,1));
                    float2 uv = clip.xy / max(clip.w,0.001) * 0.5+0.5;
                    uv += slope * float2(0.014,0.045);
                    float2 border = min(uv,1.0-uv);
                    half blend = smoothstep(0,0.05,min(border.x,border.y)) * step(0.01,clip.w) * cameraMatch;
                    half3 quay = SAMPLE_TEXTURE2D_LOD(_PuddleReflectionTex,sampler_PuddleReflectionTex,saturate(uv),1.7).rgb;
                    reflection = lerp(reflection,min(quay,3.0),blend*0.87);
                }
                half fresnel = 0.08+0.79*pow(1.0-saturate(dot(view,normal)),3.0);
                Light key = GetMainLight();
                half spec = pow(saturate(dot(normal,normalize(view+key.direction))),76.0);
                half3 water = lerp(_DeepColor.rgb,reflection,fresnel) + key.color*spec*0.055;
                return half4(MixFog(water,i.fog),1);
            }
            ENDHLSL
        }
    }
}
