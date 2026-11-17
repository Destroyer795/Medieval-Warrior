Shader "Medieval Warrior/Baked Ground Contact"
{
 Properties{_MainTex("Baked contact occlusion",2D)="white"{}}
 SubShader
 {
  Tags{"Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Blend DstColor Zero ZWrite Off Offset -1,-1 Cull Off
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   struct v2f{float4 position:SV_POSITION;float2 uv:TEXCOORD0;UNITY_FOG_COORDS(1)};
   v2f vert(appdata_base v){v2f o;o.position=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;UNITY_TRANSFER_FOG(o,o.position);return o;}
   fixed4 frag(v2f i):SV_Target{fixed a=tex2D(_MainTex,i.uv).r;fixed4 c=fixed4(a,a,a,1);UNITY_APPLY_FOG_COLOR(i.fogCoord,c,fixed4(1,1,1,1));return c;}
   ENDCG
  }
 }
}
