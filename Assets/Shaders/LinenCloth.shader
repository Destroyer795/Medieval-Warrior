Shader "MedievalWarrior/LinenCloth"
{
    Properties
    {
        _Color ("Linen tint", Color) = (.55,.51,.42,1)
        _MainTex ("Baked cloth finish", 2D) = "white" {}
        _BumpMap ("Tangent normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = .85
        _DetailAlbedoMap ("Woven detail", 2D) = "gray" {}
        _MetallicGlossMap ("Smoothness", 2D) = "black" {}
        _GlossMapScale ("Smoothness scale", Range(0,1)) = 1
        _Glossiness ("Smoothness fallback", Range(0,1)) = .08
        _UVSec ("Detail coordinates", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
        #pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
        #include "UnityStandardUtils.cginc"
        struct Input { float2 uv_MainTex; float2 uv2_DetailAlbedoMap; float face : VFACE; };
        sampler2D _MainTex, _BumpMap, _DetailAlbedoMap, _MetallicGlossMap;
        fixed4 _Color;
        half _BumpScale,_GlossMapScale;
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 baseColor=tex2D(_MainTex,IN.uv_MainTex).rgb;
            fixed3 detail=tex2D(_DetailAlbedoMap,IN.uv2_DetailAlbedoMap).rgb*unity_ColorSpaceDouble.rgb;
            o.Albedo=baseColor*detail*_Color.rgb;
            o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_MainTex),_BumpScale)*(IN.face>0?1:-1);
            o.Metallic=0;
            o.Smoothness=min(.12h,tex2D(_MetallicGlossMap,IN.uv_MainTex).a*_GlossMapScale);
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
