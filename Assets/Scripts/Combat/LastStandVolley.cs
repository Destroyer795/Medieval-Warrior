using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Each aimed shot resolves against its assigned enemy when the fire reaches it.
// The rest of the arrows and their impacts stage the volley around the knight.
internal sealed class LastStandVolley
{
    const float ArrowLength = .9f;
    const float FlashLife = .58f;

    sealed class Shot
    {
        public EnemyController target;
        public Vector3 origin, impact, normal;
        public float launch, flight, arc, width, nextSpark;
        public GameObject arrow;
        public ParticleSystem flame;
        public TrailRenderer trail;
        public bool landed;
    }

    sealed class ImpactLight
    {
        public Light light;
        public float start, strength;
    }

    readonly List<Shot> shots = new List<Shot>();
    readonly Transform root;
    readonly Material trailMaterial;
    readonly Gradient trailGradient = new Gradient();
    readonly AnimationCurve trailWidth = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.44f, .7f), new Keyframe(1f, 0f));
    readonly ParticleSystem sparks, ash;
    readonly ImpactLight[] impactLights = new ImpactLight[3];
    readonly Vector3 knight;
    float nextImpactSound, nextReleaseSound, nextShake;
    int nextLight;

    public LastStandVolley(Transform parent, Vector3 player, Camera camera)
    {
        knight = player;
        root = new GameObject("Royal arrow volley").transform;
        root.SetParent(parent, false);
        var particleMaterial = Resources.Load<Material>("FX/Particle");
        trailMaterial = Resources.Load<Material>("LastStand/RoyalTrail");
        var sparkMaterial = Resources.Load<Material>("LastStand/RoyalSparks");
        sparks = MakeEmitter("Royal arrow sparks", sparkMaterial != null ? sparkMaterial : particleMaterial, 1200, .74f, true);
        ash = MakeEmitter("Royal arrow smoke", particleMaterial, 180, -.08f, false);
        trailGradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, .95f, .68f), 0f),
                new GradientColorKey(new Color(1f, .44f, .09f), .32f), new GradientColorKey(new Color(.84f, .13f, .025f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.62f, .32f),
                new GradientAlphaKey(.20f, .76f), new GradientAlphaKey(0f, 1f) });

        for (int i = 0; i < impactLights.Length; i++)
        {
            var light = new GameObject("Royal arrow impact light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.color = new Color(1f, .44f, .16f);
            light.range = 6.5f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            impactLights[i] = new ImpactLight { light = light };
        }

        foreach (var enemy in EnemyController.All)
        {
            if (enemy == null || enemy.IsDead) continue;
            enemy.AwaitRoyalVolley();
            Vector3 chest = Chest(enemy);
            float arrival = Mathf.Lerp(2.5f, 4.85f, Mathf.InverseLerp(3f, 36f, Vector3.Distance(player, chest)));
            AddShot(chest, Vector3.up, enemy, arrival + Random.Range(-.23f, .36f));
        }

        Vector3 forward = camera != null ? camera.transform.forward : Vector3.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > .01f ? forward.normalized : Vector3.forward;
        // Three staggered ranks sweep past from the hills. Space the impacts
        // across the view, with the first rank landing nearest the knight.
        const int perRank = 11;
        for (int rank = 0; rank < 3; rank++)
        for (int i = 0; i < perRank; i++)
        {
            float sweep = i / (perRank - 1f);
            if (rank == 1) sweep = 1f - sweep;
            float angle = Mathf.Lerp(-74f, 74f, sweep) + Random.Range(-8f, 8f);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * forward;
            float distance = Random.Range(4.0f + rank * 2.7f, 11.5f + rank * 3.1f);
            Vector3 above = player + direction * distance + Vector3.up * 36f;
            if (!Physics.Raycast(above, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore)) continue;

            float arrival = 2.25f + rank * .88f + sweep * .63f + Random.Range(-.09f, .12f);
            AddShot(hit.point + hit.normal * .025f, hit.normal, null, arrival);
        }
    }

    static Vector3 Chest(EnemyController target) => target.transform.position
        + Vector3.up * (1.2f * target.transform.localScale.y);

    void AddShot(Vector3 impact, Vector3 normal, EnemyController target, float arrival)
    {
        Vector3 hill = LastStandWeather.SunriseDirection;
        hill.y = 0f;
        Vector3 approach = Quaternion.Euler(0f, Random.Range(-18f, 18f), 0f) * hill.normalized;
        float flight = Random.Range(1.16f, 1.53f);
        shots.Add(new Shot
        {
            target = target,
            impact = impact,
            normal = normal,
            origin = impact + approach * Random.Range(15f, 29f) + Vector3.up * Random.Range(17f, 25f),
            launch = arrival - flight,
            flight = flight,
            arc = Random.Range(3.1f, 5.4f),
            width = Random.Range(.22f, .43f)
        });
    }

    public void Tick(float elapsed)
    {
        foreach (var flash in impactLights)
        {
            if (!flash.light.enabled) continue;
            float remaining = 1f - Mathf.Clamp01((elapsed - flash.start) / FlashLife);
            flash.light.intensity = flash.strength * remaining * remaining;
            flash.light.enabled = remaining > 0f;
        }

        foreach (var shot in shots)
        {
            if (shot.landed || elapsed < shot.launch) continue;
            float progress = Mathf.Clamp01((elapsed - shot.launch) / shot.flight);
            if (shot.target != null && !shot.target.IsDead) shot.impact = Chest(shot.target);

            float curvature = shot.arc * 4f;
            Vector3 position = Vector3.Lerp(shot.origin, shot.impact, progress)
                + Vector3.up * (curvature * progress * (1f - progress));
            Vector3 tangent = shot.impact - shot.origin + Vector3.up * (curvature * (1f - 2f * progress));
            Vector3 direction = tangent.normalized;
            Quaternion rotation = Quaternion.LookRotation(direction);
            Vector3 hilt = position - direction * ArrowLength;
            if (shot.arrow == null) Launch(shot, hilt, rotation, elapsed);
            else shot.arrow.transform.SetPositionAndRotation(hilt, rotation);

            if (progress >= 1f) Land(shot, direction, elapsed);
            else if (elapsed >= shot.nextSpark)
            {
                shot.nextSpark = elapsed + Random.Range(.04f, .09f);
                EmitSpark(position, -direction * Random.Range(1.4f, 4.3f) + Random.insideUnitSphere * 1.7f, .15f, .36f, .04f, .1f);
            }
        }
    }

    void Launch(Shot shot, Vector3 position, Quaternion rotation, float elapsed)
    {
        shot.arrow = Arrow.Model();
        shot.arrow.name = "Royal flaming arrow";
        shot.arrow.transform.SetParent(root, false);
        shot.arrow.transform.SetPositionAndRotation(position, rotation);
        var flaringTip = FX.AttachFlame(shot.arrow.transform, Vector3.forward * ArrowLength, 1.2f);
        shot.flame = flaringTip.GetComponent<ParticleSystem>();

        var wake = new GameObject("Royal flame wake");
        wake.transform.SetParent(shot.arrow.transform, false);
        wake.transform.localPosition = Vector3.forward * ArrowLength;
        shot.trail = wake.AddComponent<TrailRenderer>();
        if (trailMaterial != null) shot.trail.sharedMaterial = trailMaterial;
        shot.trail.shadowCastingMode = ShadowCastingMode.Off;
        shot.trail.receiveShadows = false;
        shot.trail.time = .14f;
        shot.trail.minVertexDistance = .045f;
        shot.trail.widthCurve = trailWidth;
        shot.trail.widthMultiplier = shot.width;
        shot.trail.colorGradient = trailGradient;
        shot.trail.textureMode = LineTextureMode.Stretch;
        shot.trail.numCornerVertices = 2;

        if (elapsed < nextReleaseSound) return;
        nextReleaseSound = elapsed + .44f;
        AudioManager.PlayAt("bow_release", position, .7f, .06f, .83f, 7800f, 55, .72f, 17f, 115f);
    }

    void Land(Shot shot, Vector3 direction, float elapsed)
    {
        shot.landed = true;
        shot.trail.emitting = false;
        shot.flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        bool struckEnemy = shot.target != null && !shot.target.IsDead;
        if (struckEnemy)
        {
            shot.target.FallToRoyalVolley(direction);
            Object.Destroy(shot.arrow, .35f);
        }

        Vector3 bounce = Vector3.Reflect(direction, shot.normal);
        var at = shot.impact + shot.normal * .04f;
        int burst = struckEnemy ? 20 : 15;
        for (int i = 0; i < burst; i++)
        {
            Vector3 scatter = (bounce * .7f + Random.insideUnitSphere).normalized * Random.Range(2.7f, 9f) + Vector3.up;
            EmitSpark(at, scatter, .25f, .67f, .05f, .15f);
        }
        for (int i = 0; i < 5; i++)
        {
            ash.Emit(new ParticleSystem.EmitParams
            {
                position = at,
                velocity = Vector3.up * Random.Range(.7f, 1.9f) + Random.insideUnitSphere * .7f,
                startSize = Random.Range(.23f, .63f),
                startColor = new Color(.25f, .17f, .15f, Random.Range(.17f, .34f)),
                startLifetime = Random.Range(.55f, 1.05f)
            }, 1);
        }

        float distance = Vector3.Distance(at, knight);
        if (distance < 18f)
        {
            var flash = impactLights[nextLight++ % impactLights.Length];
            flash.light.transform.position = at + shot.normal * .42f;
            flash.start = elapsed;
            flash.strength = Mathf.Lerp(3.6f, 1.45f, Mathf.InverseLerp(3f, 18f, distance));
            flash.light.intensity = flash.strength;
            flash.light.enabled = true;
            if (distance < 6f && elapsed >= nextShake)
            {
                nextShake = elapsed + .5f;
                FX.Shake(.14f);
            }
        }

        if (elapsed < nextImpactSound) return;
        nextImpactSound = elapsed + .12f;
        AudioManager.PlayAt(struckEnemy ? "arrow_flesh" : "arrow_hit", at, .7f,
            .08f, .85f, 14000f, 51);
    }

    void EmitSpark(Vector3 position, Vector3 velocity, float minLife, float maxLife, float minSize, float maxSize)
    {
        sparks.Emit(new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startSize = Random.Range(minSize, maxSize),
            startColor = Color.Lerp(new Color(1f, .38f, .08f), new Color(1f, .83f, .35f), Random.value),
            startLifetime = Random.Range(minLife, maxLife)
        }, 1);
    }

    ParticleSystem MakeEmitter(string name, Material material, int capacity, float gravity, bool stretch)
    {
        var emitter = new GameObject(name).AddComponent<ParticleSystem>();
        emitter.transform.SetParent(root, false);
        emitter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = emitter.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.maxParticles = capacity;

        var shape = emitter.shape;
        shape.enabled = false;
        var emission = emitter.emission;
        emission.enabled = false;
        var colors = emitter.colorOverLifetime;
        colors.enabled = true;
        colors.color = new Gradient
        {
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.7f, .45f), new GradientAlphaKey(0f, 1f) }
        };
        var growth = emitter.sizeOverLifetime;
        growth.enabled = true;
        growth.size = new ParticleSystem.MinMaxCurve(1f,
            stretch ? AnimationCurve.Linear(0f, 1f, 1f, .22f) : AnimationCurve.Linear(0f, 1f, 1f, 2.3f));

        var renderer = emitter.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (stretch)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3.1f;
            renderer.velocityScale = .045f;
        }
        emitter.Play();
        return emitter;
    }
}
