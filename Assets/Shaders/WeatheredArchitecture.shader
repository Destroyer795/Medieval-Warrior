Shader "Medieval Warrior/Weathered Architecture"
{
 Properties
 {
  _Color("Tint",Color)=(1,1,1,1)
  _MainTex("Architectural atlas",2D)="white"{}
  [Normal]_BumpMap("Atlas normal",2D)="bump"{}
  _BumpScale("Atlas normal strength",Range(0,2))=1
  _MetallicGlossMap("Metallic / smoothness atlas",2D)="black"{}
  _OcclusionMap("Baked contact AO",2D)="white"{}
  _OcclusionStrength("Contact AO",Range(0,1))=1
  _GlossMapScale("Smoothness scale",Range(0,1))=1
  _Glossiness("Fallback smoothness",Range(0,1))=0
  _Metallic("Fallback metallic",Range(0,1))=0
  _StoneAlbedo("4K masonry albedo",2D)="white"{}
  [Normal]_StoneNormal("Masonry relief normal",2D)="bump"{}
  _StoneSurface("Masonry AO / height / smoothness",2D)="white"{}
  _RubbleAlbedo("Foundation rock albedo",2D)="white"{}
  [Normal]_RubbleNormal("Foundation rock relief",2D)="bump"{}
  _RubbleSurface("Foundation rock AO / height",2D)="white"{}
  _StoneMetres("Masonry repeat in metres",Float)=2.5
  _RubbleMetres("Foundation rock repeat in metres",Float)=2
  _StoneNormalStrength("Stone normal depth",Range(0,3))=1.35
  _ParallaxMetres("Recessed mortar depth in metres",Range(0,.08))=.035
  _ParallaxDistance("Full relief distance",Float)=18
  _ProjectionMode("0 plane, 1 round tower, 2 four bastions",Float)=0
  _LocalCenter("Measured local centre",Vector)=(0,0,0,0)
  _LocalSize("Measured local size",Vector)=(1,1,1,0)
  _VertexAO("Baked vertex AO contribution",Range(0,1))=.65
  _WeatherStrength("Environmental weathering",Range(0,1))=.72
  [ToggleOff]_SpecularHighlights("Specular highlights",Float)=1
  [ToggleOff]_GlossyReflections("Glossy reflections",Float)=1
 }
 SubShader
 {
  Tags {"RenderType"="Opaque"} LOD 350
  CGPROGRAM
  #pragma target 3.0
  #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
  #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
  #pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
  #pragma multi_compile_instancing
  #include "UnityCG.cginc"
  #include "UnityStandardUtils.cginc"
  sampler2D _MainTex,_BumpMap,_MetallicGlossMap,_OcclusionMap;
  sampler2D _StoneAlbedo,_StoneNormal,_StoneSurface,_EnvironmentGroundHeight;
  sampler2D _RubbleAlbedo,_RubbleNormal,_RubbleSurface;
  float4 _Color,_LocalCenter,_LocalSize,_EnvironmentTerrainOrigin,_EnvironmentTerrainSize;
  float _BumpScale,_OcclusionStrength,_GlossMapScale,_StoneMetres,_RubbleMetres,_StoneNormalStrength;
  float _ParallaxMetres,_ParallaxDistance,_ProjectionMode,_VertexAO,_WeatherStrength;
  float _EnvironmentGroundMode,_EnvironmentFlatGround;
  struct Input {float2 uv_MainTex;float3 localPosition;float3 localNormal;float3 worldPos;float3 worldNormal;float4 color:COLOR;INTERNAL_DATA};
  void vert(inout appdata_full v,out Input o)
  {
   UNITY_INITIALIZE_OUTPUT(Input,o);o.localPosition=v.vertex.xyz;o.localNormal=v.normal;o.color=v.color;
  }
  float hash21(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float field(float2 p)
  {
   float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
   return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
  }
  float groundHeight(float3 world)
  {
   if(_EnvironmentGroundMode>1.5)return _EnvironmentFlatGround;
   if(_EnvironmentGroundMode>.5)
   {
    float2 uv=(world.xz-_EnvironmentTerrainOrigin.xz)/max(_EnvironmentTerrainSize.xz,.001);
    float2 rg=tex2D(_EnvironmentGroundHeight,saturate(uv)).rg;
    return _EnvironmentTerrainOrigin.y+(rg.x+rg.y/255)*_EnvironmentTerrainSize.y;
   }
   return mul(unity_ObjectToWorld,float4(0,_LocalCenter.y-_LocalSize.y*.5,0,1)).y;
  }
  float2 masonryUV(float3 local,float3 normal)
  {
   float3 scale=float3(length(unity_ObjectToWorld._m00_m10_m20),length(unity_ObjectToWorld._m01_m11_m21),length(unity_ObjectToWorld._m02_m12_m22));
   float3 p=local-_LocalCenter.xyz;float3 n=abs(normal);float2 uv;
   if(n.y>.72)return p.xz*scale.xz/_StoneMetres;
   float2 center=0;float radius=_LocalSize.x*.5;float start=0;float curved=_ProjectionMode;
   if(_ProjectionMode>1.5)
   {
    radius=_LocalSize.z*.25;center=sign(p.xz)*(_LocalSize.xz*.5-radius);
    float d=length(p.xz-center);curved=(d>radius*.80&&d<radius*1.10)?1:0;
    start=p.x>0?(p.z>0?4.712389:3.141593):(p.z>0?0:1.570796);
   }
   if(curved>.5)
   {
    float a=atan2(p.z-center.y,p.x-center.x)-start;
    a=a<0?a+6.283185:a;
    // An integral circumference repeat closes the cylindrical UV seam.
    float repeats=max(1,round(6.283185*radius*scale.x/_StoneMetres));
    uv=float2(a/6.283185*repeats,p.y*scale.y/_StoneMetres);
   }
   else uv=n.x>n.z?float2(p.z*scale.z*sign(normal.x),p.y*scale.y)/_StoneMetres:float2(-p.x*scale.x*sign(normal.z),p.y*scale.y)/_StoneMetres;
   return uv;
  }
  float reliefHeight(float2 uv,float rubble){return rubble>.5?tex2Dlod(_RubbleSurface,float4(uv,0,0)).b:tex2Dlod(_StoneSurface,float4(uv,0,0)).b;}
  float2 relief(float2 uv,float3 view,float distance,float rubble)
  {
   float fade=1-smoothstep(_ParallaxDistance*.65,_ParallaxDistance,distance);
   if(fade<.01||abs(view.z)<.12)return uv;
   const int steps=14;float delta=1.0/steps;
   float2 stepUV=view.xy/max(.28,abs(view.z))*(_ParallaxMetres/(rubble>.5?_RubbleMetres:_StoneMetres))*fade/steps;
   float2 p=uv;float layer=0;float height=1-reliefHeight(p,rubble);
   [loop]for(int k=0;k<steps;k++)
   {
    if(layer>=height)break;p-=stepUV;layer+=delta;height=1-reliefHeight(p,rubble);
   }
   float2 previous=p+stepUV;float after=height-layer;float before=1-reliefHeight(previous,rubble)-layer+delta;
   return lerp(p,previous,saturate(after/(after-before+.00001)));
  }
  void surf(Input IN,inout SurfaceOutputStandard o)
  {
   float2 atlas=IN.uv_MainTex;float tile=floor(atlas.x*4)+floor(atlas.y*4)*4;
   float stone=(tile<1.5)?1:0;float foundation=abs(tile-1)<.2?1:0;
   float4 base=tex2D(_MainTex,atlas)*_Color;float4 mask=tex2D(_MetallicGlossMap,atlas);
   float3 N=normalize(WorldNormalVector(IN,float3(0,0,1)));
   float3 oldT=normalize(WorldNormalVector(IN,float3(1,0,0))),oldB=normalize(WorldNormalVector(IN,float3(0,1,0)));
   o.Albedo=base.rgb;o.Metallic=mask.r;o.Smoothness=mask.a*_GlossMapScale;o.Normal=UnpackScaleNormal(tex2D(_BumpMap,atlas),_BumpScale);
   float ao=lerp(1,tex2D(_OcclusionMap,atlas).g,_OcclusionStrength);
   float2 uv=masonryUV(IN.localPosition,normalize(IN.localNormal));
   uv*=foundation>.5?_StoneMetres/_RubbleMetres:1;
   float3 dpdx=ddx(IN.worldPos),dpdy=ddy(IN.worldPos);float2 dx=ddx(uv),dy=ddy(uv);
   float determinant=dx.x*dy.y-dx.y*dy.x;float orientation=determinant<0?-1:1;
   float3 T=normalize((dpdx*dy.y-dpdy*dx.y)*orientation+float3(.000001,0,0));
   T=normalize(T-N*dot(T,N));float3 B=normalize(cross(N,T))*orientation;
   float surfaceCrevice=0;
   if(stone>.5)
   {
    float3 V=normalize(_WorldSpaceCameraPos-IN.worldPos);uv=relief(uv,float3(dot(V,T),dot(V,B),dot(V,N)),distance(_WorldSpaceCameraPos,IN.worldPos),foundation);
    float4 s=foundation>.5?tex2Dgrad(_RubbleSurface,uv,dx,dy):tex2Dgrad(_StoneSurface,uv,dx,dy);
    float4 packedNormal=foundation>.5?tex2Dgrad(_RubbleNormal,uv,dx,dy):tex2Dgrad(_StoneNormal,uv,dx,dy);
    float3 stoneNormal=UnpackScaleNormal(packedNormal,_StoneNormalStrength*(1+foundation*.35));
    float3 W=normalize(T*stoneNormal.x+B*stoneNormal.y+N*stoneNormal.z);
    o.Normal=float3(dot(W,oldT),dot(W,oldB),dot(W,N));
    o.Albedo=(foundation>.5?tex2Dgrad(_RubbleAlbedo,uv,dx,dy).rgb:tex2Dgrad(_StoneAlbedo,uv,dx,dy).rgb)*_Color.rgb;o.Metallic=0;o.Smoothness=s.a;
    surfaceCrevice=foundation>.5?saturate((.6-s.b)*2):1-smoothstep(.18,.65,s.b);
    ao=min(ao,s.g);
   }
   // These estimates describe the same crevices: multiplying all three would
   // blacken shaded facades. Preserve the strongest occlusion estimate instead.
   ao=min(ao,lerp(1,max(.48,IN.color.a),_VertexAO));
   float above=max(0,IN.worldPos.y-groundHeight(IN.worldPos));
   float patch=field(IN.worldPos.xz*.78)+field(IN.worldPos.xz*2.1)*.35;
   float dirtHeight=min(1.1,max(.18,_LocalSize.y*length(unity_ObjectToWorld._m01_m11_m21)*.22));
   float dirt=exp(-above/max(.1,dirtHeight))*(.45+patch*.5)*_WeatherStrength;
   float streak=smoothstep(.46,.75,field(float2(uv.x*24,uv.y*.27)))*stone;
   float water=streak*(.16+.22*field(IN.worldPos.xz*.4))*_WeatherStrength;
   float crevice=max(surfaceCrevice,saturate((1-ao)*1.8+(tile>8.5&&tile<9.5?.35:0)));
   float moss=crevice*saturate(patch-.38)*2.1*exp(-above/2.4)*_WeatherStrength;
   float solid=(tile>7.5&&tile<8.5)?0:1;
   o.Albedo*=1-solid*(dirt*.28+water);
   o.Albedo=lerp(o.Albedo,o.Albedo*.22+float3(.055,.10,.025),saturate(moss)*solid);
   o.Occlusion=ao;o.Alpha=1;
  }
  ENDCG
 }
 FallBack "Standard"
}
