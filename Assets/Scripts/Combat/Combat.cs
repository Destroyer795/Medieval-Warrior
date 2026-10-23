using UnityEngine;

public static class Combat
{
    // Area damage: hurts the knight AND other zombies (lure Bloaters into crowds).
    public static void Explode(Vector3 center, float radius, float damage, EnemyController source, Color tint)
    {
        FX.Explosion(center, radius * 0.5f, tint);
        AudioManager.PlayAt("explosion", center, 1f, 0.1f);

        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            float d = Vector3.Distance(player.transform.position + Vector3.up, center);
            if (d < radius)
            {
                var ph = player.GetComponent<PlayerHealth>();
                float falloff = 1f - Mathf.Clamp01(d / radius) * 0.6f;
                if (ph != null) ph.TakeDamage(damage * falloff, source != null ? source.transform : null, 10f * falloff, true);
            }
            FX.Shake(Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(d / (radius * 4f))));
        }

        // copy: damage can kill zombies, which removes them from the list
        var zombies = EnemyController.All.ToArray();
        int killed = 0;
        foreach (var z in zombies)
        {
            if (z == null || z == source || z.IsDead) continue;
            Vector3 to = z.transform.position + Vector3.up - center;
            if (to.magnitude > radius) continue;
            float falloff = 1f - Mathf.Clamp01(to.magnitude / radius) * 0.5f;
            z.TakeDamage(damage * 0.25f * falloff, to.normalized, 9f * falloff);
            if (z.IsDead) killed++;
            if (GameManager.instance != null) GameManager.instance.RegisterExplosionHit(z);
        }
        if (killed >= 3 && source != null && source.type == ZombieType.Bloater) Feats.Unlock("chain_reaction");
    }

    public static Material ResourceMaterial(string name, Color fallback)
    {
        var m = Resources.Load<Material>("FX/" + name);
        if (m != null) return m;
        m = new Material(Shader.Find("Standard")) { color = fallback };
        return m;
    }
}
