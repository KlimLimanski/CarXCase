Shader "CarXCase/Apartment CRT Noise"
{
    Properties
    {
        _CrtOrigin ("Screen lower corner", Vector) = (0,0,0,0)
        _CrtDimensions ("Screen dimensions", Vector) = (0.5,0.4,0,0)
        _Peak ("Phosphor brightness", Range(0,4)) = 2.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _CrtOrigin;
                float4 _CrtDimensions;
                float _Peak;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 screenUV : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.screenUV = (world.zy - _CrtOrigin.zy) / _CrtDimensions.xy;
                return o;
            }

            float Noise(float2 value)
            {
                float3 f = frac(float3(value.xyx) * .1031);
                f += dot(f, f.yzx + 33.33);
                return frac((f.x + f.y) * f.z);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = saturate(i.screenUV);
                float frame = floor(_Time.y * 24);
                float row = floor(uv.y * 118);
                float rowShift = Noise(float2(row,frame)) * .013;
                float2 pixel = floor(float2((uv.x + rowShift) * 156, row));
                float signal = Noise(pixel + float2(frame * 7.13, frame * 2.77));
                float pixelFootprint = max(fwidth(uv.x) * 156, fwidth(uv.y) * 118);
                float grain = lerp(signal, .48, smoothstep(.75, 2.4, pixelFootprint));
                float tracks = 1 - smoothstep(.02,.075,abs(frac(uv.y - _Time.y * .28) - .5));
                float scanAmount = .22 * (1 - smoothstep(.3, 1.2, fwidth(uv.y) * 118));
                float scan = lerp(1 - scanAmount, 1, smoothstep(.18,.58,frac(uv.y * 118)));
                float vignette = 1 - saturate(dot(uv - .5, uv - .5)) * .55;
                float level = (.045 + pow(grain,1.65) * _Peak + tracks * .24) * scan * vignette;
                return half4(level,level,level,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
