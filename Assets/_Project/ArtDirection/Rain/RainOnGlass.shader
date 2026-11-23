// Rain drops on car glass: static beads that build up over time, larger drops that run down
// leaving a trail, and refraction of what is behind the glass (_CameraOpaqueTexture).
// UVs of the glass are in metres (u horizontal, v up). On the windshield, two tandem wipers
// (timing shared with RainAtmosphere via _RainTime/_RainWiperParams) clear the drops they pass.
Shader "CarXCase/RainOnGlass"
{
    Properties
    {
        _StaticAmount ("Static beads", Range(0, 1)) = 0.8
        _StaticSize ("Bead cell size (m)", Float) = 0.0085
        _RunningAmount ("Running drops", Range(0, 1)) = 0.65
        _RunSpeed ("Running speed", Float) = 1
        _Refraction ("Refraction", Float) = 0.045
        _EdgeDarken ("Edge darkening", Range(0, 1)) = 0.55
        _Brightness ("Brightness", Range(0, 2)) = 1.05
        _Accumulate ("Seconds to cover after a wipe", Float) = 2.4
        _VScale ("Metres along glass per UV.v", Float) = 1
        [Toggle] _Wipers ("Wipers", Float) = 0
        _WiperPivots ("Wiper pivots (u1, v1, u2, v2)", Vector) = (0, 0, 0, 0)
        _WiperRange ("Wiper radius min/max (m)", Vector) = (0.02, 0.5, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "RainOnGlass"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            float _RainTime;
            float4 _RainWiperParams; // up time, down time, period, sweep (radians)

            CBUFFER_START(UnityPerMaterial)
                half _StaticAmount, _RunningAmount, _EdgeDarken, _Brightness;
                float _StaticSize, _RunSpeed, _Refraction, _Accumulate, _VScale, _Wipers;
                float4 _WiperPivots, _WiperRange;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; };

            V vert (A i)
            {
                V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv; o.screenPos = ComputeScreenPos(o.positionCS); return o;
            }

            float H21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float2 H22(float2 p) { float n = H21(p); return float2(n, H21(p + n)); }

            // seconds since the wiper last passed over p (large if never)
            float WipeAge(float2 p, float2 pivot)
            {
                float2 d = p - pivot; d.y *= _VScale;
                float r = length(d);
                if (r < _WiperRange.x || r > _WiperRange.y) return 1e4;
                float sweep = max(_RainWiperParams.w, 1e-3);
                float f = atan2(d.y, -d.x) / sweep;      // 0 = parked (pointing to -u), 1 = end of sweep
                if (f < 0 || f > 1) return 1e4;
                float up = _RainWiperParams.x, dn = _RainWiperParams.y, T = max(_RainWiperParams.z, up + dn);
                float t = _RainTime, c = floor(t / T) * T, tau = t - c;
                float x = acos(clamp(1 - 2 * f, -1, 1)) / PI;
                float tu = up * x, td = up + dn * (1 - x);
                float last = tau >= td ? c + td : (tau >= tu ? c + tu : c - T + td);
                return t - last;
            }

            // bead layer: xy = refraction normal * mask, z = mask
            float3 Beads(float2 p, float cell, float age, float seed)
            {
                float2 g = p / cell + seed; float2 id = floor(g); float2 f = frac(g) - 0.5;
                float2 rnd = H22(id);
                float2 pos = (rnd - 0.5) * 0.55;
                float rad = lerp(0.16, 0.42, pow(H21(id + 7.13), 2));
                float appear = H21(id + 3.71) * _Accumulate;
                float alive = step(H21(id + 11.3), _StaticAmount) * smoothstep(appear, appear + 0.12, age);
                float2 d = f - pos; d.y *= 1.15;
                float dist = length(d) / rad;
                float m = smoothstep(1.0, 0.75, dist) * alive;
                return float3(d / rad * m, m);
            }

            // running drop layer: xy normal * mask, z mask, w trail mask (wipes beads under it)
            float4 Runners(float2 p, float colW, float cellH, float t, float seed)
            {
                float2 g = float2(p.x / colW, p.y / cellH) + seed;
                float col = floor(g.x);
                float rc = H21(float2(col, seed));
                g.y += rc * 7.0;
                float2 id = floor(g); float2 f = float2(frac(g.x) - 0.5, frac(g.y));
                float rr = H21(id + 1.7);
                if (rr > _RunningAmount) return 0;
                float speed = lerp(0.18, 0.5, H21(id + 2.3)) * _RunSpeed;
                float tt = t * speed + rr * 5.0;
                tt += 0.18 * sin(tt * 9.0) ; // stop-and-go
                float dy = 1 - frac(tt);
                float wob = sin(f.y * 17.0 + rr * 6.0) * 0.08 + (H21(id + 4.4) - 0.5) * 0.4;
                float2 d = float2((f.x - wob) * colW, (f.y - dy) * cellH);
                float2 radius = float2(0.0024, 0.0032) * lerp(0.8, 1.4, H21(id + 9.1));
                float dist = length(d / radius);
                float m = smoothstep(1.0, 0.72, dist);
                float2 n = d / radius * m;
                // trail of tiny beads above the drop
                float above = (f.y - dy) * cellH;
                float trailBand = smoothstep(0.004, 0.0015, abs(d.x)) * step(0, above) * smoothstep(0.16, 0.0, above);
                float ty = frac(above / 0.011); float tid = floor(above / 0.011);
                float2 td = float2(d.x, (ty - 0.5) * 0.011) / (0.0011 * lerp(0.6, 1.2, H21(float2(tid, col))));
                float tm = smoothstep(1.0, 0.7, length(td)) * trailBand * step(0.35, H21(float2(tid, col + 3)));
                n += td * tm * 0.6;
                return float4(n, saturate(m + tm), trailBand);
            }

            half4 frag (V i) : SV_Target
            {
                float2 p = i.uv;
                float age = 1e4;
                if (_Wipers > 0.5 && _RainWiperParams.w > 0) age = min(WipeAge(p, _WiperPivots.xy), WipeAge(p, _WiperPivots.zw));

                float t = _RainTime;
                float4 run = Runners(p, 0.032, 0.30, t, 0) + Runners(p * 1.0, 0.055, 0.45, t * 0.8, 17.0) * 0.8;
                run *= smoothstep(1.0, 1.8, age);
                float clearUnder = saturate(run.w);

                float3 b = Beads(p, _StaticSize, age, 0) + Beads(p, _StaticSize * 1.9, age * 0.8, 31.7) * 0.9
                         + Beads(p, _StaticSize * 0.55, age * 1.3, 5.3) * 0.7;
                b *= 1 - clearUnder * 0.85;

                float mask = saturate(b.z + run.z);
                float2 n = b.xy + run.xy;
                if (mask < 0.003) discard;

                float2 suv = i.screenPos.xy / i.screenPos.w;
                // a drop is a tiny lens: it shows a flipped, shifted image of what is behind it
                half3 bg = clamp(SampleSceneColor(saturate(suv - n * _Refraction)), 0.0, 16.0);
                half edge = pow(saturate(length(n)), 3);
                half3 col = bg * lerp(1.0, 1.0 - _EdgeDarken, edge) * _Brightness;
                // small highlight on the top of each bead
                col += saturate(-n.y) * 0.04 * mask;
                return half4(clamp(col, 0.0, 16.0), saturate(mask));
            }
            ENDHLSL
        }
    }
}
