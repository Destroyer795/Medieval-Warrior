Shader "Medieval Warrior/Last Stand/Sky"
{
    Properties
    {
        [NoScaleOffset] _Tex ("Sky cubemap", Cube) = "grey" {}
        _Rotation ("Cloud rotation", Range(0, 360)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _Tex;
            float4 _Tex_HDR;
            float _Rotation, _SkyTime;
            float3 _SunDirection;
            float4 _Weather; // Storm, dawn, break in the clouds, lightning.

            struct Interpolators
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
                float3 clouds : TEXCOORD1;
            };

            Interpolators vert(appdata_base input)
            {
                Interpolators output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = input.vertex.xyz;
                float s, c;
                sincos(radians(_Rotation), s, c);
                output.clouds = float3(c * input.vertex.x - s * input.vertex.z, input.vertex.y,
                    s * input.vertex.x + c * input.vertex.z);
                return output;
            }

            float4 frag(Interpolators input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                float3 drift = float3(sin(_SkyTime * .088) * .018, .036, cos(_SkyTime * .071) * .022);
                float3 clouds = DecodeHDR(texCUBE(_Tex, input.clouds), _Tex_HDR);
                float3 highClouds = DecodeHDR(texCUBE(_Tex, normalize(input.clouds) + drift), _Tex_HDR);
                clouds = lerp(clouds, highClouds, .36);
                float luma = dot(clouds, float3(.2126, .7152, .0722));
                float cloud = saturate(luma * .67);
                float horizon = exp(-abs(ray.y) * 4.1);
                float3 midnight = float3(.004, .008, .022) + cloud * float3(.022, .035, .067)
                    + horizon * float3(.008, .016, .03);
                float3 storm = float3(.016, .003, .01) + cloud * float3(.061, .012, .024)
                    + horizon * float3(.06, .008, .018);
                float3 color = lerp(midnight, storm, _Weather.x);
                color += _Weather.w * lerp(float3(.11, .09, .22), float3(.41, .27, .5), cloud);

                float sun = saturate(dot(ray, normalize(_SunDirection)));
                float halo = pow(sun, 12) * (.24 + horizon);
                float opacity = smoothstep(.27, .74, cloud);
                float opening = smoothstep(.03, .85, _Weather.z - opacity * .21);
                float cloudBreak = smoothstep(.873, .997, sun) * opening;
                float diskExposure = smoothstep(.32, .89, _Weather.z - opacity * .11);
                float sunDisk = smoothstep(.99954, .99983, sun) * diskExposure;
                float3 dawn = clouds * float3(.47, .48, .51) + horizon * float3(.25, .129, .061);
                dawn += halo * float3(.89, .44, .17) * lerp(.61, 1.3, cloud);
                color = lerp(color, dawn, _Weather.y);
                color += halo * float3(.45, .164, .044) * _Weather.z * (1 - _Weather.y);
                color += float3(1.27, .63, .24) * cloudBreak * (.14 + .36 * _Weather.y);
                color += sunDisk * float3(4.2, 2.7, 1.44);
                return float4(color, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
