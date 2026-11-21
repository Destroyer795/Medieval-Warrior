using UnityEngine;
using UnityEngine.AI;

public sealed class ExplosiveBarrel : MonoBehaviour
{
    const float Radius = 5.5f;
    static Mesh mesh;
    bool spent;

    internal static void Place(Transform parent, Vector3 position)
    {
        if (mesh == null)
        {
            var kit = new ProceduralModels.MeshKit(3);
            // Swelled red staves, dark forged hoops and a pitch-soaked fuse.
            kit.Tube(0, Vector3.up * .05f, Vector3.up * .57f, .35f, .45f, 14);
            kit.Tube(0, Vector3.up * .57f, Vector3.up * 1.09f, .45f, .35f, 14);
            foreach (float h in new[] { .18f, .55f, .91f })
            {
                float radius = .465f - Mathf.Abs(.55f - h) * .18f;
                kit.Tube(1, Vector3.up * h, Vector3.up * (h + .065f), radius, radius, 14);
            }
            kit.Tube(2, Vector3.up * 1.08f, new Vector3(.12f, 1.28f, 0), .035f, .022f, 6);
            mesh = kit.Build("Banded pitch barrel");
        }
        var go = ProceduralModels.Spawn("Red pitch barrel", mesh, new[] {
            ProceduralModels.Mat("Oxide red staves", new Color(.60f,.085f,.04f)),
            ProceduralModels.Mat("Pitch barrel hoops", new Color(.16f,.15f,.13f), .36f, .62f),
            ProceduralModels.Mat("Tarred fuse", new Color(.055f,.04f,.03f))
        }, parent);
        go.transform.position = position;
        var hull = go.AddComponent<CapsuleCollider>();
        hull.center = Vector3.up * .56f; hull.height = 1.12f; hull.radius = .43f;
        var obstacle = go.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.center = hull.center; obstacle.radius = hull.radius; obstacle.height = hull.height;
        go.AddComponent<ExplosiveBarrel>();
    }

    public void Detonate()
    {
        var gm = GameManager.instance;
        if (spent || gm == null || gm.State != GameManager.MatchState.Playing) return;
        spent = true;
        gameObject.SetActive(false);
        Vector3 center = transform.position + Vector3.up * .6f;
        FX.Explosion(center, 2.8f, new Color(1f, .38f, .06f));
        AudioManager.PlayAt("explosion", center, 1f);
        foreach (var enemy in EnemyController.All.ToArray())
        {
            if (enemy == null || enemy.IsDead) continue;
            Vector3 delta = enemy.transform.position + Vector3.up - center;
            float distance = delta.magnitude;
            if (distance > Radius || !CombatBoons.ClearLine(center, center + delta, enemy.transform)) continue;
            float strength = Mathf.Lerp(1f, .45f, distance / Radius);
            enemy.TakeDamage(18f * strength, delta.normalized, 0);
            if (!enemy.IsDead) enemy.Repel(center, strength * 2.5f);
            gm.RegisterExplosionHit(enemy);
        }
        Destroy(gameObject);
    }
}
