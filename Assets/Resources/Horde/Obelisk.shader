Shader "Medieval Warrior/Infected Basalt"
{
    Properties
    {
        _MainTex("Weathered stone scan",2D)="white"{}
        _BumpMap("Stone relief",2D)="bump"{}
        _Color("Stone tint",Color)=(1,1,1,1)
        _Curse("Infected veins",Range(0,1))=0
    }
    SubShader { Tags { "RenderType"="Opaque" } LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        struct Input {float3 worldPos;float2 uv_MainTex;float4 color:COLOR;};
        sampler2D _MainTex,_BumpMap;fixed4 _Color;float _Curse;
        float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,93.3)))*43758.5453);}
        float cracks(float2 p)
        {
            float2 cell=floor(p),f=frac(p);float nearest=9,second=9;
            for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
            {
                float2 offset=float2(x,y);float2 q=cell+offset;
                float2 center=float2(hash(float3(q,3)),hash(float3(q,7)));
                float d=length(offset+center-f);
                if(d<nearest){second=nearest;nearest=d;}else second=min(second,d);
            }
            return 1-smoothstep(.008,.026,second-nearest);
        }
        void surf(Input IN,inout SurfaceOutputStandard o)
        { float3 p=IN.worldPos;float veins=cracks(IN.uv_MainTex*2.3)*_Curse;
          fixed3 scan=tex2D(_MainTex,IN.uv_MainTex*.7).rgb;
          o.Albedo=scan*_Color.rgb*IN.color.rgb*(1-veins*.55);
          o.Smoothness=.035;o.Metallic=0;o.Occlusion=.85;
          o.Normal=UnpackNormal(tex2D(_BumpMap,IN.uv_MainTex*.7));
          o.Emission=veins*float3(.28,.015,.46)*(.7+.2*sin(_Time.y*2)); }
        ENDCG
    }
    FallBack "Standard"
}
