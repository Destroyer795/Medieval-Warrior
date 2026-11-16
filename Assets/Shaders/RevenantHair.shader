Shader "Medieval Warrior/Revenant Hair"
{
    Properties
    {
        _MainTex("Hair atlas",2D)="white"{}
        _NormalAtlas("Normal atlas",2D)="bump"{}
        _HairTint("Hair color",Color)=(1,1,1,1)
        _Cutoff("Strand cutoff",Range(0,1))=.24
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Cull Off
        ZWrite Off
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;float4 _HairTint;float _Cutoff;
        float4 Strand(float2 uv,float tile,float coverage)
        {
            float id=floor(tile+.1);float2 local=id<8.5?uv:uv*3;
            float2 atlas=(float2(fmod(id,4),floor(id/4))+(id<8.5?saturate(local):frac(local))*.974+.013)/4;
            float4 color=tex2Dgrad(_MainTex,atlas,ddx(local)*.2435,ddy(local)*.2435);
            color.a*=coverage;return color;
        }
        ENDCG
        Pass
        {
            Tags {"LightMode"="ForwardBase"}
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fwdbase
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float4 tangent:TANGENT;float2 uv:TEXCOORD0;float2 tile:TEXCOORD1;float4 color:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 normal:TEXCOORD1;float3 world:TEXCOORD2;float tile:TEXCOORD3;float4 color:COLOR;SHADOW_COORDS(4) float3 across:TEXCOORD5;};
            v2f vert(appdata v)
            {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.tile=v.tile.x;o.color=v.color;
                o.normal=UnityObjectToWorldNormal(v.normal);o.across=UnityObjectToWorldDir(v.tangent.xyz);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;TRANSFER_SHADOW(o);return o;
            }
            fixed4 frag(v2f i,fixed facing:VFACE):SV_Target
            {
                float4 color=Strand(i.uv,i.tile,i.color.a);clip(color.a-.005);float3 n=normalize(i.normal)*(facing>=0?1:-1);
                UNITY_LIGHT_ATTENUATION(atten,i,i.world);
                float diffuse=abs(dot(n,normalize(_WorldSpaceLightPos0.xyz)));
                float3 lighting=max(ShadeSH9(float4(n,1)),.035)+_LightColor0.rgb*diffuse*atten;
                float3 strand=normalize(cross(n,i.across));
                float3 halfVector=normalize(normalize(_WorldSpaceCameraPos-i.world)+normalize(_WorldSpaceLightPos0.xyz));
                float tangentDot=dot(strand,halfVector);
                float3 tint=color.rgb*_HairTint.rgb;
                return float4(tint*lighting,color.a);
            }
            ENDCG
        }
        Pass
        {
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On
            Blend Off
            CGPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma target 3.0
            #pragma multi_compile_shadowcaster
            struct shadowInput {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float2 tile:TEXCOORD1;float4 color:COLOR;};
            struct shadowOutput {V2F_SHADOW_CASTER;float2 uv:TEXCOORD1;float tile:TEXCOORD2;float4 color:COLOR;};
            shadowOutput vertShadow(shadowInput v)
            {
                shadowOutput o;TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);o.uv=v.uv;o.tile=v.tile.x;o.color=v.color;return o;
            }
            float4 fragShadow(shadowOutput i):SV_Target {clip(Strand(i.uv,i.tile,i.color.a).a-_Cutoff);SHADOW_CASTER_FRAGMENT(i)}
            ENDCG
        }
    }
    FallBack "Transparent/Cutout/Diffuse"
}
