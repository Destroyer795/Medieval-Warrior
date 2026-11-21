Shader "Medieval Warrior/Last Stand/Royal Fire"
{
    Properties
    {
        _Emission ("Emission", Float) = 3.5
        _Billboard ("Round spark", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+12" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            half _Emission, _Billboard;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_FOG_COORDS(1)
            };

            Varyings vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.color = input.color;
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 shape = input.uv * 2 - 1;
                float turbulence = sin(input.uv.x * 42 - _Time.y * 13) * .09;
                float cross = lerp(abs(shape.y + turbulence), length(shape), _Billboard);
                float falloff = saturate(1 - cross);
                float hotCore = pow(falloff, 4.4);
                float halo = falloff * falloff;
                half3 heat = lerp(input.color.rgb * half3(1, .58, .32), input.color.rgb, hotCore);
                half4 result = half4(heat * (halo * .30 + hotCore) * (input.color.a * _Emission), 0);
                UNITY_APPLY_FOG_COLOR(input.fogCoord, result, half4(0, 0, 0, 0));
                return result;
            }
            ENDCG
        }
    }
    Fallback Off
}
