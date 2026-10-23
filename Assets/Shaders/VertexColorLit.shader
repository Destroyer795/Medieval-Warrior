// Lit, double-sided surface that takes its colour from mesh vertex colours.
// Used for the waving tower banners.
Shader "MedievalWarrior/VertexColorLit"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
            float face : VFACE;
        };

        fixed4 _Color;
        half _Glossiness;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = IN.color.rgb * _Color.rgb;
            o.Normal = float3(0, 0, IN.face > 0 ? 1 : -1);   // light the back of the cloth too
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
