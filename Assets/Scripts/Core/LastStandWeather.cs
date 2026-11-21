using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

internal sealed class LastStandWeather : IDisposable
{
    public static readonly Vector3 SunriseDirection = new Vector3(-.77f, .30f, .60f).normalized;
    static readonly Vector3 MoonDirection = new Vector3(.38f, .69f, -.62f).normalized;
    static readonly Color NightFog = new Color(.17f, .205f, .28f);
    static readonly Color StormFog = new Color(.23f, .122f, .155f);
    static readonly Color DawnFog = new Color(.69f, .58f, .43f);

    readonly Camera camera;
    readonly Transform root;
    readonly LastStandMiasma miasma;
    readonly Material originalSky, sky;
    readonly int sceneHandle;
    readonly bool originalFog;
    readonly FogMode originalFogMode;
    readonly float originalFogDensity, originalAmbient, originalReflection;
    readonly Color originalFogColor, originalSkyColor, originalEquator, originalGround;
    readonly Light sun;
    readonly Quaternion originalSunRotation;
    readonly Color originalSunColor;
    readonly float originalSunIntensity;
    readonly List<(Light light, float intensity)> torches = new List<(Light, float)>();
    readonly List<SkyDrift> drifts = new List<SkyDrift>();
    readonly ParticleSystem rain;
    readonly LineRenderer[] bolt = new LineRenderer[3];
    readonly Light strikeLight;
    readonly float skyRotation;

    Vector3 center, playerPosition;
    float radius, ceiling, clock, nextStrike = LastStandDirector.Duration - LastStandDirector.StormSeconds;
    float strikeAt = -100f;
    bool rainPlaying;

    public LastStandWeather(Transform parent, Camera view)
    {
        camera = view;
        sceneHandle = SceneManager.GetActiveScene().handle;
        root = new GameObject("Last Stand weather").transform;
        root.SetParent(parent, false);
        originalSky = RenderSettings.skybox;
        originalFog = RenderSettings.fog;
        originalFogMode = RenderSettings.fogMode;
        originalFogDensity = RenderSettings.fogDensity;
        originalFogColor = RenderSettings.fogColor;
        originalAmbient = RenderSettings.ambientIntensity;
        originalSkyColor = RenderSettings.ambientSkyColor;
        originalEquator = RenderSettings.ambientEquatorColor;
        originalGround = RenderSettings.ambientGroundColor;
        originalReflection = RenderSettings.reflectionIntensity;
        sun = RenderSettings.sun;
        if (sun != null)
        {
            originalSunColor = sun.color;
            originalSunIntensity = sun.intensity;
            originalSunRotation = sun.transform.rotation;
        }

        var skyShader = Resources.Load<Shader>("LastStand/Sky");
        if (skyShader != null)
        {
            foreach (var drift in UnityEngine.Object.FindObjectsByType<SkyDrift>(FindObjectsSortMode.None))
            {
                if (!drift.enabled || drift.gameObject.scene.handle != sceneHandle) continue;
                drifts.Add(drift);
                drift.enabled = false;
            }
            sky = new Material(skyShader) { name = "Last Stand sky" };
            if (originalSky != null && originalSky.HasProperty("_Tex")) sky.SetTexture("_Tex", originalSky.GetTexture("_Tex"));
            if (originalSky != null && originalSky.HasProperty("_Rotation")) skyRotation = originalSky.GetFloat("_Rotation");
            RenderSettings.skybox = sky;
        }
        foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.gameObject.scene.name == "Background" && light.type == LightType.Point && light.color.r > light.color.b * 1.4f)
                torches.Add((light, light.intensity));
        }
        if (camera != null) miasma = camera.gameObject.AddComponent<LastStandMiasma>();

        Material particle = Resources.Load<Material>("FX/Particle");
        rain = CreateRain(particle);
        for (int i = 0; i < bolt.Length; i++)
        {
            bolt[i] = new GameObject(i == 0 ? "Lightning strike" : "Lightning fork").AddComponent<LineRenderer>();
            bolt[i].transform.SetParent(root, false);
            bolt[i].sharedMaterial = particle;
            bolt[i].shadowCastingMode = ShadowCastingMode.Off;
            bolt[i].receiveShadows = false;
            bolt[i].startWidth = i == 0 ? .35f : .17f;
            bolt[i].endWidth = i == 0 ? .19f : .02f;
            bolt[i].enabled = false;
        }
        strikeLight = new GameObject("Lightning illumination").AddComponent<Light>();
        strikeLight.transform.SetParent(root, false);
        strikeLight.color = new Color(.82f, .62f, 1f);
        strikeLight.range = 28f;
        strikeLight.enabled = false;
    }

    public void Tick(Vector3 focus, float front, float height, Vector3 player, float elapsed)
    {
        center = focus;
        radius = front;
        ceiling = height;
        playerPosition = player;
        clock = elapsed;
        float stormStart = LastStandDirector.Duration - LastStandDirector.StormSeconds;
        float storm = Mathf.SmoothStep(0f, 1f, (elapsed - stormStart) / 2.8f);
        if (elapsed >= nextStrike)
        {
            Strike(player);
            nextStrike = elapsed + Random.Range(12f, 19f);
            strikeAt = elapsed;
        }
        rain.transform.position = player + Vector3.up * 15f;
        Apply(storm, Mathf.Exp(-8f * (elapsed - strikeAt)));
    }

    public void Dawn(float elapsed)
    {
        clock = LastStandDirector.Duration + elapsed;
        Apply(1f, Mathf.Exp(-8f * (clock - strikeAt)), elapsed);
    }

    static float Ramp(float time, float start, float end) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, time));

    void Apply(float storm, float flash, float dawnTime = -1f)
    {
        // Let the horizon warm first. The cloud opening, rain and ground bank
        // have separate fades so the whole scene never changes at once.
        float dawn = Ramp(dawnTime, 1.1f, 8.65f);
        float cloudBreak = Ramp(dawnTime, .48f, 7.3f);
        float clearing = Ramp(dawnTime, 1.25f, 8.9f);
        float retreat = 95f * Ramp(dawnTime, 1.15f, 8.75f);
        float haze = cloudBreak * Mathf.Lerp(1f, .17f, Ramp(dawnTime, 5.65f, 9.25f));

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Lerp(Mathf.Lerp(.0068f, .0098f, storm), .0014f, clearing);
        RenderSettings.fogColor = Color.Lerp(Color.Lerp(NightFog, StormFog, storm), DawnFog, dawn);
        RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(new Color(.45f, .53f, .72f), new Color(.54f, .37f, .48f), storm),
            new Color(.76f, .66f, .63f), dawn);
        RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.34f, .37f, .49f), new Color(.67f, .55f, .43f), dawn);
        RenderSettings.ambientGroundColor = Color.Lerp(new Color(.24f, .25f, .34f), new Color(.45f, .34f, .27f), dawn);
        RenderSettings.ambientIntensity = Mathf.Lerp(.96f, 1.15f, dawn);
        RenderSettings.reflectionIntensity = Mathf.Lerp(.29f, .65f, dawn);

        Vector3 sunrise = Vector3.Slerp(new Vector3(SunriseDirection.x, .025f, SunriseDirection.z).normalized,
            SunriseDirection, Ramp(dawnTime, .2f, 8.6f));
        if (sun != null)
        {
            // Move the directional key while it is dim. This keeps the keep's
            // shadows from swinging across the courtyard at full brightness.
            Vector3 lightDirection = Vector3.Slerp(MoonDirection, sunrise, Ramp(dawnTime, 1.5f, 3.28f));
            sun.transform.rotation = Quaternion.LookRotation(-lightDirection);
            Color key = Color.Lerp(new Color(.58f, .71f, 1f), new Color(.93f, .52f, .58f), storm);
            sun.color = Color.Lerp(Color.Lerp(key, new Color(1f, .78f, .49f), cloudBreak), new Color(.81f, .76f, 1f), flash);
            float moon = Mathf.Lerp(.5f, .39f, storm) * (1f - Ramp(dawnTime, .55f, 2.35f));
            float daylight = Mathf.Lerp(.06f, 1.68f, Ramp(dawnTime, 2.7f, 8.16f));
            sun.intensity = moon + daylight * cloudBreak + flash * 1.18f;
        }
        if (sky != null)
        {
            sky.SetFloat("_Rotation", skyRotation + clock * .19f);
            sky.SetFloat("_SkyTime", clock);
            sky.SetVector("_SunDirection", sunrise);
            sky.SetVector("_Weather", new Vector4(storm, dawn, cloudBreak, flash));
        }
        if (miasma != null)
        {
            float highGround = Mathf.SmoothStep(0f, 1f, (playerPosition.y - ceiling + .75f) / 2.5f);
            float remainingMist = 1f - clearing;
            miasma.SetWeather(center, radius, clock, storm, remainingMist, retreat, ceiling, highGround,
                haze, cloudBreak * (.28f + haze * .96f), sunrise);
        }
        for (int i = 0; i < torches.Count; i++)
        {
            var torch = torches[i];
            if (torch.light == null) continue;
            float flicker = Mathf.Lerp(.68f, 1.24f, Mathf.PerlinNoise(i * 1.71f, clock * 7.4f));
            torch.light.intensity = torch.intensity * Mathf.Lerp(flicker * 1.45f, 1f, dawn);
        }

        bool lightning = flash > .035f;
        strikeLight.enabled = lightning;
        strikeLight.intensity = flash * 7f;
        foreach (var line in bolt)
        {
            line.enabled = lightning;
            line.startColor = new Color(.82f, .7f, 1f, Mathf.Clamp01(flash * 2f));
            line.endColor = new Color(1f, .43f, .44f, flash);
        }

        float rainRate = storm * (1f - Ramp(dawnTime, .25f, 5.8f));
        var emission = rain.emission;
        emission.rateOverTime = 940f * rainRate;
        if (rainRate > .01f && !rainPlaying) { rain.Play(); rainPlaying = true; }
        if (rainRate <= .01f && rainPlaying)
        {
            rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            rainPlaying = false;
        }
    }

    ParticleSystem CreateRain(Material material)
    {
        var ps = new GameObject("Blood rain").AddComponent<ParticleSystem>();
        ps.transform.SetParent(root, false);
        ps.transform.rotation = Quaternion.LookRotation(new Vector3(.16f, -1f, .1f), Vector3.forward);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = new ParticleSystem.MinMaxCurve(23f, 31f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(.58f, .93f);
        main.startSize = new ParticleSystem.MinMaxCurve(.035f, .061f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(.55f, .05f, .12f, .62f), new Color(.9f, .21f, .27f, .82f));
        main.maxParticles = 1200;
        var fading = ps.colorOverLifetime;
        fading.enabled = true;
        fading.color = new Gradient
        {
            alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f),
                new GradientAlphaKey(1f, .64f), new GradientAlphaKey(0f, 1f) }
        };
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(39f, 39f, 1.8f);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 10f;
        renderer.velocityScale = .019f;
        return ps;
    }

    void Strike(Vector3 player)
    {
        Vector3 facing = camera != null ? camera.transform.forward : Vector3.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < .01f) facing = Vector3.forward;
        float bearing = Random.Range(20f, 33f) * (Random.value < .5f ? -1f : 1f);
        Vector3 ground = player + Quaternion.Euler(0f, bearing, 0f) * facing.normalized * Random.Range(29f, 48f);
        if (Physics.Raycast(ground + Vector3.up * 80f, Vector3.down, out var hit, 110f, ~0, QueryTriggerInteraction.Ignore))
            ground = hit.point;

        Vector3 skyPoint = ground + new Vector3(Random.Range(-12f, 12f), 63f, Random.Range(-10f, 10f));
        var trunk = new Vector3[13];
        for (int i = 0; i < trunk.Length; i++)
        {
            float t = i / (trunk.Length - 1f);
            trunk[i] = Vector3.Lerp(skyPoint, ground, t);
            if (i > 0 && i < trunk.Length - 1) trunk[i] += Random.insideUnitSphere * 3.7f;
        }
        bolt[0].positionCount = trunk.Length;
        bolt[0].SetPositions(trunk);
        for (int i = 1; i < bolt.Length; i++)
        {
            Vector3 from = trunk[2 + 2 * i];
            Vector3 end = from + new Vector3((i == 1 ? -1f : 1f) * Random.Range(7f, 13f), -16f, Random.Range(-5f, 5f));
            bolt[i].positionCount = 5;
            for (int j = 0; j < 5; j++)
                bolt[i].SetPosition(j, Vector3.Lerp(from, end, j / 4f) + (j > 0 ? Random.insideUnitSphere * 2.5f : Vector3.zero));
        }
        strikeLight.transform.position = ground + Vector3.up * 3f;
        AudioManager.PlayAt("last_stand_thunder", strikeLight.transform.position, .95f, .06f, 1f, 13500f, 36, .63f, 27f, 130f);
        FX.Shake(.22f);
    }

    public void Dispose()
    {
        foreach (var torch in torches)
            if (torch.light != null) torch.light.intensity = torch.intensity;
        if (sun != null)
        {
            sun.color = originalSunColor;
            sun.intensity = originalSunIntensity;
            sun.transform.rotation = originalSunRotation;
        }
        if (SceneManager.GetActiveScene().handle == sceneHandle)
        {
            if (sky != null) RenderSettings.skybox = originalSky;
            RenderSettings.fog = originalFog;
            RenderSettings.fogMode = originalFogMode;
            RenderSettings.fogDensity = originalFogDensity;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.ambientIntensity = originalAmbient;
            RenderSettings.ambientSkyColor = originalSkyColor;
            RenderSettings.ambientEquatorColor = originalEquator;
            RenderSettings.ambientGroundColor = originalGround;
            RenderSettings.reflectionIntensity = originalReflection;
        }
        foreach (var drift in drifts)
            if (drift != null) drift.enabled = true;
        if (miasma != null) UnityEngine.Object.Destroy(miasma);
        if (sky != null) UnityEngine.Object.Destroy(sky);
        if (root != null) UnityEngine.Object.Destroy(root.gameObject);
    }
}
