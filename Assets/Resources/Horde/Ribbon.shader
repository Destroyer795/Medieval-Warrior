Shader "Medieval Warrior/Horde Ribbon"
{
    SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Blend SrcAlpha One ZWrite Off Cull Off
        Pass { CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        struct app { float4 vertex:POSITION; fixed4 color:COLOR;float2 uv:TEXCOORD0; };
        struct output { float4 pos:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0; };
        output vert(app v){output o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o;}
        fixed4 frag(output i):SV_Target {float edge=saturate(1-abs(i.uv.y*2-1));return fixed4(i.color.rgb*1.35,i.color.a*edge*edge);}
        ENDCG }
    }
}
