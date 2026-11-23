Shader "Medieval Warrior/Shield Heraldry"
{
    Properties
    {
        _Color ("Steel Tint", Color) = (.48, .51, .54, 1)
        _MainTex ("Forged Steel", 2D) = "white" {}
        _BumpMap ("Dents", 2D) = "bump" {}
        _BumpScale ("Dents Strength", Float) = .85
        _MetallicGlossMap ("Steel Metalness and Gloss", 2D) = "white" {}
        _GlossMapScale ("Steel Gloss", Range(0, 1)) = 1
        _DetailAlbedoMap ("Fine Wear", 2D) = "gray" {}
        _Field ("Heraldic Field", Color) = (.55, .51, .42, 1)
        _Pigment ("Charge Pigment", Color) = (.85, .68, .36, 1)
        _ChargeEdge ("Charge Edge", Color) = (0, 0, 0, 0)
        _Charge ("Heraldic Charge", 2D) = "black" {}
        _HasCharge ("Show Charge", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard vertex:project
        sampler2D _MainTex, _BumpMap, _MetallicGlossMap, _DetailAlbedoMap, _Charge;
        fixed4 _Color, _Field, _Pigment, _ChargeEdge;
        float4 _Charge_TexelSize;
        float _BumpScale, _GlossMapScale, _HasCharge;
        struct Input
        {
            float2 uv_MainTex;
            float2 uv2_DetailAlbedoMap;
            float3 platePosition;
            float normalForward;
        };
        void project(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.platePosition = v.vertex.xyz;
            o.normalForward = v.normal.z;
        }
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex;
            fixed4 steel = tex2D(_MainTex, uv);
            fixed4 finish = tex2D(_MetallicGlossMap, uv);
            fixed3 grain = tex2D(_DetailAlbedoMap, IN.uv2_DetailAlbedoMap).rgb * unity_ColorSpaceDouble.rgb;
            float length = saturate((.285 - IN.platePosition.x) / .675);
            float halfWidth = length < .22 ? lerp(.238, .235, length / .22) :
                length < .50 ? lerp(.235, .190, (length - .22) / .28) :
                length < .78 ? lerp(.190, .108, (length - .50) / .28) :
                lerp(.108, .005, (length - .78) / .22);
            float trim = min(min(.285 - IN.platePosition.x, IN.platePosition.x + .390),
                halfWidth - abs(IN.platePosition.y));
            float enamel = smoothstep(.020, .031, trim) * smoothstep(.35, .60, IN.normalForward)
                * smoothstep(.024, .029, IN.platePosition.z);
            // Object space follows the curve of the shield. Local X is up.
            float2 heraldry = saturate(float2(.5 + IN.platePosition.y / .42, .5 + (IN.platePosition.x - .045) / .50));
            float glyph = _HasCharge * tex2D(_Charge, heraldry).a;
            float edgeMask = 0;
            if (_ChargeEdge.a > 0)
            {
                float2 stepSize = _Charge_TexelSize.xy * 3.0;
                edgeMask = max(max(tex2D(_Charge, heraldry + float2(stepSize.x, 0)).a,
                               tex2D(_Charge, heraldry - float2(stepSize.x, 0)).a),
                           max(tex2D(_Charge, heraldry + float2(0, stepSize.y)).a,
                               tex2D(_Charge, heraldry - float2(0, stepSize.y)).a));
            }
            float wear = saturate(dot(steel.rgb * grain, float3(.299, .587, .114)) * 1.35 + .24);
            fixed3 pigment = lerp(lerp(_Field.rgb, _ChargeEdge.rgb, edgeMask), _Pigment.rgb, glyph) * wear;
            o.Albedo = lerp(steel.rgb * grain * _Color.rgb, pigment, enamel);
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);
            o.Metallic = lerp(finish.r, .14, enamel);
            o.Smoothness = lerp(finish.a * _GlossMapScale, .36 + .10 * glyph, enamel);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
