Shader "Medieval Warrior/Horde Ground Gas"
{
    SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass { CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        struct app { float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0; };
        struct output {float4 pos:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float4 screen:TEXCOORD2;};
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float noise(float2 p){float2 q=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(q),hash(q+float2(1,0)),f.x),lerp(hash(q+float2(0,1)),hash(q+1),f.x),f.y);}
        output vert(app v){output o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.screen=ComputeScreenPos(o.pos);COMPUTE_EYEDEPTH(o.screen.z);return o;}
        fixed4 frag(output i):SV_Target
        { float2 uv=i.uv*2-1;float edge=saturate(1-dot(uv,uv));float n=noise(i.world.xz*2+_Time.y*.07)*.6+noise(i.world.xz*5-_Time.y*.12)*.4;
          float nearFade=saturate(distance(_WorldSpaceCameraPos,i.world)*.9-.25);
          float sceneDepth=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
          float soft=saturate((sceneDepth-i.screen.z)*1.7);
          return fixed4(i.color.rgb*(.7+n*.6),i.color.a*edge*edge*(.4+n)*nearFade*soft); }
        ENDCG }
    }
}
