Shader "Medieval Warrior/Last Stand/Miasma"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "white" {}
        _Turbulence ("Turbulence", 3D) = "white" {}
        _Relief ("Terrain relief", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex, _Relief, _LastStandFog;
        sampler3D _Turbulence;
        float4 _MainTex_TexelSize;
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        float4 _Zone;     // Courtyard center and safe radius.
        float4 _Weather;  // Match time, storm strength, smoke density and dawn retreat in meters.
        float4 _Air;      // Ceiling, high ground exposure, warm haze and sunlight through clouds.
        float4 _Terrain;  // World XZ origin and inverse size.
        float3 _ViewOrigin, _ViewForward, _ViewRight, _ViewUp;
        float2 _ViewOffset;
        float3 _SunDirection, _ScreenSun;

        float2 DepthUV(float2 uv)
        {
            #if UNITY_UV_STARTS_AT_TOP
            if (_MainTex_TexelSize.y < 0) uv.y = 1 - uv.y;
            #endif
            return uv;
        }

        float EyeDepth(float2 uv)
        {
            return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, DepthUV(uv)));
        }

        float TerrainHeight(float2 worldXZ)
        {
            float2 uv = (worldXZ - _Terrain.xy) * _Terrain.zw;
            if (_Terrain.z <= 0 || any(uv < 0) || any(uv > 1)) return _Zone.y;
            uv = uv * (127.0 / 128.0) + .5 / 128.0;
            return tex2Dlod(_Relief, float4(uv, 0, 0)).r;
        }

        // Leave room for tendrils to precede the damage boundary. Clip the ray
        // to that radius before sampling the more irregular moving volume.
        float2 ClearInterval(float3 ray, float distance)
        {
            float innerRadius = _Zone.w + _Weather.w * .23 - 7.5;
            float2 offset = _ViewOrigin.xz - _Zone.xz;
            float a = dot(ray.xz, ray.xz);
            float b = dot(offset, ray.xz);
            float c = dot(offset, offset) - innerRadius * innerRadius;
            if (a < .00001) return c < 0 ? float2(0, distance) : float2(-1, -1);
            float discriminant = b * b - a * c;
            if (discriminant < 0) return -1;
            float extent = sqrt(discriminant);
            return float2(max(0, (-b - extent) / a), min(distance, (-b + extent) / a));
        }

        float3 Smoke(float3 samplePosition, float2 sunXZ)
        {
            float2 radial = samplePosition.xz - _Zone.xz;
            float distance = length(radial);
            float2 outwards = radial / max(1, distance);
            // The side facing sunrise peels back ahead of the shaded side.
            float retreat = _Weather.w * (.62 + .39 * dot(outwards, sunXZ));
            float edge = distance - _Zone.w - retreat;
            float height = max(0, samplePosition.y - TerrainHeight(samplePosition.xz));

            float clock = _Weather.x;
            float2 wind = float2(.22, -.12) * clock;
            // Move the pattern inward during the siege. At dawn it stretches
            // out with the lifting bank, uncovering the terrain beneath it.
            float3 advected = float3(radial.x, height, radial.y);
            advected.xz += wind + outwards * (clock * .29 - retreat * .92);
            float billow = tex3Dlod(_Turbulence, float4(advected * float3(.022, .046, .022) + float3(.12, -clock * .018, .39), 0)).r;
            float curl = tex3Dlod(_Turbulence, float4(advected * float3(.064, .093, .064) - float3(.23, clock * .013, .31), 0)).r;
            float flow = .66 * billow + .34 * curl;
            float brokenEdge = edge + (billow - .5) * 9 + (curl - .5) * 3.8;
            float bank = smoothstep(-2.8, 5.8, brokenEdge);
            float skirt = smoothstep(-5.6, -1, brokenEdge) * smoothstep(.40, .68, curl) * .24;

            // A denser floor layer supports taller broken plumes above it.
            float ground = exp2(-height * .43) * (bank + skirt);
            float crest = lerp(3.6, 13.5, smoothstep(.31, .71, billow)) + smoothstep(1, 22, edge) * 6;
            float plume = 1 - smoothstep(crest * .34, crest, height);
            float thickness = ground * lerp(.062, .31, flow) + plume * bank * smoothstep(.20, .82, flow) * .21;
            return float3(thickness * _Weather.z, flow, height);
        }

        void Integrate(inout float4 fog, float3 ray, float start, float end, float2 sunXZ, float forwardLight)
        {
            float span = min(end - start, 100);
            if (span <= .035 || fog.a >= .992) return;
            const int samples = 14;
            const float stride = 1.0 / samples;
            [loop] for (int i = 0; i < samples; i++)
            {
                float t = (i + .5) * stride;
                float stepSize = span * (.52 + .96 * t) * stride;
                float3 samplePosition = _ViewOrigin + ray * (start + span * t * (.52 + .48 * t));
                float3 smoke = Smoke(samplePosition, sunXZ);
                float alpha = 1 - exp(-smoke.x * stepSize);

                float laced = smoothstep(.24, .79, smoke.y) * (.64 + .36 * saturate(smoke.z * .21));
                float3 umber = lerp(float3(.009, .012, .017), float3(.015, .009, .014), _Weather.y);
                float3 silver = lerp(float3(.067, .084, .104), float3(.082, .054, .064), _Weather.y);
                float3 scattering = lerp(umber, silver, laced);
                scattering += float3(.24, .152, .075) * (_Air.z * (.12 + .88 * forwardLight)) * (.3 + .7 * laced);
                fog.rgb += scattering * alpha * (1 - fog.a);
                fog.a += alpha * (1 - fog.a);
                if (fog.a >= .992) break;
            }
        }

        float4 LiftingWisps(float3 ray, float depth, float forwardLight)
        {
            float travel = min(depth, 41);
            float amount = 0;
            [unroll] for (int i = 0; i < 6; i++)
            {
                float t = (i + .5) / 6;
                float3 samplePosition = _ViewOrigin + ray * (travel * t * t);
                float height = samplePosition.y - TerrainHeight(samplePosition.xz);
                float floorMist = smoothstep(-.4, .1, height) * (1 - smoothstep(.32, 2.75, height));
                float3 wind = float3(_Weather.x * .011, -_Weather.w * .002, _Weather.x * -.008);
                float ragged = tex3Dlod(_Turbulence, float4(samplePosition * float3(.039, .17, .051) + wind, 0)).r;
                amount += floorMist * smoothstep(.34, .72, ragged) * (travel * t / 3);
            }
            float alpha = 1 - exp(-amount * .083 * _Air.z * _Weather.z);
            float3 silver = lerp(float3(.041, .044, .055), float3(.24, .156, .078), forwardLight);
            return float4(silver * alpha, alpha);
        }

        float3 Sunbeams(float2 uv, float3 ray, float distance)
        {
            if (_Air.w < .001 || _ScreenSun.z <= 0) return 0;
            float facing = smoothstep(.60, .997, dot(ray, _SunDirection));
            if (facing <= .001) return 0;
            float2 towardSun = _ScreenSun.xy - uv;
            float visible = 0;
            [unroll] for (int i = 1; i <= 14; i++)
            {
                float2 at = uv + towardSun * (i / 16.0);
                float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, DepthUV(saturate(at))));
                float inView = step(0, at.x) * step(at.x, 1) * step(0, at.y) * step(at.y, 1);
                visible += smoothstep(.90, 1, depth) * inView;
            }
            float light = _Air.w * facing * (visible / 14) * lerp(.04, .26, saturate(distance / 38));
            return float3(1, .63, .31) * light;
        }

        float4 CalculateMist(v2f_img input) : SV_Target
        {
            float2 uv = input.uv;
            float2 screen = DepthUV(uv) * 2 - 1 + _ViewOffset;
            float3 ray = normalize(_ViewForward + screen.x * _ViewRight + screen.y * _ViewUp);
            float depth = min(460, EyeDepth(uv) / max(.1, dot(ray, _ViewForward)));
            float4 fog = 0;
            if (_Weather.z > .001)
            {
                float2 sunXZ = normalize(_SunDirection.xz);
                float forwardLight = pow(saturate(dot(ray, _SunDirection)), 3);
                float2 clear = ClearInterval(ray, depth);
                if (clear.x >= 0 && clear.y > clear.x)
                {
                    Integrate(fog, ray, 0, clear.x, sunXZ, forwardLight);
                    Integrate(fog, ray, clear.y, depth, sunXZ, forwardLight);
                }
                else Integrate(fog, ray, 0, depth, sunXZ, forwardLight);

                if (_Air.z > .001)
                {
                    float4 wisps = LiftingWisps(ray, depth, forwardLight);
                    fog = wisps + fog * (1 - wisps.a);
                }

                // The ceiling is a gameplay boundary. Use local airborne wisps
                // on the high ground without drawing a flat lid across the sky.
                if (_Air.y > .001)
                {
                    float distance = min(depth * .46, 8);
                    float3 samplePosition = _ViewOrigin + ray * distance;
                    float wisp = tex3Dlod(_Turbulence, float4(samplePosition * .063 + float3(_Weather.x * .027, 0, 0), 0)).r;
                    float drift = smoothstep(_Air.x - 1.2, _Air.x + 1.5, samplePosition.y);
                    float opacity = (1 - exp(-min(depth, 21) * .061 * wisp)) * drift * _Air.y * _Weather.z;
                    fog.rgb = lerp(fog.rgb, float3(.025, .022, .033), opacity);
                    fog.a += opacity * (1 - fog.a);
                }
            }
            fog.rgb += Sunbeams(uv, ray, depth);
            return fog;
        }

        float4 CompositeMist(v2f_img input) : SV_Target
        {
            float2 uv = input.uv;
            float2 size = max(1, floor(_MainTex_TexelSize.zw * .5));
            float2 pixel = uv * size - .5;
            float2 cell = floor(pixel);
            float2 blend = frac(pixel);
            float depth = EyeDepth(uv);
            float4 fog = 0;
            float coverage = 0;
            // Reconstruct the half-res volume without mixing distant smoke
            // across the edge of a nearby house, knight or battlement.
            [unroll] for (int y = 0; y < 2; y++)
            [unroll] for (int x = 0; x < 2; x++)
            {
                float2 at = saturate((cell + float2(x, y) + .5) / size);
                float2 spatial = 1 - abs(blend - float2(x, y));
                float difference = abs(depth - EyeDepth(at));
                float weight = spatial.x * spatial.y * rcp(1 + difference * difference / max(.24, depth * depth * .002));
                coverage += weight;
                fog += tex2D(_LastStandFog, at) * weight;
            }
            fog /= max(coverage, .00001);
            float4 scene = tex2D(_MainTex, uv);
            scene.rgb = scene.rgb * (1 - fog.a) + fog.rgb;
            return scene;
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment CalculateMist
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment CompositeMist
            ENDCG
        }
    }
    Fallback Off
}
