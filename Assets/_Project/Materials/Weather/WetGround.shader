Shader "Project/Wet Ground"
{
 Properties {
  _BaseMap("Albedo",2D)="white"{}
  _BaseColor("Tint",Color)=(1,1,1,1)
  _TextureScale("Texture repeats per metre",Float)=0.25
  _DetailNormal("Detail normal",2D)="bump"{}
  _DetailScale("Detail repeats per metre",Float)=0.8
  _DetailStrength("Detail strength",Range(0,2))=0.8
  _Wetness("Wetness",Range(0,1))=0.7
  _WetDarken("Darkening when wet",Range(0,1))=0.4
  _PuddleAmount("Puddle amount",Range(0,1))=0.32
  _PuddleScale("Puddle size (lower = bigger)",Float)=0.22
  _PuddleSeed("Puddle seed",Float)=0
  _RainSpeed("Rain ripples speed",Float)=1.1
  _RippleScale("Ripples per metre",Float)=3.2
  _RippleStrength("Ripple strength",Range(0,2))=0.9
  _ReflectionStrength("Puddle reflection",Range(0,1.5))=1
  _WetReflection("Wet asphalt reflection",Range(0,1))=0.55
  _ReflectionBlur("Wet asphalt blur (mip)",Range(0,6))=3
  _StreakLength("Reflection streak length",Range(0,0.05))=0.014
  _ReflectionDistort("Ripple distortion",Range(0,0.3))=0.08
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Name "ForwardLit"
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   TEXTURE2D(_DetailNormal); SAMPLER(sampler_DetailNormal);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST,_DetailNormal_ST,_BaseColor;
   float _TextureScale,_DetailScale,_DetailStrength,_Wetness,_WetDarken,_PuddleAmount,_PuddleScale,_PuddleSeed,_RainSpeed,_RippleScale,_RippleStrength;
   float _ReflectionStrength,_WetReflection,_ReflectionBlur,_StreakLength,_ReflectionDistort;
   CBUFFER_END
   // planar reflection of the street rendered by PuddleReflection.cs (global)
   TEXTURE2D(_PuddleReflectionTex); SAMPLER(sampler_PuddleReflectionTex);
   float4x4 _PuddleReflectionVP; float _PuddleReflectionOn;
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
   struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float fog:TEXCOORD2;};
   V vert(A v){V o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   float hash13(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
   float2 hash2(float2 p){return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);}
   float vnoise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash13(i),hash13(i+float3(1,0,0)),f.x),lerp(hash13(i+float3(0,1,0)),hash13(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash13(i+float3(0,0,1)),hash13(i+float3(1,0,1)),f.x),lerp(hash13(i+float3(0,1,1)),hash13(i+1),f.x),f.y),f.z);}
   // one layer of raindrop rings: returns xz slope
   float2 Ripple(float2 p, float t){
     float2 cell=floor(p); float2 f=frac(p)-0.5;
     float2 rnd=hash2(cell); float2 d=f-(rnd-0.5)*0.55;
     float phase=frac(t+rnd.x*7.13);
     float dist=length(d); float r=phase*0.42; float x=dist-r;
     float wave=sin(x*55.0)*exp(-x*x*700.0)*(1.0-phase)*(1.0-phase);
     return d/max(dist,1e-4)*wave;
   }
   half4 frag(V v):SV_Target {
    float3 n=normalize(v.normalWS);
    float3 w=pow(abs(n),4); w/=max(dot(w,1),0.001);
    float3 p=v.positionWS*_TextureScale;
    half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy)*w.x+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz)*w.y+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy)*w.z;
    float top=step(0.7,n.y);
    float2 xz=v.positionWS.xz;
    // puddles: irregular world-space blobs on top faces
    float pn=vnoise(float3(xz*_PuddleScale,_PuddleSeed))*0.62+vnoise(float3(xz*_PuddleScale*2.9,_PuddleSeed+5.3))*0.28+vnoise(float3(xz*_PuddleScale*9.0,_PuddleSeed+1.7))*0.10;
    float th=0.75-_PuddleAmount*0.55;
    float puddle=smoothstep(th,th+0.035,pn)*top;
    float damp=smoothstep(th-0.12,th,pn)*top;
    // detail normal (top faces), flattened by water
    half3 dn=UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormal,sampler_DetailNormal,xz*_DetailScale),_DetailStrength);
    float2 slope=dn.xy*(1.0-puddle)*(1.0-0.5*_Wetness);
    // rain ripples in puddles, faint drops on wet ground
    float t=_Time.y*_RainSpeed;
    float2 rip=Ripple(xz*_RippleScale,t)+Ripple(xz*_RippleScale*1.37+3.1,t*1.13+0.5)+Ripple(xz*_RippleScale*0.83+7.7,t*0.91+0.25);
    slope+=rip*_RippleStrength*(puddle+0.15*_Wetness*(1-puddle));
    float3 nTop=normalize(float3(slope.x,1.0,slope.y));
    float3 nw=normalize(lerp(n,nTop,top));
    float wet=saturate(_Wetness+damp*0.3);
    SurfaceData s=(SurfaceData)0;
    half3 alb=c.rgb*_BaseColor.rgb;
    alb*=lerp(1.0,1.0-_WetDarken,wet);
    alb=lerp(alb,alb*0.5,puddle);
    s.albedo=alb;
    s.metallic=0;
    s.smoothness=lerp(lerp(0.12,0.32,wet),0.90,puddle);
    s.normalTS=half3(0,0,1); s.occlusion=lerp(0.75,1.0,puddle); s.alpha=1;
    InputData d=(InputData)0;d.positionWS=v.positionWS;d.normalWS=nw;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.positionWS);d.shadowCoord=TransformWorldToShadowCoord(v.positionWS);d.bakedGI=SampleSH(nw);d.vertexLighting=VertexLighting(v.positionWS,nw);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);d.shadowMask=half4(1,1,1,1);
    half4 col=UniversalFragmentPBR(d,s);
    col.rgb*=lerp(1.0,0.45,puddle);
    if(_PuddleReflectionOn>0.5){
     float NoV=saturate(dot(nw,d.viewDirectionWS));
     half F=0.02+0.98*pow(1.0-NoV,5.0);
     float3 pr=v.positionWS+float3(slope.x,0,slope.y)*_ReflectionDistort;
     float4 rc=mul(_PuddleReflectionVP,float4(pr,1));
     float rw=max(rc.w,1e-4);
     float2 ruv=rc.xy/rw*0.5+0.5;
     half inside=step(1e-3,rc.w)*smoothstep(0.0,0.04,ruv.x)*smoothstep(1.0,0.96,ruv.x)*smoothstep(0.0,0.04,ruv.y)*smoothstep(1.0,0.96,ruv.y);
     ruv=saturate(ruv);
     half3 sharp=SAMPLE_TEXTURE2D_LOD(_PuddleReflectionTex,sampler_PuddleReflectionTex,ruv,0).rgb;
     half3 blur=0;
     [unroll] for(int k=-2;k<=2;k++) blur+=SAMPLE_TEXTURE2D_LOD(_PuddleReflectionTex,sampler_PuddleReflectionTex,ruv+float2(0,k*_StreakLength),_ReflectionBlur).rgb;
     blur*=0.2;
     half3 refl=clamp(lerp(blur,sharp,puddle),0.0,8.0);
     half amount=saturate((puddle*_ReflectionStrength+(1.0-puddle)*wet*_WetReflection)*F)*inside;
     if(inside>0.001) col.rgb=lerp(col.rgb,col.rgb*0.6,amount)+refl*amount;
    }
    col.rgb=clamp(MixFog(col.rgb,v.fog),0.0,64.0);return col;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}



