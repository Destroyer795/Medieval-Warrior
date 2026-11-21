using UnityEngine;
using UnityEngine.AI;

public sealed class HordeGoblet : MonoBehaviour
{
    public struct Site { public Collider wall; public Vector3 approach, back, normal; }
    public static readonly Color Tint = new Color(.28f, .9f, .43f);
    public Vector3 Target => transform.position;
    public Vector3 Approach { get; private set; }
    public bool InReach(Vector3 point) => Vector3.Distance(point, Approach) < 2.2f && Vector3.Distance(point + Vector3.up, Target) < 3f;

    internal static bool TryFindSite(Vector3 player, out Site site)
    {
        site = default;
        if (!NavMesh.SamplePosition(player, out var start, 5f, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        float nearest = float.PositiveInfinity;
        foreach (var wall in FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
        {
            if (!wall.name.StartsWith("Wall", System.StringComparison.OrdinalIgnoreCase)) continue;
            var center = wall.bounds.center;
            center.y = Mathf.Clamp(player.y + 1.4f, wall.bounds.min.y + .1f, wall.bounds.max.y);
            for (int i = 0; i < 8; i++)
            {
                var outward = Quaternion.Euler(0, 45 * i, 0) * Vector3.forward;
                var probe = center + outward * (wall.bounds.extents.magnitude + 3);
                if (!wall.Raycast(new Ray(probe, -outward), out var face, Vector3.Distance(probe, center) + 1)) continue;
                if (Mathf.Abs(face.normal.y) > .25f) continue;
                var normal = Vector3.ProjectOnPlane(face.normal, Vector3.up).normalized;
                if (!NavMesh.SamplePosition(face.point + normal * 1.8f, out var floor, 6, NavMesh.AllAreas)) continue;
                var front = floor.position + Vector3.up * 1.25f;
                if (!Physics.Raycast(front, -normal, out var surface, 2.65f, ~0, QueryTriggerInteraction.Ignore) || surface.collider != wall) continue;
                if (Vector3.Distance(player, floor.position) >= nearest) continue;
                if (!NavMesh.CalculatePath(start.position, floor.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                nearest = Vector3.Distance(player, floor.position);
                site = new Site { wall = wall, back = surface.point, approach = floor.position, normal = normal };
            }
        }
        if (site.wall != null) return true;
        // On a bake that leaves a gap at the masonry, use a freestanding plinth
        // on the village side of the outer road gate.
        var gate = GameObject.Find("Environment/Siege Road Gate");
        if (gate == null) return false;
        for (int radius = 2; radius <= 12; radius += 2)
        for (int angle = 0; angle < 360; angle += 30)
        {
            var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            if (!NavMesh.SamplePosition(gate.transform.position + direction * radius, out var position, 4, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(start.position, position.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            site = new Site { approach = position.position, normal = direction, back = position.position + Vector3.up * 1.25f };
            return true;
        }
        return false;
    }

    internal static HordeGoblet Create(Site site, Transform parent)
    {
        var go = new GameObject("Verdant goblet");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(site.back + site.normal * .48f, Quaternion.LookRotation(site.normal));
        var cup = go.AddComponent<HordeGoblet>();
        cup.Approach = site.approach;
        var stone = HordeVFX.Stone;
        var gold = ProceduralModels.Mat("Goblet bronze", new Color(.44f,.34f,.18f), .4f, .65f);
        var enamel = ProceduralModels.Mat("Goblet patina", new Color(.13f,.33f,.22f), .48f, .52f);
        var jade = ProceduralModels.Mat("Goblet jade", Tint, .65f, .35f, Tint * .6f);
        void Part(PrimitiveType shape, string name, Vector3 at, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.GetComponent<Collider>().enabled = false;
            part.transform.SetParent(go.transform, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
        Part(PrimitiveType.Cube, "Stone corbel", new Vector3(0,-.40f,-.22f), new Vector3(.74f,.18f,.85f),stone);
        if (site.wall == null)
            Part(PrimitiveType.Cylinder, "Gate plinth", new Vector3(0,-.85f,-.22f), new Vector3(.53f,.45f,.53f),stone);
        Part(PrimitiveType.Cylinder, "Foot", Vector3.down*.28f,new Vector3(.33f,.035f,.33f),gold);
        Part(PrimitiveType.Cylinder, "Stem", Vector3.down*.16f,new Vector3(.11f,.11f,.11f),gold);
        Part(PrimitiveType.Sphere, "Green chalice", Vector3.zero,new Vector3(.42f,.32f,.42f),enamel);
        Part(PrimitiveType.Cylinder, "Gilded lip", Vector3.up*.10f,new Vector3(.44f,.025f,.44f),gold);
        Part(PrimitiveType.Cylinder, "Luminous draught", Vector3.up*.13f,new Vector3(.39f,.008f,.39f),jade);
        return cup;
    }
}
