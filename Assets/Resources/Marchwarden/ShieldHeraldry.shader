Shader "Medieval Warrior/Shield Heraldry"
{
    Properties
    {
        _Paint ("Pigment", Color) = (.85, .73, .50, 1)
        _Crest ("Crest", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry+3" "RenderType"="TransparentCutout" }
        Offset -1, -1
        ZWrite Off
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard vertex:project noforwardadd
        fixed4 _Paint;
        float _Crest;
        struct Input
        {
            float3 shape;
            float faceNormal;
        };
        void project(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.shape = v.vertex.xyz;
            o.faceNormal = v.normal.z;
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            clip(min(IN.shape.z - .024, IN.faceNormal - .28));
            float2 p = float2(IN.shape.y, IN.shape.x - .035);
            float edge = max(abs(p.x) - .118, abs(p.y) - .147);
            float arms = min(max(abs(p.x) - .024, abs(p.y) - .147),
                max(abs(p.x) - .118, abs(p.y - .043) - .023));
            if (_Crest > 2.5) arms = abs(abs(p.y) - abs(p.x) * 1.24) - .026;
            else if (_Crest > 1.5) arms = abs(p.y + abs(p.x) * 1.05 - .05) - .032;
            clip(-max(edge, arms));
            o.Albedo = _Paint.rgb;
            o.Metallic = .06;
            o.Smoothness = .29;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack Off
}
