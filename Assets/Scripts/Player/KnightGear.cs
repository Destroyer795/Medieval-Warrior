using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The knight's sword and shield are part of his single skinned mesh. To let him use the bow with
// both hands, this finds the sword and shield as separate mesh islands (by bone weights and shape),
// and while the bow is out shows a copy of the mesh without them - with the sword sheathed on the
// hip and the shield slung on the back as separate props built from the same vertices.
[DefaultExecutionOrder(110)]
public class KnightGear : MonoBehaviour
{
    public bool Ready { get; private set; }
    public string Report { get; private set; } = "not analysed";

    SkinnedMeshRenderer smr;
    Mesh original, withoutGear;
    Transform swordProp, shieldProp, pelvis, spine;
    float swordLength, shieldHeight;
    bool stowed;
    PlayerControlSpeed owner;
    Transform quiver;
    readonly List<GameObject> storedArrows = new List<GameObject>();
    int shownArrows = -1, quiverCapacity;
    float hipHalfWidth = 0.2f;
    public int VisibleArrowCount => Mathf.Max(0, shownArrows);
    Transform leftShoulder, rightShoulder, neck;
    float backDepth = 0.12f, shieldThickness;
    public Quaternion ChestRotation
    {
        get
        {
            if (leftShoulder == null || rightShoulder == null || neck == null || spine == null) return transform.rotation;
            Vector3 up = (neck.position - spine.position).normalized;
            if (Vector3.Dot(up, transform.up) < 0.5f) up = transform.up;
            Vector3 right = (rightShoulder.position - leftShoulder.position).normalized;
            Vector3 forward = Vector3.Cross(right, up).normalized;
            if (forward.sqrMagnitude < 0.01f || up.sqrMagnitude < 0.01f) return transform.rotation;
            if (Vector3.Dot(forward, transform.forward) < 0f) forward = -forward;
            return Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(forward, up), 60f);
        }
    }
    public Vector3 BackSurface => (spine != null ? spine.position : transform.position + Vector3.up * 1.2f)
        + ChestRotation * new Vector3(0f, 0.04f, -backDepth);

    class Island { public List<int> tris = new List<int>(); public HashSet<int> verts = new HashSet<int>(); public int bone; }

    void Start()
    {
        smr = GetComponentInChildren<SkinnedMeshRenderer>();
        pelvis = FindBone(transform, "Bip001 Pelvis");
        spine = FindBone(transform, "Bip001 Spine1") ?? FindBone(transform, "Bip001 Spine");
        leftShoulder = FindBone(transform, "Bip001 L UpperArm");
        rightShoulder = FindBone(transform, "Bip001 R UpperArm");
        neck = FindBone(transform, "Bip001 Neck");
        owner = GetComponent<PlayerControlSpeed>();
        quiver = ProceduralModels.Spawn("Right Hip Quiver", ProceduralModels.HipQuiverMesh(), ProceduralModels.HipQuiverMaterials()).transform;
        quiver.SetParent(transform, false);
        UpdateQuiver();
        if (smr == null || smr.sharedMesh == null) { Report = "no skinned mesh"; return; }
        try { Analyse(); }
        catch (System.Exception e) { Report = "analysis failed: " + e.Message; Ready = false; }
        Debug.Log("[KnightGear] " + Report);
    }

    static Transform FindBone(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root) { var f = FindBone(c, name); if (f != null) return f; }
        return null;
    }

    void Analyse()
    {
        original = smr.sharedMesh;
        var verts = original.vertices;
        var weights = original.boneWeights;
        var binds = original.bindposes;
        var bones = smr.bones;
        if (weights.Length != verts.Length || bones.Length == 0) { Report = "mesh has no usable skinning"; return; }

        // weld by position so UV seams don't split an island
        var weld = new int[verts.Length];
        var map = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < verts.Length; i++)
        {
            var key = Vector3Int.RoundToInt(verts[i] * 2000f);
            if (!map.TryGetValue(key, out int w)) { w = i; map[key] = i; }
            weld[i] = w;
        }
        var parent = Enumerable.Range(0, verts.Length).ToArray();
        int Root(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int a, int b) { a = Root(a); b = Root(b); if (a != b) parent[a] = b; }

        var allTris = new List<(int sub, int a, int b, int c)>();
        for (int s = 0; s < original.subMeshCount; s++)
        {
            var t = original.GetTriangles(s);
            for (int i = 0; i < t.Length; i += 3)
            {
                allTris.Add((s, t[i], t[i + 1], t[i + 2]));
                Union(weld[t[i]], weld[t[i + 1]]);
                Union(weld[t[i]], weld[t[i + 2]]);
            }
        }

        var islands = new Dictionary<int, Island>();
        for (int i = 0; i < allTris.Count; i++)
        {
            var tri = allTris[i];
            int r = Root(weld[tri.a]);
            if (!islands.TryGetValue(r, out var isl)) islands[r] = isl = new Island();
            isl.tris.Add(i);
            isl.verts.Add(tri.a); isl.verts.Add(tri.b); isl.verts.Add(tri.c);
        }
        foreach (var isl in islands.Values)
        {
            var score = new Dictionary<int, float>();
            foreach (int v in isl.verts)
            {
                var bw = weights[v];
                void Add(int b, float w) { if (w > 0f) score[b] = (score.TryGetValue(b, out float s) ? s : 0f) + w; }
                Add(bw.boneIndex0, bw.weight0); Add(bw.boneIndex1, bw.weight1); Add(bw.boneIndex2, bw.weight2); Add(bw.boneIndex3, bw.weight3);
            }
            isl.bone = score.OrderByDescending(kv => kv.Value).First().Key;
        }

        // measure in the renderer's (world-scaled) space: bone spaces may carry odd scales
        var M = smr.transform.localToWorldMatrix;
        float charHeight = original.bounds.size.y * Mathf.Abs(smr.transform.lossyScale.y);
        Island sword = null, shield = null;
        float bestSword = 0f, bestShield = 0f;
        foreach (var isl in islands.Values)
        {
            string bone = bones[isl.bone] != null ? bones[isl.bone].name : "";
            var pts = isl.verts.Select(v => M.MultiplyPoint3x4(verts[v])).ToList();
            var (_, axes, ext) = Pca(pts);
            float longest = ext[0], flat = ext[2];
            longest *= 2f; flat *= 2f;
            if (bone.Contains("R Hand") && longest > charHeight * 0.22f && longest > bestSword) { sword = isl; bestSword = longest; }
            if ((bone.Contains("L Forearm") || bone.Contains("L Hand")) && longest > charHeight * 0.16f && flat < longest * 0.35f && longest > bestShield) { shield = isl; bestShield = longest; }
        }
        if (sword == null || shield == null)
        {
            Report = $"gear not found as separate islands (islands: {islands.Count}, sword: {sword != null}, shield: {shield != null})";
            return;
        }

        // the knight without his sword and shield
        withoutGear = Instantiate(original);
        withoutGear.name = original.name + " (no gear)";
        var drop = new HashSet<int>(sword.tris.Concat(shield.tris));
        var gearVertices = new HashSet<int>(sword.verts.Concat(shield.verts));
        float rear = -0.06f;
        for (int i = 0; i < verts.Length; i++)
        {
            if (gearVertices.Contains(i)) continue;
            Vector3 hipOffset = M.MultiplyPoint3x4(verts[i]) - (pelvis != null ? pelvis.position : transform.position + Vector3.up * 0.9f);
            if (Mathf.Abs(Vector3.Dot(hipOffset, transform.up)) < 0.15f)
                hipHalfWidth = Mathf.Max(hipHalfWidth, Vector3.Dot(hipOffset, transform.right));
            Vector3 d = M.MultiplyPoint3x4(verts[i]) - (spine != null ? spine.position : transform.position + Vector3.up * 1.2f);
            if (Mathf.Abs(Vector3.Dot(d, transform.up)) < 0.18f && Mathf.Abs(Vector3.Dot(d, transform.right)) < 0.24f)
                rear = Mathf.Min(rear, Vector3.Dot(d, transform.forward));
        }
        backDepth = Mathf.Clamp(-rear, 0.06f, 0.22f);
        hipHalfWidth = Mathf.Clamp(hipHalfWidth, 0.18f, 0.35f);
        for (int s = 0; s < original.subMeshCount; s++)
        {
            var keep = new List<int>();
            for (int i = 0; i < allTris.Count; i++)
            {
                var t = allTris[i];
                if (t.sub != s || drop.Contains(i)) continue;
                keep.Add(t.a); keep.Add(t.b); keep.Add(t.c);
            }
            withoutGear.SetTriangles(keep, s);
        }

        swordProp = BuildProp("Sheathed Sword", sword, allTris, verts, binds, M, true, out swordLength);
        shieldProp = BuildProp("Slung Shield", shield, allTris, verts, binds, M, false, out shieldHeight);
        // The PCA origin is the centroid, not necessarily the bounds centre.
        shieldThickness = Mathf.Max(0f, -shieldProp.GetComponent<MeshFilter>().sharedMesh.bounds.min.z);
        swordProp.gameObject.SetActive(false);
        shieldProp.gameObject.SetActive(false);
        Ready = true;
        Report = $"sword ({sword.verts.Count} verts, {swordLength:F2} m) and shield ({shield.verts.Count} verts) separated from {islands.Count} islands";
    }

    // Bakes an island into a standalone mesh in a canonical frame: +X along its length (hilt -> tip
    // for the sword), Z through its thin axis (outwards for the shield), centred on its middle.
    Transform BuildProp(string name, Island isl, List<(int sub, int a, int b, int c)> tris, Vector3[] verts, Matrix4x4[] binds, Matrix4x4 M, bool tipAwayFromBone, out float length)
    {
        var uvs = original.uv;
        var normals = original.normals;
        var local = new Dictionary<int, int>();
        var pts = isl.verts.Select(v => M.MultiplyPoint3x4(verts[v])).ToList();
        var (centre, axes, ext) = Pca(pts);
        Vector3 x = axes[0], y = axes[1], z = axes[2];
        // orient: sword tip points away from the hand bone; shield face points away from the forearm
        Vector3 boneOrigin = M.MultiplyPoint3x4(binds[isl.bone].inverse.MultiplyPoint3x4(Vector3.zero));
        if (tipAwayFromBone && Vector3.Dot(centre - boneOrigin, x) < 0f) x = -x;
        if (!tipAwayFromBone && Vector3.Dot(centre - boneOrigin, z) < 0f) z = -z;
        y = Vector3.Cross(z, x).normalized;
        var toCanon = Matrix4x4.identity;
        toCanon.SetRow(0, new Vector4(x.x, x.y, x.z, -Vector3.Dot(x, centre)));
        toCanon.SetRow(1, new Vector4(y.x, y.y, y.z, -Vector3.Dot(y, centre)));
        toCanon.SetRow(2, new Vector4(z.x, z.y, z.z, -Vector3.Dot(z, centre)));

        var v2 = new List<Vector3>(); var n2 = new List<Vector3>(); var uv2 = new List<Vector2>(); var t2 = new List<int>();
        foreach (int ti in isl.tris)
        {
            var t = tris[ti];
            foreach (int vi in new[] { t.a, t.b, t.c })
            {
                if (!local.TryGetValue(vi, out int li))
                {
                    li = v2.Count; local[vi] = li;
                    v2.Add(toCanon.MultiplyPoint3x4(M.MultiplyPoint3x4(verts[vi])));
                    n2.Add(toCanon.MultiplyVector(M.MultiplyVector(normals.Length > vi ? normals[vi] : Vector3.up)).normalized);
                    uv2.Add(uvs.Length > vi ? uvs[vi] : Vector2.zero);
                }
                t2.Add(li);
            }
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v2); mesh.SetNormals(n2); mesh.SetUVs(0, uv2); mesh.SetTriangles(t2, 0);
        mesh.RecalculateBounds();
        length = ext[0] * 2f;

        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = smr.sharedMaterial;
        return go.transform;
    }

    // centroid, principal axes (largest first) and half-extents along them
    static (Vector3 centre, Vector3[] axes, float[] ext) Pca(List<Vector3> pts)
    {
        Vector3 c = Vector3.zero;
        foreach (var p in pts) c += p;
        c /= Mathf.Max(1, pts.Count);
        float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        foreach (var p in pts)
        {
            var d = p - c;
            xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z; yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
        }
        // power iteration for the two largest eigenvectors; the third is their cross product
        Vector3 Mul(Vector3 v) => new Vector3(xx * v.x + xy * v.y + xz * v.z, xy * v.x + yy * v.y + yz * v.z, xz * v.x + yz * v.y + zz * v.z);
        Vector3 a = new Vector3(1, 0.3f, 0.2f).normalized;
        for (int i = 0; i < 64; i++) a = Mul(a).normalized;
        Vector3 b = Vector3.ProjectOnPlane(new Vector3(0.2f, 1, 0.4f), a).normalized;
        for (int i = 0; i < 64; i++) b = Vector3.ProjectOnPlane(Mul(b), a).normalized;
        Vector3 n = Vector3.Cross(a, b).normalized;
        var axes = new[] { a, b, n };
        var ext = new float[3];
        foreach (var p in pts)
            for (int k = 0; k < 3; k++) ext[k] = Mathf.Max(ext[k], Mathf.Abs(Vector3.Dot(p - c, axes[k])));
        return (c, axes, ext);
    }

    /// <summary>Bow out: sword on the hip, shield on the back. Sword out: everything back in hand.</summary>
    public void SetStowed(bool stow)
    {
        if (!Ready || stow == stowed) return;
        stowed = stow;
        smr.sharedMesh = stow ? withoutGear : original;
        swordProp.gameObject.SetActive(stow);
        shieldProp.gameObject.SetActive(stow);
    }

    void LateUpdate()
    {
        UpdateQuiver();
        if (!Ready || !stowed) return;
        // keep the props wearing the same (customised) material as the knight
        var mat = smr.sharedMaterial;
        var sr = swordProp.GetComponent<MeshRenderer>();
        if (sr.sharedMaterial != mat) { sr.sharedMaterial = mat; shieldProp.GetComponent<MeshRenderer>().sharedMaterial = mat; }

        Vector3 fwd = transform.forward, right = transform.right, up = Vector3.up;
        Vector3 hip = (pelvis != null ? pelvis.position : transform.position + up) - right * 0.24f + fwd * 0.05f;
        // sword: blade down and back from the left hip, flat against the leg
        Vector3 blade = (-up * 0.85f - fwd * 0.5f).normalized;
        Vector3 flat = -right;
        Vector3 yAxis = Vector3.Cross(flat, blade).normalized;
        swordProp.SetPositionAndRotation(hip + blade * swordLength * 0.32f, Quaternion.LookRotation(flat, yAxis));
        // shield: face outwards on the back, long side up
        Quaternion chest = ChestRotation;
        Vector3 sz = chest * Vector3.back, sx = chest * Vector3.up;
        Vector3 back = BackSurface + sz * (shieldThickness + 0.012f);
        Vector3 sy = Vector3.Cross(sz, sx).normalized;
        shieldProp.SetPositionAndRotation(back, Quaternion.LookRotation(sz, sy));
    }

    void UpdateQuiver()
    {
        if (owner == null || quiver == null) return;
        int capacity = Mathf.Max(1, Mathf.Max(owner.maxArrows, owner.Arrows));
        if (capacity != quiverCapacity)
        {
            while (storedArrows.Count < capacity)
            {
                var arrow = Arrow.Model();
                arrow.name = "Stored Arrow " + (storedArrows.Count + 1);
                arrow.transform.SetParent(quiver, false);
                arrow.transform.localScale = Vector3.one * 0.65f;
                arrow.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                foreach (var renderer in arrow.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                storedArrows.Add(arrow);
            }
            for (int i = 0; i < storedArrows.Count; i++)
            {
                float angle = i * 2.399963f;
                float radius = Mathf.Sqrt((i + 0.5f) / capacity) * 0.095f;
                storedArrows[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0.73f + (i % 4) * 0.012f, Mathf.Sin(angle) * radius);
            }
            quiver.localScale = new Vector3(Mathf.Max(1f, Mathf.Sqrt(capacity / 45f)), 1f, Mathf.Max(1f, Mathf.Sqrt(capacity / 45f)));
            quiverCapacity = capacity;
            shownArrows = -1;
        }
        if (shownArrows != owner.Arrows)
        {
            shownArrows = owner.Arrows;
            for (int i = 0; i < storedArrows.Count; i++) storedArrows[i].SetActive(i < shownArrows);
        }
        Vector3 hip = pelvis != null ? pelvis.position : transform.position + Vector3.up * 0.9f;
        float sideOffset = hipHalfWidth + 0.135f * quiver.localScale.x + 0.015f;
        quiver.SetPositionAndRotation(hip + transform.right * sideOffset - transform.up * 0.4f - transform.forward * 0.04f,
            transform.rotation * Quaternion.Euler(0f, 0f, -10f));
    }
}
