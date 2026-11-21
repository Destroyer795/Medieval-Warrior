Shader "Medieval Warrior/Revenant PBR"
{
    Properties
    {
        _Color("Tint",Color)=(1,1,1,1)
        _MainTex("Decay / linen / metal atlas",2D)="white"{}
        _NormalAtlas("Tangent normal atlas",2D)="bump"{}
        _SurfaceAtlas("Metallic / smoothness atlas",2D)="black"{}
        _EmissionColor("Impact feedback",Color)=(0,0,0,0)
        _BloodMoon("Blood Moon eyes",Range(0,1))=0
        _SkinTex("Anatomical UV skin",2D)="white"{}
        _SkinNormal("Skin pores",2D)="bump"{}
        _SkinTint("Skin variation",Color)=(1,1,1,1)
        _ClothTint("Cloth variation",Color)=(1,1,1,1)
        _TrouserTint("Trouser variation",Color)=(.58,.55,.45,1)
        _FacingTint("Garment facing variation",Color)=(.70,.57,.35,1)
        _MetalTint("Weathered metal variation",Color)=(1,1,1,1)
        _HairTint("Hair variation",Color)=(1,1,1,1)
        _Blood0("Blood contact 0",Vector)=(0,0,0,0)
        _Blood1("Blood contact 1",Vector)=(0,0,0,0)
        _Blood2("Blood contact 2",Vector)=(0,0,0,0)
        _BloodNormal0("Blood normal 0",Vector)=(0,1,0,0)
        _BloodNormal1("Blood normal 1",Vector)=(0,1,0,0)
        _BloodNormal2("Blood normal 2",Vector)=(0,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 250
        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        sampler2D _MainTex,_NormalAtlas,_SurfaceAtlas,_SkinTex,_SkinNormal;
        fixed4 _Color,_EmissionColor,_SkinTint,_ClothTint,_TrouserTint,_FacingTint,_MetalTint,_HairTint;
        float4 _Blood0,_Blood1,_Blood2,_BloodNormal0,_BloodNormal1,_BloodNormal2;
        float _BloodMoon;
        struct Input { float2 uv_MainTex; float tile; float4 color:COLOR;float3 worldPos; };
        float3 Blood(float3 color,float3 positionWS,float4 site,float4 facing)
        {
            if(site.w<.001)return color;
            float3 delta=positionWS-site.xyz;
            if(dot(delta,delta)>site.w*site.w*1.2)return color;
            float3 n=normalize(facing.xyz);float depth=dot(delta,n);
            float3 tangent=normalize(cross(n,abs(n.y)>.95?float3(0,0,1):float3(0,1,0)));
            float2 uv=float2(dot(delta,tangent),dot(delta,cross(n,tangent)))/site.w;
            float angle=atan2(uv.y,uv.x);float irregular=1+.12*sin(angle*7)+.07*sin(angle*13);
            float mask=(1-smoothstep(.58,irregular,length(uv)))*(1-smoothstep(site.w*.25,site.w*.55,abs(depth)));
            float grain=.7+.3*sin(uv.x*53+sin(uv.y*27))*.5;
            return lerp(color,float3(.14,.008,.004)*grain,mask*.94);
        }
        void vert(inout appdata_full v,out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input,o);
            o.tile=v.texcoord1.x;
            o.color=v.color;
        }
        void surf(Input IN,inout SurfaceOutput o)
        {
            float id=floor(IN.tile+.1);
            float2 cell=float2(fmod(id,4),floor(id/4));
            float2 local=IN.uv_MainTex*3;
            float2 uv=(cell+frac(local)*.974+.013)/4;
            float2 dx=ddx(local)*.2435,dy=ddy(local)*.2435;
            fixed4 albedo=tex2Dgrad(_MainTex,uv,dx,dy);
            fixed4 spec=tex2Dgrad(_SurfaceAtlas,uv,dx,dy);
            float3 n=tex2Dgrad(_NormalAtlas,uv,dx,dy).rgb*2-1;
            o.Albedo=albedo.rgb*IN.color.rgb*_Color.rgb;
            o.Normal=normalize(float3(n.xy*.65,n.z));
            if(id<.5)
            {
                o.Albedo=tex2D(_SkinTex,IN.uv_MainTex).rgb*IN.color.rgb*_Color.rgb*_SkinTint.rgb;
                float3 pores=tex2D(_SkinNormal,IN.uv_MainTex).rgb*2-1;
                o.Normal=normalize(float3(pores.xy*.45,pores.z));
            }
            if(id>.5&&id<1.5)o.Albedo*=_ClothTint.rgb;
            if(id>11.5&&id<12.5)o.Albedo*=_TrouserTint.rgb;
            if(id>12.5&&id<13.5)o.Albedo*=_FacingTint.rgb;
            if(id>1.5&&id<2.5)o.Albedo*=_MetalTint.rgb;
            if(id>7.5&&id<9.5)o.Albedo*=_HairTint.rgb;
            o.Albedo=Blood(o.Albedo,IN.worldPos,_Blood0,_BloodNormal0);
            o.Albedo=Blood(o.Albedo,IN.worldPos,_Blood1,_BloodNormal1);
            o.Albedo=Blood(o.Albedo,IN.worldPos,_Blood2,_BloodNormal2);
            o.Emission=_EmissionColor.rgb;
            if(id>6.5&&id<7.5)
            {
                o.Albedo=lerp(o.Albedo,float3(.8,.015,.006),_BloodMoon);
                o.Emission+=lerp(float3(.032,.039,.028),float3(2.8,.025,.008),_BloodMoon);
            }
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Standard"
}
