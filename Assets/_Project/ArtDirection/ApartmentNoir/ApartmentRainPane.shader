Shader "CarXCase/Interiors/Rain Pane"
{
    Properties
    {
        _LightOutside ("Street haze", Color) = (0.32, 0.40, 0.48, 1)
        _ShadeOutside ("Haze at the sill", Color) = (0.17, 0.22, 0.27, 1)
        _RainIntensity ("Rain intensity", Range(0, 1)) = 0.36
        _RunSpeed ("Run speed", Range(0, 1)) = 0.22
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "RainPane"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _LightOutside;
                half4 _ShadeOutside;
                half _RainIntensity;
                half _RunSpeed;
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

            float Hash(float2 p)
            {
                p = frac(p * float2(0.1031, 0.11369));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Both sash windows sit in walls normal to the world X axis.
                float2 glass = float2(input.positionWS.z, input.positionWS.y);
                float2 beads = glass * float2(69.0, 62.0);
                float2 cell = floor(beads);
                float2 off = float2(Hash(cell + 7.3), Hash(cell + 4.9)) * 0.52 + 0.24;
                float2 d = (frac(beads) - off) * float2(1.25, 1.0);
                float bead = (1.0 - smoothstep(0.045, 0.23, length(d))) * step(0.64, Hash(cell));
                float highlight = (1.0 - smoothstep(0.025, 0.14, length(d - float2(0.055, -0.055)))) * bead;
                float x = glass.x * 21.0;
                float seed = Hash(float2(floor(x), 12.7));
                float runoff = frac(glass.y * 1.6 + _Time.y * _RunSpeed * (0.6 + seed) + seed * 13.0);
                float column = abs(frac(x) - lerp(0.23, 0.76, seed) + 0.014 * sin(glass.y * 45.0));
                float trail = (1.0 - smoothstep(0.016, 0.054, column))
                    * smoothstep(0.06, 0.1, runoff) * (1.0 - smoothstep(0.1, 0.58, runoff)) * step(0.69, seed);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half edge = pow(1.0 - abs(view.x), 3.0);
                half3 haze = lerp(_ShadeOutside.rgb, _LightOutside.rgb, smoothstep(0.94, 2.5, glass.y));
                half street = 0.94 + 0.08 * sin(glass.x * 13.0 + glass.y * 5.0);
                return half4(haze * (street + edge * 0.32) * (1.0 - bead * 0.48 * _RainIntensity)
                    + half3(0.31, 0.42, 0.49) * (highlight * 0.48 + trail * 0.62) * _RainIntensity, 1);
            }
            ENDHLSL
        }
    }
}
