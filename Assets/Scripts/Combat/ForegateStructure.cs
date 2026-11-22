using System.Collections.Generic;
using UnityEngine;

// Defend owns this additive structure. Distances use the Blender drawing's
// convention: positive depth is inside the castle, negative faces the road.
public sealed class ForegateStructure : MonoBehaviour
{
    public Vector3 Outward => transform.forward;
    public Vector3 InnerEntry => World(0, 0, 21);
    public Transform WestLeaf { get; private set; }
    public Transform EastLeaf { get; private set; }
    public Transform Cradle { get; private set; }
    public Transform ReadyStone { get; private set; }
    readonly List<Transform> reserveStones = new();
    internal int ReserveStones => reserveStones.Count;

    readonly List<GameObject> displaced = new();
    Mesh stairCollision;
    Terrain terrain;
    TerrainData originalTerrain, siegeTerrain;

    public Vector3 World(float x, float height, float inward) =>
        transform.TransformPoint(x, height, -inward);

    public static ForegateStructure Build()
    {
        var asset = Resources.Load<GameObject>("Foregate/OuterGatehouse");
        if (asset == null) return null;
        var structure = new GameObject("Outer gate and enclosed courtyard").AddComponent<ForegateStructure>();
        structure.transform.SetPositionAndRotation(new Vector3(-14.3f, -7.73f, -60.6f),
            Quaternion.Euler(0, 180f, 0));
        structure.Install(asset);
        return structure;
    }

    void Install(GameObject asset)
    {
        var mirror = Child(transform, "Authoring axes");
        var basis = Child(mirror, "Imported model basis");
        var model = Instantiate(asset, basis, false);
        var pieces = model.GetComponentsInChildren<Transform>(true);
        // Importers may express Blender's axes on the root, or bake them into
        // each mesh. The exported metre markers preserve each authored hinge.
        Vector3 Axis(string name) => basis.InverseTransformPoint(
            System.Array.Find(pieces, p => p.name == name).position);
        var east = Axis("East axis");
        var outward = Axis("Outward axis");
        basis.localRotation = Quaternion.Inverse(Quaternion.LookRotation(outward, Axis("Up axis")));
        basis.localScale = Vector3.one / east.magnitude;
        if ((basis.localRotation * east).x < 0) mirror.localScale = new Vector3(-1, 1, 1);

        foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
        {
            string part = filter.name;
            if (part.EndsWith("gate leaf"))
            {
                bool west = part.StartsWith("West");
                float sign = west ? -1 : 1;
                var hinge = Child(transform, part + " hinge");
                hinge.localPosition = new Vector3(sign * 2.32f, 0, -.42f);
                filter.transform.SetParent(hinge, true);
                Box(hinge, "Solid oak", new Vector3(-sign * 1.15f, 2.75f, 0),
                    new Vector3(2.30f, 5.5f, .34f));
                if (west) WestLeaf = hinge;
                else EastLeaf = hinge;
            }
            else if (part == "Tipping cradle") Cradle = filter.transform;
            else if (part == "Drop stone 1") ReadyStone = filter.transform;
            else if (part.StartsWith("Drop stone ")) reserveStones.Add(filter.transform);
            else if (part.EndsWith("return"))
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }

        ClearApproachVegetation();
        BuildWalkways();
        BuildGallery();
        foreach (string name in new[] { "Wall (7)", "House (3)", "House (20)",
            "House (3) Foundation", "House (20) Foundation" })
            ClearForSiege(GameObject.Find(name));
        foreach (string group in new[] { "Base Props", "Supply Crates" })
        {
            var props = GameObject.Find("Environment/" + group);
            if (props == null) continue;
            foreach (Transform prop in props.transform)
            {
                var local = transform.InverseTransformPoint(prop.position);
                if ((Mathf.Abs(local.x) < 3.5f && local.z < 0f && local.z > -72f) ||
                    (Mathf.Abs(Mathf.Abs(local.x) - 8.43f) < 2.6f && local.z < -9.5f && local.z > -14f))
                    ClearForSiege(prop.gameObject);
            }
        }
        Physics.SyncTransforms();
    }

    void BuildWalkways()
    {
        // One closed, eight-vertex prism supplies each stair flight. Its level
        // ends abut the landings exactly; the Rigidbody never hits a tread edge.
        stairCollision = new Mesh { name = "Foregate stair prism" };
        stairCollision.vertices = new[]
        {
            new Vector3(-.5f, 0, 0), new Vector3(.5f, 0, 0), new Vector3(-.5f, 1, 1), new Vector3(.5f, 1, 1),
            new Vector3(-.5f, -1, 0), new Vector3(.5f, -1, 0), new Vector3(-.5f, -1, 1), new Vector3(.5f, -1, 1)
        };
        stairCollision.triangles = new[] {
            0,2,1, 1,2,3, 4,5,6, 5,7,6, 0,1,4, 4,1,5, 2,6,3, 3,6,7,
            0,4,2, 2,4,6, 1,3,5, 5,3,7
        };
        stairCollision.RecalculateBounds();
        for (int side = -1; side <= 1; side += 2)
        {
            float toe = StairToe(side);
            Ramp("Courtyard stair", new Vector3(side * 8.43f, toe, -11.17f),
                new Vector3(side * 8.43f, 6.23f, -1.65f), 2.3f);
            Ramp("Stair balustrade", new Vector3(side * 9.68f, side < 0 ? 3.83f : 2.85f, -11.17f),
                new Vector3(side * 9.68f, 6.83f, -1.65f), .30f);
            // The lower walk ends at x=7.80. Only the crossover occupies
            // the strip between it and the higher pier at x=6.285.
            Box(transform, "Curtain wall", new Vector3(side * 11.175f, 2.115f, 0), new Vector3(6.75f, 8.23f, 3.3f));
            Ramp("Gallery stair", new Vector3(side * 7.80f, 6.23f, -.25f),
                new Vector3(side * 6.285f, 6.845f, -.25f), 2.80f);
            Box(transform, "Crossover masonry", new Vector3(side * 7.0425f, 1.8075f, -.25f), new Vector3(1.515f, 7.615f, 2.80f));
            Box(transform, "Parapet footing", new Vector3(side * 7.0425f, 2.115f, 1.40f), new Vector3(1.515f, 8.23f, .50f));
            Box(transform, "Gate pier", new Vector3(side * 4.27f, 2.4225f, 0), new Vector3(4.03f, 8.845f, 5.55f));

            Box(transform, "Curtain parapet", new Vector3(side * 10.315f, 6.41f, 1.43f), new Vector3(8.06f, .36f, .38f));
            for (int i = 1; i < 8; i++)
                Box(transform, "Curtain merlon", new Vector3(side * (5.45f + i * 1.19f), 6.80f, 1.43f),
                    new Vector3(.56f, .46f, .48f));
            for (int j = -1; j <= 1; j++)
                Box(transform, "Pier merlon", new Vector3(side * 4.27f + j * 1.22f, 7.095f, 2.47f),
                    new Vector3(.64f, .51f, .51f));
            for (int j = 0; j < 2; j++)
                Box(transform, "Pier side merlon", new Vector3(side * 5.85f, 7.13f, 2.04f - j * .67f),
                    new Vector3(.42f, .57f, .46f));
            Box(transform, "Arrow bin", new Vector3(side * 11.3f, 6.54f, -.47f),
                new Vector3(1.86f, .55f, .62f));
        }
    }

    void BuildGallery()
    {
        // Only the rear boards cross the opening. The flanking boards end
        // at either lip of the two-metre-wide stone channel.
        Box(transform, "Gallery crossing", new Vector3(0, 6.655f, -.97f), new Vector3(4.51f, .38f, 2.07f));
        for (int side = -1; side <= 1; side += 2)
            Box(transform, "Channel walkway", new Vector3(side * 1.64f, 6.655f, 1.39f), new Vector3(1.23f, .38f, 2.65f));
        for (int side = -1; side <= 1; side += 2)
        {
            Box(transform, "Gallery parapet", new Vector3(side * 2.34f, 6.98f, 2.61f), new Vector3(.82f, .27f, .40f));
            Box(transform, "Gallery merlon", new Vector3(side * 2.30f, 7.23f, 2.61f), new Vector3(.40f, .26f, .43f));
        }

        // Follow the two stone arch rings above the doorway. The space
        // between the rings remains clear through to the ground.
        const int segments = 13;
        for (int i = 0; i < segments; i++)
        {
            float angle = (i + .5f) * Mathf.PI / segments;
            foreach (float depth in new[] { 2.27f, -1.44f })
            {
                var stone = Box(transform, "Arch voussoir", Vector3.zero, new Vector3(.66f, .54f, .50f));
                stone.transform.localPosition = new Vector3(2.61f * Mathf.Cos(angle), 3.44f + 2.61f * Mathf.Sin(angle), depth);
                stone.transform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90f);
            }
        }
    }

    internal void LoadNextStone()
    {
        int index = reserveStones.Count - 1;
        if (index < 0) return;
        reserveStones[index].gameObject.SetActive(false);
        reserveStones.RemoveAt(index);
    }

    internal bool IsOnParapet(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        return local.y >= 6f && Mathf.Abs(local.z) <= 3f && Mathf.Abs(local.x) <= 15f;
    }

    public void SetOpen(float fraction)
    {
        float angle = 96 * Mathf.Clamp01(fraction);
        WestLeaf.localRotation = Quaternion.Euler(0, angle, 0);
        EastLeaf.localRotation = Quaternion.Euler(0, -angle, 0);
    }

    void Ramp(string name, Vector3 low, Vector3 high, float width)
    {
        var run = Vector3.ProjectOnPlane(high - low, Vector3.up);
        var ramp = Child(transform, name);
        ramp.localPosition = low;
        ramp.localRotation = Quaternion.LookRotation(run);
        ramp.localScale = new Vector3(width, high.y - low.y, run.magnitude);
        ramp.gameObject.AddComponent<MeshCollider>().sharedMesh = stairCollision;
    }

    static BoxCollider Box(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var box = Child(parent, name).gameObject.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
        return box;
    }

    static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    void ClearForSiege(GameObject piece)
    {
        if (piece == null || !piece.activeSelf) return;
        displaced.Add(piece);
        piece.SetActive(false);
    }

    void ClearApproachVegetation()
    {
        terrain = Terrain.activeTerrain;
        if (terrain == null) return;
        originalTerrain = terrain.terrainData;
        // The world scene persists between modes. Its authored tree, rock and
        // grass placements stay on the original terrain data.
        siegeTerrain = Instantiate(originalTerrain);
        var size = siegeTerrain.size;
        GradeKeepRoad();
        GradeStairFoot(-1);
        GradeStairFoot(1);
        var trees = new List<TreeInstance>();
        foreach (var tree in siegeTerrain.treeInstances)
        {
            Vector3 at = terrain.transform.position + Vector3.Scale(tree.position, size);
            Vector3 local = transform.InverseTransformPoint(at);
            float depth = -local.z;
            if ((depth > -5 && depth < 18 && Mathf.Abs(local.x) < 17) ||
                (depth > -30 && depth < 74 && Mathf.Abs(local.x) < 6)) continue;
            trees.Add(tree);
        }
        siegeTerrain.SetTreeInstances(trees.ToArray(), false);
        int n = siegeTerrain.detailResolution;
        Vector3 origin = terrain.transform.position;
        Vector3 corner = World(3.5f, 0, -23);
        int x0 = Mathf.Clamp(Mathf.FloorToInt((corner.x-origin.x)*n/size.x), 0, n-1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((corner.z-origin.z)*n/size.z), 0, n-1);
        int w = Mathf.Min(n-x0, Mathf.CeilToInt(7f*n/size.x)+1);
        int h = Mathf.Min(n-z0, Mathf.CeilToInt(98f*n/size.z)+1);
        var cleared = new int[h,w];
        for (int layer = 0; layer < siegeTerrain.detailPrototypes.Length; layer++)
            siegeTerrain.SetDetailLayer(x0,z0,layer,cleared);
        terrain.terrainData = siegeTerrain;
        terrain.GetComponent<TerrainCollider>().terrainData = siegeTerrain;
    }

    static float StairToe(int side) => side < 0 ? 3.22f : 2.24f;

    void GradeStairFoot(int side)
    {
        // Fit a small landing to the sloped collision flight. Keep the
        // surrounding courtyard grade outside the width of the approach.
        int resolution = siegeTerrain.heightmapResolution;
        Vector3 size = siegeTerrain.size, origin = terrain.transform.position;
        float spacing = size.x / (resolution - 1);
        float centreX = side * 8.43f;
        Vector3 corner = World(centreX + 1.9f, 0, 10f);
        int x0 = Mathf.FloorToInt((corner.x - origin.x) / spacing);
        int z0 = Mathf.FloorToInt((corner.z - origin.z) / spacing);
        int width = Mathf.CeilToInt(3.8f / spacing) + 2;
        int height = Mathf.CeilToInt(4.6f / spacing) + 2;
        var heights = siegeTerrain.GetHeights(x0, z0, width, height);
        for (int z = 0; z < height; z++)
        for (int x = 0; x < width; x++)
        {
            var at = origin + new Vector3((x+x0)*spacing, 0, (z+z0)*spacing);
            Vector3 local = transform.InverseTransformPoint(at);
            float depth = -local.z;
            float blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.9f, 1.18f, Mathf.Abs(local.x - centreX))) *
                Mathf.InverseLerp(10f, 10.85f, depth) * Mathf.InverseLerp(14.6f, 11.7f, depth);
            float target = World(0, StairToe(side) - .025f, 0).y;
            heights[z,x] = Mathf.Lerp(heights[z,x], (target-origin.y)/size.y, blend);
        }
        siegeTerrain.SetHeights(x0, z0, heights);
    }

    void GradeKeepRoad()
    {
        // Ease the former house pads into one walkable incline. Feather the
        // shoulders into the village terrain without disturbing nearby floors.
        int resolution = siegeTerrain.heightmapResolution;
        Vector3 size = siegeTerrain.size, origin = terrain.transform.position;
        const float shoulder = 7.4f;
        Vector3 start = World(shoulder, 0, -2);
        float spacing = size.x / (resolution - 1);
        int x0 = Mathf.FloorToInt((start.x-origin.x)/spacing);
        int z0 = Mathf.FloorToInt((start.z-origin.z)/spacing);
        int width = Mathf.CeilToInt(shoulder*2/spacing)+2;
        int height = Mathf.CeilToInt(70f/spacing)+2;
        var heights = siegeTerrain.GetHeights(x0,z0,width,height);
        for (int z=0; z<height; z++)
        for (int x=0; x<width; x++)
        {
            var at = origin + new Vector3((x+x0)*spacing,0,(z+z0)*spacing);
            Vector3 local = transform.InverseTransformPoint(at);
            float depth = -local.z;
            float blend = Mathf.SmoothStep(0,1,Mathf.InverseLerp(shoulder,2.7f,Mathf.Abs(local.x))) *
                Mathf.InverseLerp(-2,0,depth) * Mathf.InverseLerp(68,66,depth);
            float roadY = depth < 36 ? Mathf.Lerp(-7.73f,-.9f,depth/36f) :
                Mathf.Lerp(-.9f,0,(depth-36)/30f);
            heights[z,x] = Mathf.Lerp(heights[z,x],(roadY-origin.y)/size.y,blend);
        }
        siegeTerrain.SetHeights(x0,z0,heights);
    }

    void OnDestroy()
    {
        if (terrain != null && terrain.terrainData == siegeTerrain && originalTerrain != null)
        {
            terrain.terrainData = originalTerrain;
            terrain.GetComponent<TerrainCollider>().terrainData = originalTerrain;
        }
        if (siegeTerrain != null) Destroy(siegeTerrain);
        if (stairCollision != null) Destroy(stairCollision);
        foreach (var piece in displaced) if (piece != null) piece.SetActive(true);
    }
}
