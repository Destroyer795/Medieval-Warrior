using UnityEngine;
using UnityEngine.AI;

public sealed class HordeGoblet : MonoBehaviour
{
    public struct Site { public Collider wall; public Vector3 approach, back, normal; }
    public static readonly Color Tint = new Color(.28f, .9f, .43f);
    public Vector3 Target => transform.position;
    public Vector3 Approach { get; private set; }
    public bool InReach(Vector3 point) => Vector3.Distance(point, Approach) < 2.2f && Vector3.Distance(point + Vector3.up, Target) < 3f;

    internal static bool TryFindSite(Vector3 openingSpawn, out Site site)
    {
        site = default;
        if (!NavMesh.SamplePosition(openingSpawn, out var start, 5f, NavMesh.AllAreas)) return false;
        var keep = GameObject.Find("Castle/Castle")?.transform;
        var path = new NavMeshPath();
        float nearest = float.PositiveInfinity;
        Vector3 towardKeep = keep != null
            ? Vector3.ProjectOnPlane(keep.position - start.position, Vector3.up).normalized
            : Vector3.forward;
        if (towardKeep == Vector3.zero) towardKeep = Vector3.forward;

        // Sample the walkable village around the opening spawn. The closest
        // usable vertical stone face must belong to the keep itself.
        if (keep != null)
        {
            for (float radius = 3.5f; radius <= 24f; radius += 1.25f)
            for (int angle = -180; angle < 180; angle += 12)
            {
                var offset = Quaternion.Euler(0, angle, 0) * towardKeep * radius;
                if (!NavMesh.SamplePosition(start.position + offset, out var floor, 2f, NavMesh.AllAreas)) continue;
                if (Mathf.Abs(floor.position.y - start.position.y) > 2f) continue;
                var front = floor.position + Vector3.up * 1.25f;
                var inward = Vector3.ProjectOnPlane(keep.position - front, Vector3.up).normalized;
                if (!Physics.Raycast(front, inward, out var surface, 2.6f, ~0, QueryTriggerInteraction.Ignore) ||
                    (surface.transform != keep && !surface.transform.IsChildOf(keep)) ||
                    surface.distance < 1f || Mathf.Abs(surface.normal.y) > .25f) continue;
                float distance = WalkDistance(start.position, floor.position, path);
                if (distance > 28f || distance >= nearest) continue;
                nearest = distance;
                site = new Site
                {
                    wall = surface.collider,
                    back = surface.point,
                    approach = floor.position,
                    normal = Vector3.ProjectOnPlane(surface.normal, Vector3.up).normalized
                };
            }
            if (site.wall != null) return true;
        }

        // A bake without ground tight to the keep gets a freestanding plinth
        // in the opening courtyard, within a short walk of the same spawn.
        for (int radius = 5; radius <= 11; radius += 2)
        for (int angle = 0; angle <= 180; angle += 30)
        for (int side = -1; side <= 1; side += 2)
        {
            var direction = Quaternion.Euler(0, angle * side, 0) * towardKeep;
            if (!NavMesh.SamplePosition(start.position + direction * radius, out var floor, 1.6f, NavMesh.AllAreas) ||
                Mathf.Abs(floor.position.y - start.position.y) > 2f ||
                WalkDistance(start.position, floor.position, path) > 14f) continue;
            var back = floor.position + Vector3.up * 1.25f;
            if (Physics.CheckSphere(back, .65f, ~0, QueryTriggerInteraction.Ignore)) continue;
            site = new Site { approach = floor.position, normal = -direction, back = back };
            return true;
        }
        return false;
    }

    static float WalkDistance(Vector3 start, Vector3 end, NavMeshPath path)
    {
        if (!NavMesh.CalculatePath(start, end, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete) return float.PositiveInfinity;
        float distance = 0f;
        foreach (var corner in path.corners)
        {
            distance += Vector3.Distance(start, corner);
            start = corner;
        }
        return distance;
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
            Part(PrimitiveType.Cylinder, "Courtyard plinth", new Vector3(0,-.85f,-.22f), new Vector3(.53f,.45f,.53f),stone);
        Part(PrimitiveType.Cylinder, "Foot", Vector3.down*.28f,new Vector3(.33f,.035f,.33f),gold);
        Part(PrimitiveType.Cylinder, "Stem", Vector3.down*.16f,new Vector3(.11f,.11f,.11f),gold);
        Part(PrimitiveType.Sphere, "Green chalice", Vector3.zero,new Vector3(.42f,.32f,.42f),enamel);
        Part(PrimitiveType.Cylinder, "Gilded lip", Vector3.up*.10f,new Vector3(.44f,.025f,.44f),gold);
        Part(PrimitiveType.Cylinder, "Luminous draught", Vector3.up*.13f,new Vector3(.39f,.008f,.39f),jade);
        return cup;
    }
}
