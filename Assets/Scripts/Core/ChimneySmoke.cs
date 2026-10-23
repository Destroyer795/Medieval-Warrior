using UnityEngine;

// Lazy wisps of smoke from a village chimney.
public class ChimneySmoke : MonoBehaviour
{
    void Start()
    {
        var ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startColor = new Color(0.82f, 0.8f, 0.78f, 0.32f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.prewarm = true;
        var em = ps.emission; em.rateOverTime = 4f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8f; shape.radius = 0.2f;
        transform.rotation = Quaternion.Euler(-90, 0, 0);
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.35f, 0.7f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 3.2f));
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.75f, 0.72f, 0.7f), 0), new GradientColorKey(new Color(0.9f, 0.9f, 0.9f), 1) },
                  new[] { new GradientAlphaKey(0f, 0), new GradientAlphaKey(0.35f, 0.15f), new GradientAlphaKey(0f, 1) });
        col.color = g;
        var r = GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Resources.Load<Material>("FX/Particle");
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }
}
