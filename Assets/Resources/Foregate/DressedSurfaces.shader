Shader "Medieval Warrior/Foregate Dressed Surfaces"
{
    Properties
    {
        _MainTex ("Castle albedo atlas", 2D) = "white" {}
        [Normal] _BumpMap ("Castle relief atlas", 2D) = "bump" {}
        _Masks ("Castle metal, occlusion, height and smoothness", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows addshadow
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex, _BumpMap, _Masks;
        struct Input
        {
            float2 uv_MainTex;
            float2 uv2_Masks;
            float4 color : COLOR;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Each cut piece carries one atlas cell and its weathering tint. Supply the
            // unwrapped derivatives so a repeat boundary cannot select a blurry atlas mip.
            float2 cell = round(IN.uv2_Masks);
            float2 uv = cell * 0.25 + 0.002 + frac(IN.uv_MainTex) * 0.245;
            float2 dx = ddx(IN.uv_MainTex) * 0.245;
            float2 dy = ddy(IN.uv_MainTex) * 0.245;
            fixed4 masks = tex2Dgrad(_Masks, uv, dx, dy);
            o.Albedo = tex2Dgrad(_MainTex, uv, dx, dy).rgb * IN.color.b;
            o.Normal = UnpackScaleNormal(tex2Dgrad(_BumpMap, uv, dx, dy), 0.82);
            o.Metallic = masks.r;
            o.Occlusion = masks.g;
            o.Smoothness = masks.a;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
