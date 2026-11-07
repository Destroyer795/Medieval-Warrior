Shader "MedievalWarrior/AcidPuddle"
{
    Properties
    {
        [HideInInspector] _MainTex ("Coordinates", 2D) = "white" {}
        _Color ("Acid", Color) = (.22,.32,.045,.82)
        _Fade ("Lifetime fade", Range(0,1)) = 1
        _Spread ("Spread", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200
        ZWrite Off
        Cull Off
        Offset -1,-1
        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0
        fixed4 _Color;
        float _Fade, _Spread;
        struct Input { float2 uv2_MainTex; float3 worldPos; };
        sampler2D _MainTex;
        float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float noise(float2 p)
        {
            float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
            return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 p=IN.worldPos.xz;
            float n=noise(p*5+float2(_Time.y*.035,0))*.7+noise(p*17)*.3;
            float2 cell=floor(p*11),f=frac(p*11)-.5;
            float bubbleSize=.06+.16*hash(cell+7);
            float bubble=smoothstep(.035,0,abs(length(f)-bubbleSize))*step(.65,hash(cell));
            float activity=.65+.35*sin(_Time.y*2+hash(cell)*6.28);
            float edge=1-smoothstep(_Spread-.09,_Spread,IN.uv2_MainTex.x);
            o.Albedo=lerp(_Color.rgb*.45,_Color.rgb*1.6,n)+float3(.12,.16,.015)*bubble*activity;
            o.Emission=float3(.035,.055,.002)*(bubble*.6+n*.16);
            o.Metallic=0; o.Smoothness=.86;
            o.Normal=normalize(float3((noise(p*12+float2(.1,0))-noise(p*12-float2(.1,0)))*.14,
                (noise(p*12+float2(0,.1))-noise(p*12-float2(0,.1)))*.14,1));
            o.Alpha=_Color.a*_Fade*edge*(.82+n*.18);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
