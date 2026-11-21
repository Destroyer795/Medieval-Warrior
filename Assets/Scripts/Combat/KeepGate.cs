using UnityEngine;
using UnityEngine.AI;

// Battle-owned overlay on the double-door frame of the retained Background scene.
public sealed class KeepGate : MonoBehaviour
{
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public float Integrity => Health / MaxHealth;
    public float LastImpact { get; private set; } = float.NegativeInfinity;
    public Vector3 Approach { get; private set; }
    public Vector3 Road { get; private set; }
    public Vector3 Outward => transform.forward;
    FireBrazier leftBrazier, rightBrazier;
    readonly System.Collections.Generic.List<Vector3> barrelSites = new System.Collections.Generic.List<Vector3>();
    public bool CanLightArrow(Vector3 at) =>
        (leftBrazier != null && leftBrazier.InReach(at)) || (rightBrazier != null && rightBrazier.InReach(at));

    readonly System.Collections.Generic.List<Vector3> lanes = new System.Collections.Generic.List<Vector3>();
    readonly System.Collections.Generic.List<Vector3> batteries = new System.Collections.Generic.List<Vector3>();
    public System.Collections.Generic.IReadOnlyList<Vector3> Lanes => lanes;
    NavMeshPath path;
    float nextWarning;
    GameManager match;

    internal static KeepGate Create(GameManager match, Vector3 player)
    {
        var frame = GameObject.Find("Castle/Castle/Double Door Frame");
        var road = GameObject.Find("Environment/Siege Road Gate");
        if (frame == null || road == null || !frame.transform.parent.TryGetComponent<Renderer>(out var keep))
            return null;

        var root = new GameObject("Keep Sanctuary Gate");
        root.transform.SetParent(match.transform, false);
        var gate = root.AddComponent<KeepGate>();
        gate.match = match;
        var ground = frame.transform.position;
        ground.y = keep.bounds.min.y + .05f;
        root.transform.SetPositionAndRotation(ground, Quaternion.LookRotation(-frame.transform.parent.forward));
        if (!NavMesh.SamplePosition(player, out var start, 5, NavMesh.AllAreas) ||
            !gate.TryReach(start.position, ground + gate.Outward * 1.15f, 3f, out var approach) ||
            !gate.TryRoadEntry(approach, road.transform.position, out var entry))
        {
            Destroy(root);
            return null;
        }
        gate.Approach = approach;
        gate.Road = entry;
        gate.MaxHealth = GameSettings.Difficulty switch
        {
            Difficulty.Squire => 1250f, Difficulty.Warlord => 850f, _ => 1000f
        };
        gate.Health = gate.MaxHealth;
        float width = Mathf.Abs(frame.transform.parent.lossyScale.x) * .58f;
        float height = Mathf.Abs(frame.transform.parent.lossyScale.y) * .82f;
        // Keep the original gatehouse crest and portcullis visible over its timber leaves.
        gate.AddLeaves(width, height);
        gate.PlanApproaches();
        gate.PlaceDefenses();
        return gate;
    }

    // On the baked map the terrain outside the wall can be a separate NavMesh island.
    // Search from the road threshold inward so the entire assault enters on foot.
    bool TryRoadEntry(Vector3 approach, Vector3 threshold, out Vector3 entry)
    {
        Vector3 inward = Vector3.ProjectOnPlane(approach - threshold, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, inward);
        for (int depth = 0; depth <= 12; depth += 2)
        {
            for (int lateral = 0; lateral < 5; lateral++)
            {
                float offset = lateral % 2 == 0 ? lateral : -lateral - 1;
                Vector3 probe = threshold + inward * depth + side * offset;
                if (Terrain.activeTerrain != null)
                    probe.y = Terrain.activeTerrain.SampleHeight(probe) + Terrain.activeTerrain.transform.position.y;
                if (TryReach(approach, probe, 3, out entry)) return true;
            }
        }
        entry = default;
        return false;
    }

    void AddLeaves(float width, float height)
    {
        var wood = new Color(.25f, .12f, .055f);
        for (int i = 0; i < 8; i++)
            Part(transform, PrimitiveType.Cube, "Oak gate plank",
                new Vector3(width * ((i + .5f) / 8f - .5f), height * .5f, -.08f),
                new Vector3(width / 8f - .015f, height, .20f), wood * (1f + .05f * (i % 3)));
        for (int i = 1; i <= 2; i++)
            Part(transform, PrimitiveType.Cube, "Iron strap", new Vector3(0, height * i / 3f, .045f),
                new Vector3(width, .14f, .08f), new Color(.19f, .17f, .14f));
        var hull = gameObject.AddComponent<BoxCollider>();
        hull.center = Vector3.up * height * .5f;
        hull.size = new Vector3(width, height, .25f);
    }

    void PlaceDefenses()
    {
        FireBrazier Flank(float side)
        {
            var probe = Approach + Outward * 3 + transform.right * side * 2.7f;
            return TryReach(Approach, probe, 4f, out var at) ? FireBrazier.Place(transform, at) : null;
        }
        leftBrazier = Flank(-1);
        rightBrazier = Flank(1);
        foreach (Vector3 site in barrelSites) ExplosiveBarrel.Place(transform, site);
        var roadSide = Road + Vector3.Cross(Vector3.up, (Approach - Road).normalized) * 1.6f;
        if (TryReach(Road, roadSide, 2f, out var entry)) ExplosiveBarrel.Place(transform, entry);
    }

    void PlanApproaches()
    {
        var data = Resources.Load<TextAsset>("Horde/Arena");
        var arena = data != null ? JsonUtility.FromJson<HordeDirector.Arena>(data.text) : null;
        if (arena?.lanes != null)
        {
            foreach (var lane in arena.lanes)
            {
                var center = (lane.start + lane.end) * .5f;
                float range = Vector3.Distance(Approach, center);
                if (range < 10 || range > 44 || lane.width > 4.6f ||
                    lanes.Exists(p => (p - center).sqrMagnitude < 12 * 12)) continue;
                if (!TryReach(Approach, center, 3f, out var junction)) continue;
                lanes.Add(junction);
                if (barrelSites.Count < 4 && TryBarrelSite(lane, arena.lanes, out var site))
                    barrelSites.Add(site);
            }
        }
        // Keep a clear arc into the steps. Prefer lower plaza ground with room to deploy.
        var target = Approach + Outward * 3;
        for (int i = 0; i < 40; i++)
        {
            Vector3 dir = Quaternion.AngleAxis((i % 8 - 3.5f) * 17, Vector3.up) * Outward;
            var probe = target + dir * (10 + 2.5f * (i / 8));
            if (!TryReach(Approach, probe, 4f, out var at) || at.y > Approach.y + .45f ||
                !NavMesh.FindClosestEdge(at, out var edge, NavMesh.AllAreas) || edge.distance < 1.1f ||
                batteries.Exists(p => (p - at).sqrMagnitude < 16) ||
                !BileGlob.ClearArc(at + Vector3.up * 1.7f, target)) continue;
            batteries.Add(at);
        }
    }

    bool TryBarrelSite(HordeDirector.Lane lane, HordeDirector.Lane[] routes, out Vector3 site)
    {
        // Tuck barrels into the mouth of an alley. Leave clearance for a running knight
        // on every intersecting lane as well as space for an agent to pass the barrel.
        Vector3 along = Vector3.ProjectOnPlane(lane.end - lane.start, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, along);
        for (int i = 0; i < 12; i++)
        {
            bool far = (i & 2) == 0;
            var mouth = far ? lane.end : lane.start;
            var probe = mouth + along * (far ? 1 : -1) * (1 + i / 4) +
                side * ((i & 1) == 0 ? 1.35f : -1.35f);
            if (!TryReach(Approach, probe, 1.0f, out site)) continue;
            bool clear = true;
            foreach (var route in routes)
            {
                Vector3 segment = Vector3.ProjectOnPlane(route.end - route.start, Vector3.up);
                Vector3 offset = Vector3.ProjectOnPlane(site - route.start, Vector3.up);
                float t = Mathf.Clamp01(Vector3.Dot(offset, segment) / Mathf.Max(.01f, segment.sqrMagnitude));
                if ((offset - segment * t).sqrMagnitude >= 1.5f * 1.5f) continue;
                clear = false;
                break;
            }
            if (clear) return true;
        }
        site = default;
        return false;
    }

    internal Vector3 Battery(int index) => batteries.Count == 0 ? Approach + Outward * 8 :
        batteries[(index & int.MaxValue) % batteries.Count];

    internal Vector3 Deployment(int index) => lanes.Count == 0 ? Road : lanes[(index & int.MaxValue) % lanes.Count];

    // Property blocks preserve a shared built-in material across the siege props.
    internal static Transform Part(Transform parent, PrimitiveType shape, string label, Vector3 position, Vector3 size, Color color)
    {
        var piece = GameObject.CreatePrimitive(shape);
        piece.name = label;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = size;
        var collider = piece.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        var tint = new MaterialPropertyBlock();
        tint.SetColor("_Color", color);
        piece.GetComponent<Renderer>().SetPropertyBlock(tint);
        return piece.transform;
    }

    internal bool TryReach(Vector3 from, Vector3 near, float radius, out Vector3 position)
    {
        path ??= new NavMeshPath();
        position = default;
        if (!NavMesh.SamplePosition(near, out var sample, radius, NavMesh.AllAreas) ||
            !NavMesh.CalculatePath(from, sample.position, NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete) return false;
        position = sample.position;
        return true;
    }

    public void Strike(float amount)
    {
        if (amount <= 0 || Health <= 0 || match.State != GameManager.MatchState.Playing ||
            match.IsPaused || match.ChoosingBoon) return;
        Health = Mathf.Max(0, Health - amount);
        bool warn = Time.time >= nextWarning;
        if (warn || Time.time - LastImpact >= .16f) AudioManager.GateImpact(Approach, warn);
        LastImpact = Time.time;
        if (warn)
        {
            nextWarning = Time.time + 6f;
            GameHUD.GateWarning();
        }
        if (Health <= 0) match.GameOver();
    }
}
