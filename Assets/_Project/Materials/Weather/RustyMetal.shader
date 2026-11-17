Shader "Project/Rusty Metal"
{
 Properties {
  _BaseMap("Rust albedo (A = smoothness)",2D)="white"{}
  _NormalMap("Rust normal",2D)="bump"{}
  _BaseColor("Tint",Color)=(1,1,1,1)
  _TextureScale("Texture repeats per metre",Float)=0.45
  _NormalStrength("Normal strength",Range(0,2))=1
  _Metallic("Metallic",Range(0,1))=0.2
  _Wetness("Rain wetness",Range(0,1))=0.45
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
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST, _NormalMap_ST, _BaseColor;
   float _TextureScale,_NormalStrength,_Metallic,_Wetness;
   CBUFFER_END
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
   struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float fog:TEXCOORD2;};
   V vert(A v){V o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 frag(V v):SV_Target {
    float3 n=normalize(v.normalWS);
    float3 w=pow(abs(n),4); w/=max(dot(w,1),0.001);
    float3 p=v.positionWS*_TextureScale;
    float2 uvX=p.zy, uvY=p.xz+0.37, uvZ=p.xy+0.71;
    half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uvX)*w.x+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uvY)*w.y+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uvZ)*w.z;
    half3 tx=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,uvX),_NormalStrength);
    half3 ty=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,uvY),_NormalStrength);
    half3 tz=UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,uvZ),_NormalStrength);
    half3 sg=step(0,n)*2-1;
    tx.z*=sg.x; ty.z*=sg.y; tz.z*=sg.z;
    tx=half3(tx.xy+n.zy,abs(tx.z)*n.x);
    ty=half3(ty.xy+n.xz,abs(ty.z)*n.y);
    tz=half3(tz.xy+n.xy,abs(tz.z)*n.z);
    float3 nw=normalize(tx.zyx*w.x+ty.xzy*w.y+tz.xyz*w.z);
    // rain: upward faces hold water, vertical faces get darker streaks
    float top=saturate(n.y);
    float streak=frac(sin(dot(floor(v.positionWS.xz*7),float2(12.9898,78.233)))*43758.5453);
    float wet=_Wetness*saturate(0.45+0.55*top+0.25*streak*(1-top));
    SurfaceData s=(SurfaceData)0;
    s.albedo=c.rgb*_BaseColor.rgb*lerp(1,0.62,wet);
    s.metallic=_Metallic*(1-wet*0.5);
    s.smoothness=lerp(c.a,0.78,wet*0.75);
    s.normalTS=half3(0,0,1); s.occlusion=1; s.alpha=1;
    nw=normalize(lerp(nw,n,wet*top*0.6));
    InputData d=(InputData)0;d.positionWS=v.positionWS;d.normalWS=nw;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.positionWS);d.shadowCoord=TransformWorldToShadowCoord(v.positionWS);d.bakedGI=SampleSH(nw);d.vertexLighting=VertexLighting(v.positionWS,nw);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);d.shadowMask=half4(1,1,1,1);
    half4 col=UniversalFragmentPBR(d,s);col.rgb=MixFog(col.rgb,v.fog);return col;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
