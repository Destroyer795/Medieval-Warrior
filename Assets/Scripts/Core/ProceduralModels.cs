using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Low-poly props built in code so they share the toon look of the characters:
// bow, arrows, supply crates, potions. Used at runtime and by the editor builder.
public static class ProceduralModels
{
    public class MeshKit
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<int>[] subs;
        Vector3 pivot;

        public MeshKit(int submeshes)
        {
            subs = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) subs[i] = new List<int>();
        }

        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c)
        {
            // wind every face away from the current primitive's centre (flat shaded, unique verts)
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - pivot) < 0f) { var tmp = b; b = c; c = tmp; }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            subs[sub].Add(i); subs[sub].Add(i + 1); subs[sub].Add(i + 2);
        }

        void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(sub, a, b, c); Tri(sub, a, c, d); }

        public void Box(int sub, Vector3 center, Vector3 size, Quaternion rot)
        {
            pivot = center;
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rot * new Vector3(x * h.x, y * h.y, z * h.z);
            Quad(sub, P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1));
            Quad(sub, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));
            Quad(sub, P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1));
            Quad(sub, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));
            Quad(sub, P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1));
            Quad(sub, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1));
        }

        public void Box(int sub, Vector3 center, Vector3 size) => Box(sub, center, size, Quaternion.identity);

        public void Tube(int sub, Vector3 from, Vector3 to, float r0, float r1, int sides)
        {
            pivot = (from + to) * 0.5f;
            Vector3 axis = (to - from).normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 d0 = u * Mathf.Cos(a0) + v * Mathf.Sin(a0), d1 = u * Mathf.Cos(a1) + v * Mathf.Sin(a1);
                Quad(sub, from + d0 * r0, from + d1 * r0, to + d1 * r1, to + d0 * r1);
                if (r1 > 0f) Tri(sub, to, to + d0 * r1, to + d1 * r1);
                if (r0 > 0f) Tri(sub, from, from + d1 * r0, from + d0 * r0);
            }
        }

        public void Ball(int sub, Vector3 center, Vector3 radius, int rings = 5, int segs = 8)
        {
            pivot = center;
            for (int r = 0; r < rings; r++)
            {
                float t0 = Mathf.PI * r / rings, t1 = Mathf.PI * (r + 1) / rings;
                for (int s = 0; s < segs; s++)
                {
                    float p0 = Mathf.PI * 2f * s / segs, p1 = Mathf.PI * 2f * (s + 1) / segs;
                    Vector3 S(float t, float p) => center + Vector3.Scale(new Vector3(Mathf.Sin(t) * Mathf.Cos(p), Mathf.Cos(t), Mathf.Sin(t) * Mathf.Sin(p)), radius);
                    if (r == 0) Tri(sub, S(t0, p0), S(t1, p0), S(t1, p1));
                    else if (r == rings - 1) Tri(sub, S(t0, p0), S(t1, p0), S(t0, p1));
                    else Quad(sub, S(t0, p0), S(t1, p0), S(t1, p1), S(t0, p1));
                }
            }
        }

        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

    public static Material Mat(string key, Color color, float smoothness = 0.2f, float metallic = 0f, Color? emission = null)
    {
        if (materials.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(Shader.Find("Standard")) { name = key, color = color, enableInstancing = true };
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        materials[key] = m;
        return m;
    }

    static Mesh Cached(string key, System.Func<Mesh> build)
    {
        if (!meshes.TryGetValue(key, out var m) || m == null) meshes[key] = m = build();
        return m;
    }

    // ------------------------------------------------------------ bow (limbs bend towards +Z, string at -Z)
    public static Mesh BowMesh() => Cached("bow", () =>
    {
        var k = new MeshKit(2); // 0 wood, 1 trim/grip (the string is a live LineRenderer, see ArcherRig)
        const int segs = 10;
        const float half = 0.62f, depth = 0.2f;
        Vector3 Limb(float t) { float y = Mathf.Lerp(-half, half, t); float yy = y / half; return new Vector3(0, y, depth * (1f - yy * yy) - depth * 0.15f * yy * yy * yy * yy); }
        for (int i = 0; i < segs; i++)
        {
            float t0 = i / (float)segs, t1 = (i + 1) / (float)segs;
            float thick = Mathf.Lerp(0.032f, 0.018f, Mathf.Abs(t0 - 0.5f) * 2f);
            k.Tube(0, Limb(t0), Limb(t1), thick, Mathf.Lerp(0.032f, 0.018f, Mathf.Abs(t1 - 0.5f) * 2f), 5);
        }
        k.Tube(1, Limb(0.42f), Limb(0.58f), 0.042f, 0.042f, 6);           // leather grip
        k.Ball(1, Limb(0f), Vector3.one * 0.03f, 3, 5);                  // nocks
        k.Ball(1, Limb(1f), Vector3.one * 0.03f, 3, 5);
        return k.Build("Bow");
    });

    public static Material[] BowMaterials(int finish)
    {
        var f = GameSettings.BowFinishes[Mathf.Clamp(finish, 0, GameSettings.BowFinishes.Length - 1)];
        return new[]
        {
            Mat("bow_wood_" + finish, f.wood, 0.35f),
            Mat("bow_trim_" + finish, f.trim, 0.5f, 0.3f),
        };
    }

    // ------------------------------------------------------------ arrow pointing +Z, origin at the tail
    public static Mesh ArrowMesh() => Cached("arrow", () =>
    {
        var k = new MeshKit(3); // 0 shaft, 1 head, 2 fletching
        k.Tube(0, Vector3.zero, new Vector3(0, 0, 0.78f), 0.011f, 0.011f, 5);
        k.Tube(1, new Vector3(0, 0, 0.76f), new Vector3(0, 0, 0.9f), 0.03f, 0f, 4);
        for (int i = 0; i < 3; i++)
        {
            Quaternion r = Quaternion.Euler(0, 0, i * 120f);
            k.Box(2, new Vector3(0, 0, 0.08f) + r * new Vector3(0, 0.028f, 0), new Vector3(0.004f, 0.05f, 0.14f), r);
        }
        return k.Build("Arrow");
    });

    public static Material[] ArrowMaterials() => new[]
    {
        Mat("arrow_shaft", new Color(0.72f, 0.55f, 0.34f), 0.2f),
        Mat("arrow_head", new Color(0.78f, 0.8f, 0.85f), 0.7f, 0.8f),
        Mat("arrow_fletch", new Color(0.85f, 0.15f, 0.12f), 0.1f),
    };

    // ------------------------------------------------------------ supply crate with an arrow bundle
    public static Mesh CrateMesh() => Cached("crate", () =>
    {
        var k = new MeshKit(2); // 0 planks, 1 iron
        Vector3 size = new Vector3(1.0f, 0.62f, 0.7f);
        k.Box(0, new Vector3(0, size.y / 2f, 0), size * 0.97f);
        for (int i = 0; i < 4; i++)
        {
            float y = 0.08f + i * 0.155f;
            k.Box(0, new Vector3(0, y, size.z / 2f), new Vector3(size.x * 0.98f, 0.13f, 0.03f));
            k.Box(0, new Vector3(0, y, -size.z / 2f), new Vector3(size.x * 0.98f, 0.13f, 0.03f));
        }
        foreach (float x in new[] { -0.47f, 0.47f })
            foreach (float z in new[] { -0.32f, 0.32f })
                k.Box(1, new Vector3(x, size.y / 2f, z), new Vector3(0.08f, size.y + 0.02f, 0.08f));
        k.Box(1, new Vector3(0, size.y + 0.005f, 0), new Vector3(size.x + 0.02f, 0.03f, 0.08f));
        return k.Build("SupplyCrate");
    });

    public static Material[] CrateMaterials() => new[]
    {
        Mat("crate_wood", new Color(0.62f, 0.42f, 0.24f), 0.15f),
        Mat("crate_iron", new Color(0.3f, 0.3f, 0.32f), 0.45f, 0.6f),
    };

    public static Mesh ArrowBundleMesh() => Cached("bundle", () =>
    {
        var k = new MeshKit(3);
        for (int i = 0; i < 7; i++)
        {
            float a = i * 0.9f, r = i == 0 ? 0f : 0.045f;
            Vector3 off = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
            Quaternion lean = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-3f, 3f), 0);
            Vector3 tail = off + new Vector3(0, 0, -0.42f), tip = tail + lean * new Vector3(0, 0, 0.84f);
            k.Tube(0, tail, tip, 0.011f, 0.011f, 4);
            k.Tube(1, tip - (tip - tail).normalized * 0.02f, tip + (tip - tail).normalized * 0.11f, 0.026f, 0f, 4);
            k.Box(2, tail + new Vector3(0, 0, 0.07f), new Vector3(0.05f, 0.004f, 0.12f), Quaternion.Euler(0, 0, a * 57f));
        }
        k.Tube(1, new Vector3(0, 0, -0.04f), new Vector3(0, 0, 0.04f), 0.085f, 0.085f, 8); // strap
        return k.Build("ArrowBundle");
    });

    public static Material[] BundleMaterials() => new[]
    {
        Mat("arrow_shaft", new Color(0.72f, 0.55f, 0.34f), 0.2f),
        Mat("bundle_strap", new Color(0.36f, 0.2f, 0.12f), 0.2f),
        Mat("arrow_fletch", new Color(0.85f, 0.15f, 0.12f), 0.1f),
    };

    public static Mesh PotionMesh() => Cached("potion", () =>
    {
        var k = new MeshKit(3); // 0 glass/liquid, 1 neck, 2 cork
        k.Ball(0, new Vector3(0, 0.13f, 0), new Vector3(0.12f, 0.12f, 0.12f), 6, 10);
        k.Tube(1, new Vector3(0, 0.22f, 0), new Vector3(0, 0.33f, 0), 0.04f, 0.035f, 8);
        k.Tube(2, new Vector3(0, 0.32f, 0), new Vector3(0, 0.38f, 0), 0.042f, 0.038f, 8);
        return k.Build("Potion");
    });

    public static Material[] PotionMaterials() => new[]
    {
        Mat("potion_liquid", new Color(0.85f, 0.08f, 0.12f), 0.9f, 0f, new Color(0.6f, 0.02f, 0.04f)),
        Mat("potion_glass", new Color(0.75f, 0.9f, 0.95f), 0.9f),
        Mat("potion_cork", new Color(0.55f, 0.36f, 0.2f), 0.1f),
    };

    public static GameObject Spawn(string name, Mesh mesh, Material[] mats, Transform parent = null, bool shadows = true)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mats;
        mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        return go;
    }
}
