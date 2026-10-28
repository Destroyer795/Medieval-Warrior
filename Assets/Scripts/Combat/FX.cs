using System.Collections;
using UnityEngine;

// One-shot particle effects, built in code from a single soft-particle material (Resources/FX/Particle).
public static class FX
{
    static Material particleMat;
    static MonoBehaviour runner;

    static Material ParticleMat
    {
        get
        {
            if (particleMat == null)
            {
                particleMat = Resources.Load<Material>("FX/Particle");
                if (particleMat == null)
                {
                    var sh = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
                    particleMat = new Material(sh) { mainTexture = Resources.Load<Texture2D>("UI/glow") };
                }
            }
            return particleMat;
        }
    }

    static ParticleSystem Make(string name, Vector3 pos, float lifetime, float maxLifetime)
    {
        var go = new GameObject("FX " + name);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = lifetime;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = ParticleMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Object.Destroy(go, maxLifetime + lifetime + 0.2f);
        return ps;
    }

    static void Burst(ParticleSystem ps, int count)
    {
        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        ps.Play();
    }

    static void Gradient(ParticleSystem ps, Color a, Color b)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                  new[] { new GradientAlphaKey(a.a, 0), new GradientAlphaKey(a.a * 0.8f, 0.6f), new GradientAlphaKey(0, 1) });
        col.color = g;
    }

    public static void Blood(Vector3 pos, Vector3 dir, float amount = 1f, bool zombie = true)
    {
        var ps = Make("Blood", pos, 0.1f, 0.9f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.gravityModifier = 1.6f;
        Color c = zombie ? new Color(0.35f, 0.42f, 0.08f, 1f) : new Color(0.6f, 0.03f, 0.03f, 1f);
        main.startColor = c;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.05f;
        ps.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.01f ? dir : Vector3.up);
        Gradient(ps, c, c * 0.5f);
        Burst(ps, Mathf.RoundToInt(14 * amount));
    }

    public static void Sparks(Vector3 pos, Vector3 normal)
    {
        var ps = Make("Sparks", pos, 0.1f, 0.5f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        main.gravityModifier = 1f;
        main.startColor = new Color(1f, 0.85f, 0.4f);
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 40f;
        ps.transform.rotation = Quaternion.LookRotation(normal);
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 1.5f;
        Gradient(ps, new Color(1f, 0.9f, 0.5f, 1f), new Color(1f, 0.3f, 0.05f, 1f));
        Burst(ps, 18);
    }

    public static void Dust(Vector3 pos, float size = 1f, int count = 12, Color? color = null)
    {
        var ps = Make("Dust", pos, 0.1f, 1.4f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * size, 2.2f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f * size, 1.1f * size);
        main.gravityModifier = -0.05f;
        Color c = color ?? new Color(0.62f, 0.5f, 0.36f, 0.55f);
        main.startColor = c;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.3f * size;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.6f));
        Gradient(ps, c, new Color(c.r, c.g, c.b, 0f));
        Burst(ps, count);
    }

    public static void Explosion(Vector3 pos, float radius, Color tint)
    {
        var ps = Make("Explosion", pos, 0.1f, 1.6f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.5f, radius * 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.4f, radius * 0.9f);
        main.gravityModifier = 0.3f;
        main.startColor = tint;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.3f;
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.dampen = 0.25f; lim.limit = 1f;
        Gradient(ps, tint, new Color(0.25f, 0.25f, 0.2f, 0f));
        Burst(ps, 45);
        Dust(pos, radius * 0.6f, 16, new Color(0.35f, 0.4f, 0.2f, 0.6f));
        var lightGo = new GameObject("Flash");
        lightGo.transform.position = pos + Vector3.up;
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point; l.color = tint; l.range = radius * 4f; l.intensity = 6f;
        Object.Destroy(lightGo, 0.15f);
    }

    public static void Sparkle(Vector3 pos, Color color, int count = 16)
    {
        var ps = Make("Sparkle", pos, 0.1f, 1.2f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.gravityModifier = -0.4f;
        main.startColor = color;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.5f;
        ps.transform.rotation = Quaternion.Euler(-90, 0, 0);
        Gradient(ps, color, new Color(color.r, color.g, color.b, 0));
        Burst(ps, count);
    }

    public static void Embers(Vector3 pos, int count = 6)
    {
        var ps = Make("Embers", pos, 0.1f, 1f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.gravityModifier = -0.6f;
        main.startColor = new Color(1f, 0.55f, 0.12f, 0.95f);
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.25f;
        Gradient(ps, new Color(1f, 0.75f, 0.25f, 1f), new Color(0.6f, 0.1f, 0.02f, 0f));
        Burst(ps, count);
    }

    // A pale crescent that traces each sword swing; the combo step decides the angle.
    static Mesh swipeMesh;
    static readonly MaterialPropertyBlock swipeBlock = new MaterialPropertyBlock();

    public static void Swipe(Transform owner, int step)
    {
        if (swipeMesh == null) swipeMesh = BuildSwipeMesh();
        var go = new GameObject("FX Swipe");
        go.transform.SetParent(owner, false);
        go.transform.localPosition = new Vector3(0, 1.15f, 0.35f);
        float roll = step == 0 ? 20f : step == 1 ? 160f : 90f;   // right-to-left, back-hand, overhead
        go.transform.localRotation = Quaternion.Euler(0, 0, roll);
        go.AddComponent<MeshFilter>().sharedMesh = swipeMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ParticleMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        EnsureRunner();
        runner.StartCoroutine(FadeSwipe(go, mr, step == 2));
    }

    static IEnumerator FadeSwipe(GameObject go, MeshRenderer mr, bool heavy)
    {
        float life = heavy ? 0.18f : 0.14f;
        for (float t = 0; t < life && go != null; t += Time.deltaTime)
        {
            float k = t / life;
            float opacity = 1f - Mathf.SmoothStep(0f, 1f, k);
            swipeBlock.SetColor("_TintColor", new Color(1f, 0.95f, 0.85f, opacity * (heavy ? 0.9f : 0.75f)));
            swipeBlock.SetColor("_Color", new Color(1f, 0.95f, 0.85f, opacity * 0.75f));
            mr.SetPropertyBlock(swipeBlock);
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 0.97f, k);
            yield return null;
        }
        if (go != null) Object.Destroy(go);
    }

    static Mesh BuildSwipeMesh()
    {
        // ring sector in the local XY plane, bright in the middle of its width, fading at both ends
        const int segs = 18;
        var v = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var col = new System.Collections.Generic.List<Color>();
        var tri = new System.Collections.Generic.List<int>();
        for (int i = 0; i <= segs; i++)
        {
            float t = i / (float)segs;
            float a = Mathf.Lerp(-75f, 75f, t) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(a), Mathf.Cos(a) * 0.35f, Mathf.Cos(a));
            float fade = Mathf.Sin(t * Mathf.PI);
            foreach (var (r, vcoord) in new[] { (0.85f, 0f), (1.12f, 0.5f), (1.35f, 1f) })
            {
                v.Add(dir.normalized * r);
                uv.Add(new Vector2(0.5f, vcoord));
                col.Add(new Color(1, 1, 1, fade));
            }
        }
        for (int i = 0; i < segs; i++)
            for (int j = 0; j < 2; j++)
            {
                int a = i * 3 + j, b = a + 1, c = a + 3, d = a + 4;
                tri.AddRange(new[] { a, c, b, b, c, d, a, b, c, b, d, c });   // double sided
            }
        var m = new Mesh { name = "Swipe" };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    // ------------------------------------------------------------ game feel

    static Coroutine hitStop;

    public static void HitStop(float seconds)
    {
        if (Time.timeScale < 0.5f) return; // paused or already frozen
        EnsureRunner();
        if (hitStop != null) runner.StopCoroutine(hitStop);
        hitStop = runner.StartCoroutine(HitStopRoutine(seconds));
    }

    static IEnumerator HitStopRoutine(float seconds)
    {
        Time.timeScale = 0.06f;
        yield return new WaitForSecondsRealtime(seconds);
        if (Time.timeScale > 0f) Time.timeScale = 1f;
        hitStop = null;
    }

    public static void Shake(float amount)
    {
        if (!GameSettings.CameraShake) return;
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (cam != null) cam.AddShake(amount);
    }

    static void EnsureRunner()
    {
        if (runner != null) return;
        var go = new GameObject("FX Runner");
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<FXRunner>();
    }

    class FXRunner : MonoBehaviour { }
}
