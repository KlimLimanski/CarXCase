Shader "Project/Elevated Steel Rust"
{
 Properties {
 _BaseMap("Weathered paint",2D)="white"{}
 _BaseColor("Paint tint",Color)=(1,1,1,1)
 _TextureScale("Texture repeats per metre",Float)=0.7
 _RustAmount("Additional rust amount",Range(0,1))=0.28
 _RustColor("Rust colour",Color)=(0.24,0.085,0.028,1)
 _RustScale("Rust patch scale",Float)=2.4
 _Metallic("Metallic",Range(0,1))=0.35
 _Smoothness("Paint smoothness",Range(0,1))=0.28
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
 // Painted, heavily weathered steel: suppress grazing-angle specular glare.
 // Keep diffuse lighting and received shadows from the scene lights.
 #define _SPECULARHIGHLIGHTS_OFF 1
 #define _ENVIRONMENTREFLECTIONS_OFF 1
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor, _RustColor;
 float _TextureScale,_RustAmount,_RustScale,_Metallic,_Smoothness;
 CBUFFER_END
 struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float fog:TEXCOORD2;};
 V vert(A v){V o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
 float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
 float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);}
 half4 frag(V v):SV_Target {
 float3 n=normalize(v.normalWS),w=pow(abs(n),4);w/=max(dot(w,1),0.001);
 float3 p=v.positionWS*_TextureScale;
 half3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.yz).rgb*w.x+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb*w.y+SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb*w.z;
 float grain=noise(v.positionWS*75);
 float patches=noise(v.positionWS*_RustScale)*0.75+noise(v.positionWS*_RustScale*4)*0.25;
 float rust=smoothstep(1-_RustAmount,1-_RustAmount+0.12,patches)*step(0.001,_RustAmount);
 SurfaceData s=(SurfaceData)0;
 s.albedo=lerp(base*_BaseColor.rgb,_RustColor.rgb*lerp(0.65,1.25,grain),rust);
 s.metallic=lerp(_Metallic,0,rust);s.smoothness=lerp(_Smoothness,0.06,rust);s.normalTS=half3(0,0,1);s.occlusion=1;s.alpha=1;
 InputData d=(InputData)0;d.positionWS=v.positionWS;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.positionWS);d.shadowCoord=TransformWorldToShadowCoord(v.positionWS);d.bakedGI=SampleSH(n);d.vertexLighting=VertexLighting(v.positionWS,n);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);d.shadowMask=half4(1,1,1,1);
 half4 c=UniversalFragmentPBR(d,s);c.rgb=MixFog(c.rgb,v.fog);return c;
 }
 ENDHLSL
 }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}
