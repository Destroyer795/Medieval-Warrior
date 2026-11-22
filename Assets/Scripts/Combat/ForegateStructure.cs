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

    GameObject displacedTower;

    public Vector3 World(float x, float height, float inward) =>
        transform.TransformPoint(x, height, -inward);

    public static ForegateStructure Build()
    {
        var asset = Resources.Load<GameObject>("Foregate/OuterGatehouse");
        if (asset == null) return null;
        var structure = new GameObject("Outer gate and enclosed courtyard").AddComponent<ForegateStructure>();
        structure.transform.SetPositionAndRotation(new Vector3(14, -.54f, 75),
            Quaternion.Euler(0, .39f * Mathf.Rad2Deg, 0));
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
            else if (part.EndsWith("curtain") || part.EndsWith("return") ||
                     part.EndsWith("gate pier") || part == "Arch and gallery")
            {
                // Crenels and the central drop channel remain open for arrows and stones.
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
        }

        for (int side = -1; side <= 1; side += 2)
        {
            // The treads are .18 m high. A single smooth plane keeps a
            // CharacterController grounded in either direction.
            Ramp(new Vector3(side * 7.18f, .07f, -11.05f),
                new Vector3(side * 7.18f, 6.25f, -1.40f), 2.30f);
            Ramp(new Vector3(side * 7.74f, 6.24f, -.28f),
                new Vector3(side * 6.17f, 6.86f, -.28f), 1.65f);
            var rail = Ramp(new Vector3(side * 8.43f, .70f, -11.05f),
                new Vector3(side * 8.43f, 6.89f, -1.40f), .31f);
            rail.size += Vector3.up * .65f;
            rail.center -= Vector3.up * .325f;
            Box(transform, "Arrow bin", new Vector3(side * 11.3f, 6.54f, -.47f),
                new Vector3(1.86f, .55f, .62f));
        }
        // This tower occupies the old road marker. Its neighbours end either
        // side of the passage and remain the rear corners of the courtyard.
        displacedTower = GameObject.Find("TowerB (8)");
        if (displacedTower != null) displacedTower.SetActive(false);
        Physics.SyncTransforms();
    }

    public void SetOpen(float fraction)
    {
        float angle = 96 * Mathf.Clamp01(fraction);
        WestLeaf.localRotation = Quaternion.Euler(0, angle, 0);
        EastLeaf.localRotation = Quaternion.Euler(0, -angle, 0);
    }

    BoxCollider Ramp(Vector3 low, Vector3 high, float width)
    {
        var span = high - low;
        var ramp = Child(transform, "Stair slope");
        ramp.localRotation = Quaternion.LookRotation(span, Vector3.up);
        ramp.localPosition = (low + high) / 2;
        return Box(ramp, "Walking surface", new Vector3(0, -.11f, 0),
            new Vector3(width, .22f, span.magnitude));
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

    void OnDestroy()
    {
        if (displacedTower != null) displacedTower.SetActive(true);
    }
}
