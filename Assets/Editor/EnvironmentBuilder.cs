using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

// Builds the world around the castle: textured terrain that stretches past the walls,
// buildings grounded into the soil, foliage, sky/lighting and post-processing.
// Re-runnable: everything it generates lives under "Environment" (Background scene)
// and is replaced on every run.
//
// Menu: Medieval Warrior > Rebuild Environment
// Batch: Unity -batchmode -projectPath . -executeMethod EnvironmentBuilder.BuildBatch [-shotDir <folder>]
public static class EnvironmentBuilder
{
    const string GameScene = "Assets/Scenes/Semi-Final.unity";
    const string WorldScene = "Assets/Scenes/Background.unity";
    const string Root = "Assets/Environment";
    const string TexDir = Root + "/Textures";
    const string GenDir = Root + "/Generated";

    // Terrain layout (world units). The castle sits on a flat plain at y = 0.
    // Centre and radius are measured from the walls; everything else derives from them.
    public static Vector2 Center;
    public static float CastleRadius;
    static float TerrainSize;
    const float TerrainHeight = 120f;
    const float TerrainBase = 16f;     // world y = 0 is this far above the terrain floor
    static float FlatRadius;           // perfectly level play area
    static float PlainRadius;          // gentle undulation up to here, hills beyond
    const int HeightRes = 1025;
    const int AlphaRes = 2048;
    const int DetailRes = 1024;
    static Vector2[][] Roads;

    enum L { GrassLush, GrassMeadow, Dirt, Cobble, Rock }

    class Building
    {
        public GameObject go;
        public string kind;
        public Vector2 center;      // footprint centre (world XZ)
        public Vector2 axisX, axisZ; // footprint axes (world XZ, unit)
        public Vector2 half;        // footprint half extents
        public float bottom;        // lowest world y of the meshes
        public float height;

        public float SignedDistance(Vector2 p)
        {
            Vector2 d = p - center;
            float qx = Mathf.Abs(Vector2.Dot(d, axisX)) - half.x;
            float qz = Mathf.Abs(Vector2.Dot(d, axisZ)) - half.y;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qz, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qz), 0f);
        }
    }

    static Random rng;
    static List<Building> buildings;
    static List<Vector2> wallHull;
    static float houseHeight = 3f;


    [MenuItem("Medieval Warrior/Rebuild Environment")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
        BuildMenu();
        EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        EditorSceneManager.OpenScene(WorldScene, OpenSceneMode.Additive);
    }

    static Material builtSky;

    static void BuildMenu()
    {
        GameplayBuilder.SetupMenuScene(Center, CastleRadius, builtSky != null ? builtSky : CreateSky(), ApplyLighting);
    }

    public static void BuildBatch()
    {
        try
        {
            Build();
            string shotDir = GetArg("-shotDir");
            if (!string.IsNullOrEmpty(shotDir)) CaptureShots(shotDir);
            BuildMenu();
            Debug.Log("[EnvironmentBuilder] DONE");
            if (GetArg("-smoke") != null) { SmokeTest.Begin(GetArg("-smoke")); return; }
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    public static void Build()
    {
        rng = new Random(1337);
        EnsureFolder(GenDir);
        GameplayBuilder.SetupAssets();
        ConfigureTextureImports();
        ApplyCastleGrade();
        ConfigureTextureImports();

        Scene game = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        Scene world = EditorSceneManager.OpenScene(WorldScene, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(world);

        foreach (GameObject old in world.GetRootGameObjects().Where(g => g.name == "Environment")) Object.DestroyImmediate(old);
        var env = new GameObject("Environment");
        SceneManager.MoveGameObjectToScene(env, world);

        var mats = CreateMaterials();
        var prefabs = CreateFoliagePrefabs(mats);

        CollectBuildings(world);
        DeriveLayout();
        GroundBuildings();

        Terrain terrain = CreateTerrain(env.transform, prefabs);
        CreateFoundations(env.transform, mats);
        ScatterBaseProps(env.transform, prefabs);
        PlaceSupplyCrates(env.transform);
        TameTorchLights(world);

        SceneManager.SetActiveScene(game);
        Material sky = builtSky = CreateSky();
        ApplyLighting(game, sky);
        ApplyLighting(world, sky);
        SetupCameraAndPostFx(game);
        SetupQuality();
        GameplayBuilder.SetupGameScene(game);
        PlacePlayerStart(game);
        BakeNavMeshAndSpawns(game, terrain);

        EditorSceneManager.MarkSceneDirty(world);
        EditorSceneManager.MarkSceneDirty(game);
        EditorSceneManager.SaveScene(world);
        EditorSceneManager.SaveScene(game);
        AssetDatabase.SaveAssets();
        Debug.Log($"[EnvironmentBuilder] Built terrain, {buildings.Count} grounded buildings.");
    }

    // ------------------------------------------------------------------ assets

    static void ConfigureTextureImports()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture", new[] { TexDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            string file = Path.GetFileNameWithoutExtension(path);
            bool dirty = false;
            void Set<T>(T current, T wanted, Action apply) { if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(); dirty = true; } }

            if (file.EndsWith("_N")) Set(ti.textureType, TextureImporterType.NormalMap, () => ti.textureType = TextureImporterType.NormalMap);
            if (file.StartsWith("Sky"))
            {
                Set(ti.textureShape, TextureImporterShape.TextureCube, () => ti.textureShape = TextureImporterShape.TextureCube);
                Set(ti.maxTextureSize, 2048, () => ti.maxTextureSize = 2048);
            }
            if (file.StartsWith("Grass") && !file.Contains("_") || file.StartsWith("Flowers"))
            {
                Set(ti.alphaIsTransparency, true, () => ti.alphaIsTransparency = true);
                Set(ti.wrapMode, TextureWrapMode.Clamp, () => ti.wrapMode = TextureWrapMode.Clamp);
            }
            if (dirty) ti.SaveAndReimport();
        }
    }

    static T Tex<T>(string name) where T : Texture
    {
        string guid = AssetDatabase.FindAssets(name, new[] { TexDir }).FirstOrDefault(g => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name);
        var t = guid == null ? null : AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
        if (t == null) throw new Exception("Missing texture " + name);
        return t;
    }

    static Material SaveMaterial(string name, Color color, float smoothness, Texture2D albedo = null, Texture2D normal = null, Vector2? tiling = null)
    {
        string path = $"{GenDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = color;
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.mainTexture = albedo;
        if (tiling.HasValue) m.mainTextureScale = tiling.Value;
        if (normal != null)
        {
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    class Mats { public Material bark, leaf, leafLight, pine, autumn, bush, rock, foundation; }

    static Mats CreateMaterials()
    {
        return new Mats
        {
            bark = SaveMaterial("Bark", new Color(0.45f, 0.31f, 0.2f), 0.05f),
            leaf = SaveMaterial("Leaves", new Color(0.34f, 0.62f, 0.2f), 0.12f),
            leafLight = SaveMaterial("LeavesLight", new Color(0.55f, 0.76f, 0.26f), 0.12f),
            pine = SaveMaterial("PineNeedles", new Color(0.17f, 0.42f, 0.22f), 0.08f),
            autumn = SaveMaterial("LeavesAutumn", new Color(0.93f, 0.6f, 0.2f), 0.12f),
            bush = SaveMaterial("Bush", new Color(0.3f, 0.56f, 0.2f), 0.1f),
            rock = SaveMaterial("Rock", new Color(0.86f, 0.86f, 0.82f), 0.15f, Tex<Texture2D>("Rock_Mossy_D"), Tex<Texture2D>("Rock_Mossy_N")),
            foundation = SaveMaterial("FoundationStone", new Color(0.92f, 0.88f, 0.82f), 0.1f, Tex<Texture2D>("Cobble_D"), Tex<Texture2D>("Cobble_N")),
        };
    }

    // ------------------------------------------------------------------ procedural low-poly meshes

    class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] subs;
        Vector3 pivot; // faces are wound to point away from this
        public MeshBuilder(int submeshes) { subs = Enumerable.Range(0, submeshes).Select(_ => new List<int>()).ToArray(); }

        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c)
        {
            // flat shaded: unique vertices, planar UV on the dominant axis
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - pivot) < 0f) { Vector3 tmp = b; b = c; c = tmp; n = -n; }
            Vector3 an = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            Func<Vector3, Vector2> uv = an.y >= an.x && an.y >= an.z ? (Func<Vector3, Vector2>)(p => new Vector2(p.x, p.z))
                : an.x >= an.z ? (p => new Vector2(p.z, p.y)) : (Func<Vector3, Vector2>)(p => new Vector2(p.x, p.y));
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(uv(a)); uvs.Add(uv(b)); uvs.Add(uv(c));
            subs[sub].Add(i); subs[sub].Add(i + 1); subs[sub].Add(i + 2);
        }

        public void Blob(int sub, Vector3 center, Vector3 radius, float roughness)
        {
            pivot = center;
            var (v, f) = Icosphere();
            var jitter = v.Select(p =>
            {
                float n = 1f + roughness * ((float)rng.NextDouble() * 2f - 1f);
                return center + Vector3.Scale(p * n, radius);
            }).ToArray();
            for (int i = 0; i < f.Length; i += 3) Tri(sub, jitter[f[i]], jitter[f[i + 1]], jitter[f[i + 2]]);
        }

        public void Cone(int sub, Vector3 baseCenter, float radius, float height, int sides)
        {
            Vector3 tip = baseCenter + Vector3.up * height;
            pivot = baseCenter + Vector3.up * height * 0.3f;
            float twist = (float)rng.NextDouble() * Mathf.PI;
            for (int i = 0; i < sides; i++)
            {
                Vector3 p0 = baseCenter + Ring(i, sides, twist) * radius;
                Vector3 p1 = baseCenter + Ring(i + 1, sides, twist) * radius;
                Tri(sub, p0, tip, p1);
                Tri(sub, p1, baseCenter, p0);
            }
        }

        public void Cylinder(int sub, Vector3 baseCenter, float r0, float r1, float height, int sides)
        {
            pivot = baseCenter + Vector3.up * height * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = baseCenter + Ring(i, sides, 0) * r0, b = baseCenter + Ring(i + 1, sides, 0) * r0;
                Vector3 c = baseCenter + Vector3.up * height + Ring(i, sides, 0) * r1, d = baseCenter + Vector3.up * height + Ring(i + 1, sides, 0) * r1;
                Tri(sub, a, c, b);
                Tri(sub, b, c, d);
            }
        }

        public void Box(int sub, Vector3 min, Vector3 max, float uvScale)
        {
            Vector3[] c =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
            };
            pivot = (min + max) * 0.5f * uvScale;
            int[] q = { 0, 3, 2, 1, 1, 2, 6, 5, 5, 6, 7, 4, 4, 7, 3, 0, 3, 7, 6, 2 };
            for (int i = 0; i < q.Length; i += 4)
            {
                Tri(sub, c[q[i]] * uvScale, c[q[i + 1]] * uvScale, c[q[i + 2]] * uvScale);
                Tri(sub, c[q[i]] * uvScale, c[q[i + 2]] * uvScale, c[q[i + 3]] * uvScale);
            }
            // scale back positions, UVs keep the world-sized tiling
            for (int i = verts.Count - 30; i < verts.Count; i++) verts[i] /= uvScale;
        }

        static Vector3 Ring(int i, int n, float offset)
        {
            float a = offset + i * Mathf.PI * 2f / n;
            return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }

    static (Vector3[] v, int[] f) cachedIco;

    static (Vector3[] v, int[] f) Icosphere()
    {
        if (cachedIco.v != null) return cachedIco;
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        }.Select(p => p.normalized).ToList();
        var f = new List<int> { 0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8, 3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1 };
        // one subdivision -> 80 faces: chunky, stylised
        var mid = new Dictionary<long, int>();
        int Mid(int a, int b)
        {
            long key = Math.Min(a, b) * 1000L + Math.Max(a, b);
            if (mid.TryGetValue(key, out int i)) return i;
            v.Add(((v[a] + v[b]) * 0.5f).normalized);
            return mid[key] = v.Count - 1;
        }
        var f2 = new List<int>();
        for (int i = 0; i < f.Count; i += 3)
        {
            int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
            f2.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        return cachedIco = (v.ToArray(), f2.ToArray());
    }

    class Prefabs { public GameObject[] trees; public GameObject[] bushes; public GameObject[] rocks; }

    static GameObject SavePrefab(string name, Mesh mesh, Material[] materials, bool collider, float colliderRadius = 0.2f, float colliderHeight = 2f)
    {
        string meshPath = $"{GenDir}/{name}_Mesh.asset";
        mesh = SaveMesh(mesh, meshPath);

        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = materials;
        mr.shadowCastingMode = ShadowCastingMode.On;
        if (collider)
        {
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = colliderRadius;
            cc.height = colliderHeight;
            cc.center = new Vector3(0, colliderHeight / 2f, 0);
        }
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{GenDir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    static Prefabs CreateFoliagePrefabs(Mats m)
    {
        var trees = new List<GameObject>();
        // Pines: stacked cones
        for (int k = 0; k < 2; k++)
        {
            var mb = new MeshBuilder(2);
            mb.Cylinder(0, Vector3.zero, 0.16f, 0.1f, 1.0f, 6);
            float y = 0.6f, r = 1.25f;
            for (int i = 0; i < 4; i++) { mb.Cone(1, new Vector3(R(-.05f, .05f), y, R(-.05f, .05f)), r, 1.5f - i * 0.12f, 8); y += 0.75f; r *= 0.74f; }
            trees.Add(SavePrefab("Tree_Pine_" + k, mb.ToMesh("Pine"), new[] { m.bark, m.pine }, true, 0.18f, 3f));
        }
        // Round broadleaf trees (green, light green, one autumn accent)
        Material[] crowns = { m.leaf, m.leafLight, m.leaf, m.autumn };
        for (int k = 0; k < crowns.Length; k++)
        {
            var mb = new MeshBuilder(2);
            mb.Cylinder(0, Vector3.zero, 0.2f, 0.13f, 1.9f, 6);
            mb.Cylinder(0, new Vector3(0, 1.2f, 0), 0.09f, 0.05f, 0.9f, 5);
            mb.Blob(1, new Vector3(0, 2.4f, 0), new Vector3(1.25f, 1.05f, 1.25f), 0.12f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + R(0, 1);
                mb.Blob(1, new Vector3(Mathf.Cos(a) * 0.75f, 2.1f + R(-0.2f, 0.5f), Mathf.Sin(a) * 0.75f), Vector3.one * R(0.7f, 0.9f), 0.14f);
            }
            trees.Add(SavePrefab("Tree_Round_" + k, mb.ToMesh("RoundTree"), new[] { m.bark, crowns[k] }, true, 0.22f, 2f));
        }

        var bushes = new List<GameObject>();
        Material[] bushMats = { m.bush, m.leafLight, m.leaf };
        for (int k = 0; k < 3; k++)
        {
            var mb = new MeshBuilder(1);
            int blobs = 2 + k;
            for (int i = 0; i < blobs; i++)
                mb.Blob(0, new Vector3(R(-0.35f, 0.35f), R(0.2f, 0.32f), R(-0.35f, 0.35f)), new Vector3(R(0.38f, 0.5f), R(0.3f, 0.42f), R(0.38f, 0.5f)), 0.15f);
            bushes.Add(SavePrefab("Bush_" + k, mb.ToMesh("Bush"), new[] { bushMats[k] }, false));
        }

        var rocks = new List<GameObject>();
        for (int k = 0; k < 3; k++)
        {
            var mb = new MeshBuilder(1);
            mb.Blob(0, new Vector3(0, 0.18f, 0), new Vector3(R(0.5f, 0.7f), R(0.32f, 0.45f), R(0.45f, 0.6f)), 0.28f);
            rocks.Add(SavePrefab("Rock_" + k, mb.ToMesh("Rock"), new[] { m.rock }, false));
        }
        return new Prefabs { trees = trees.ToArray(), bushes = bushes.ToArray(), rocks = rocks.ToArray() };
    }

    // ------------------------------------------------------------------ buildings

    static string KindOf(GameObject go)
    {
        var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
        string n = src != null ? src.name : go.name;
        if (n.StartsWith("House")) return "House";
        if (n.StartsWith("Tower")) return "Tower";
        if (n.StartsWith("Wall")) return "Wall";
        if (n.StartsWith("Castle")) return "Castle";
        if (n.StartsWith("Bridge")) return "Bridge";
        if (n.StartsWith("Column")) return "Column";
        return null;
    }

    static void CollectBuildings(Scene world)
    {
        buildings = new List<Building>();
        // pieces are nested (everything sits under a scaled "Castle" group): walk down to prefab roots
        var pieces = new List<(GameObject go, string kind)>();
        void Walk(Transform t)
        {
            if (t.name == "Environment") return;
            bool prefabRoot = PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject);
            string k = KindOf(t.gameObject);
            if (k != null && (prefabRoot || k == "Column" && t.GetComponent<MeshFilter>() != null)) { pieces.Add((t.gameObject, k)); return; }
            foreach (Transform c in t) Walk(c);
        }
        foreach (GameObject r in world.GetRootGameObjects()) Walk(r.transform);
        foreach (var (go, kind) in pieces)
        {
            var filters = go.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).ToArray();
            if (filters.Length == 0) continue;

            // world-space vertices, measured in the building's own rotation frame
            Quaternion inv = Quaternion.Inverse(Quaternion.Euler(0, go.transform.eulerAngles.y, 0));
            var pts = new List<Vector3>();
            foreach (var f in filters)
            {
                var v = f.sharedMesh.vertices;
                for (int i = 0; i < v.Length; i++) pts.Add(f.transform.TransformPoint(v[i]));
            }
            float bottom = pts.Min(p => p.y), top = pts.Max(p => p.y);
            // footprint = the lowest 40cm slice, so roof overhangs don't inflate it
            var slice = pts.Where(p => p.y < bottom + 0.4f).Select(p => inv * (p - go.transform.position)).ToList();
            float minX = slice.Min(p => p.x), maxX = slice.Max(p => p.x), minZ = slice.Min(p => p.z), maxZ = slice.Max(p => p.z);
            Vector3 localCenter = new Vector3((minX + maxX) / 2f, 0, (minZ + maxZ) / 2f);
            Quaternion rot = Quaternion.Euler(0, go.transform.eulerAngles.y, 0);
            Vector3 c = go.transform.position + rot * localCenter;
            Vector3 ax = rot * Vector3.right, az = rot * Vector3.forward;
            buildings.Add(new Building
            {
                go = go, kind = kind, bottom = bottom, height = top - bottom,
                center = new Vector2(c.x, c.z),
                axisX = new Vector2(ax.x, ax.z).normalized, axisZ = new Vector2(az.x, az.z).normalized,
                half = new Vector2((maxX - minX) / 2f, (maxZ - minZ) / 2f),
            });
        }
        var houses = buildings.Where(b => b.kind == "House").Select(b => b.height).OrderBy(h => h).ToList();
        if (houses.Count > 0) houseHeight = houses[houses.Count / 2];
        wallHull = ConvexHull(buildings.Where(b => b.kind == "Wall" || b.kind == "Tower").Select(b => b.center).ToList());
        Debug.Log($"[EnvironmentBuilder] {buildings.Count} buildings, median house height {houseHeight:F2}, hull {wallHull.Count} pts");
        foreach (var b in buildings.Where(b => b.kind != "Column"))
            Debug.Log($"[EnvironmentBuilder]   {b.go.name} ({b.kind}) bottom={b.bottom:F2} h={b.height:F2} half={b.half}");
    }

    // Walk round the wall ring looking for openings wide enough to walk through; roads lead to them.
    static List<float> FindGateAngles()
    {
        var open = new List<(float angle, bool open)>();
        for (int deg = 0; deg < 360; deg += 2)
        {
            Vector2 dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
            Vector2 p = Center;
            for (float r = 0; r < CastleRadius * 1.5f; r += 0.5f)
            {
                p = Center + dir * r;
                if (!InsideWalls(p, 0f)) break;
            }
            bool blocked = Physics.CheckSphere(new Vector3(p.x, 1.6f, p.y), 1.4f, ~0, QueryTriggerInteraction.Ignore)
                        || Physics.CheckSphere(new Vector3(p.x - dir.x * 2f, 1.6f, p.y - dir.y * 2f), 1.4f, ~0, QueryTriggerInteraction.Ignore);
            open.Add((deg, !blocked));
        }
        // group consecutive open samples into gaps, widest first
        var gaps = new List<(float center, int width)>();
        int n = open.Count;
        int startIdx = open.FindIndex(o => !o.open);
        if (startIdx < 0) return new List<float>();
        int run = 0; float runStart = 0;
        for (int k = 1; k <= n; k++)
        {
            var o = open[(startIdx + k) % n];
            if (o.open) { if (run == 0) runStart = o.angle; run++; }
            else if (run > 0) { gaps.Add((runStart + (run - 1) * 1f, run)); run = 0; }
        }
        return gaps.Where(g => g.width >= 2).OrderByDescending(g => g.width).Select(g => g.center).Take(2).ToList();
    }

    static void DeriveLayout()
    {
        var ring = wallHull.Count >= 3 ? wallHull : buildings.Select(b => b.center).ToList();
        Center = new Vector2(ring.Average(p => p.x), ring.Average(p => p.y));
        CastleRadius = buildings.Max(b => (b.center - Center).magnitude + b.half.magnitude * 0.5f);
        FlatRadius = CastleRadius + 22f;
        PlainRadius = CastleRadius + 55f;
        TerrainSize = Mathf.Ceil((CastleRadius + 260f) * 2f / 50f) * 50f;
        // two dirt roads leaving the castle, winding off towards the mountains
        Vector2[] Road(float angleDeg, float wiggle)
        {
            var pts = new List<Vector2>();
            for (float r = CastleRadius * 0.55f; r < TerrainSize * 0.6f; r += 30f)
            {
                float a = (angleDeg + Mathf.Sin(r * 0.025f) * wiggle) * Mathf.Deg2Rad;
                pts.Add(Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return pts.ToArray();
        }
        var gates = FindGateAngles();
        float a0 = gates.Count > 0 ? gates[0] : -62f;
        float a1 = gates.Count > 1 ? gates[1] : a0 + 180f;
        Roads = new[] { Road(a0, 9f), Road(a1, 7f) };
        Debug.Log($"[EnvironmentBuilder] gates at {string.Join(", ", gates.Select(g => g.ToString("F0")))} deg");
        Debug.Log($"[EnvironmentBuilder] centre {Center} castle radius {CastleRadius:F1} terrain {TerrainSize}");
    }

    // Sink anything that stands on the ground slightly into it, so no base edge floats.
    static void GroundBuildings()
    {
        const float sink = 0.12f;
        foreach (var b in buildings)
        {
            if (b.bottom > 1.0f || b.bottom < -0.6f) continue; // mounted on another piece / already buried
            float delta = -sink - b.bottom;
            if (Mathf.Abs(delta) < 0.01f) continue;
            if (delta > 0f && b.bottom > -sink - 0.05f) continue; // never raise
            Undo.RecordObject(b.go.transform, "Ground building");
            b.go.transform.position += Vector3.up * delta;
            b.bottom += delta;
        }
    }

    static bool InsideWalls(Vector2 p, float margin = 0f)
    {
        if (wallHull.Count < 3) return false;
        for (int i = 0; i < wallHull.Count; i++)
        {
            Vector2 a = wallHull[i], b = wallHull[(i + 1) % wallHull.Count];
            Vector2 e = b - a;
            float cross = e.x * (p.y - a.y) - e.y * (p.x - a.x);
            if (cross / e.magnitude < margin) return false;
        }
        return true;
    }

    static List<Vector2> ConvexHull(List<Vector2> pts)
    {
        pts = pts.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
        if (pts.Count < 3) return pts;
        float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        var h = new List<Vector2>();
        foreach (var p in pts) { while (h.Count >= 2 && Cross(h[h.Count - 2], h[h.Count - 1], p) <= 0) h.RemoveAt(h.Count - 1); h.Add(p); }
        int lower = h.Count + 1;
        for (int i = pts.Count - 2; i >= 0; i--) { var p = pts[i]; while (h.Count >= lower && Cross(h[h.Count - 2], h[h.Count - 1], p) <= 0) h.RemoveAt(h.Count - 1); h.Add(p); }
        h.RemoveAt(h.Count - 1);
        return h; // counter-clockwise
    }

    static float NearestBuilding(Vector2 p, out Building nearest)
    {
        nearest = null;
        float best = float.MaxValue;
        foreach (var b in buildings)
        {
            if ((p - b.center).sqrMagnitude > (b.half.magnitude + 8f) * (b.half.magnitude + 8f)) continue;
            float d = b.SignedDistance(p);
            if (d < best) { best = d; nearest = b; }
        }
        return best;
    }

    // ------------------------------------------------------------------ terrain

    static float Fbm(float x, float z, int octaves)
    {
        float sum = 0, amp = 1, freq = 1, norm = 0;
        for (int i = 0; i < octaves; i++) { sum += amp * Mathf.PerlinNoise(x * freq + 31.7f * i, z * freq - 12.3f * i); norm += amp; amp *= 0.5f; freq *= 2.03f; }
        return sum / norm;
    }

    static float RoadDistance(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var road in Roads)
            for (int i = 0; i < road.Length - 1; i++)
            {
                Vector2 a = road[i], b = road[i + 1], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
            }
        // wobble the edge so it doesn't look ruler-straight
        return best + (Mathf.PerlinNoise(p.x * 0.06f, p.y * 0.06f) - 0.5f) * 3f;
    }

    static float WorldHeight(float x, float z)
    {
        Vector2 p = new Vector2(x, z);
        float d = (p - Center).magnitude;
        float h = 0f;
        // soft meadow undulation outside the playable core
        h += Mathf.SmoothStep(0, 1, Mathf.InverseLerp(FlatRadius, PlainRadius, d)) * (Fbm(x * 0.03f, z * 0.03f, 3) - 0.35f) * 2.2f;
        // rolling hills
        float hills = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(PlainRadius, PlainRadius + 70f, d));
        h += hills * (Fbm(x * 0.01f + 5f, z * 0.01f + 9f, 4) * 20f);
        // mountain ring that frames the valley
        float mountains = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(PlainRadius + 110f, PlainRadius + 190f, d));
        float ridge = 1f - Mathf.Abs(Fbm(x * 0.007f, z * 0.007f, 4) * 2f - 1f);
        h += mountains * (30f + ridge * 45f);
        // roads cut gently through the hills
        float road = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4f, 18f, RoadDistance(p)));
        h = Mathf.Lerp(h, Mathf.Min(h, h * 0.35f), road);
        return Mathf.Max(h, -2f);
    }

    static Terrain CreateTerrain(Transform parent, Prefabs prefabs)
    {
        string dataPath = GenDir + "/CastleTerrain.asset";
        // reuse the existing asset so its GUID (and the scene reference) stays stable between rebuilds
        var td = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
        if (td == null) { td = new TerrainData(); AssetDatabase.CreateAsset(td, dataPath); }
        td.heightmapResolution = HeightRes;
        td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
        Vector3 origin = new Vector3(Center.x - TerrainSize / 2f, -TerrainBase, Center.y - TerrainSize / 2f);

        var heights = new float[HeightRes, HeightRes];
        for (int iz = 0; iz < HeightRes; iz++)
            for (int ix = 0; ix < HeightRes; ix++)
            {
                float x = origin.x + ix / (float)(HeightRes - 1) * TerrainSize;
                float z = origin.z + iz / (float)(HeightRes - 1) * TerrainSize;
                heights[iz, ix] = (WorldHeight(x, z) + TerrainBase) / TerrainHeight;
            }
        td.SetHeights(0, 0, heights);

        // layers
        TerrainLayer Layer(string name, string tex, float tile, Color tint)
        {
            string path = $"{GenDir}/{name}.terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (l == null) { l = new TerrainLayer(); AssetDatabase.CreateAsset(l, path); }
            l.diffuseTexture = Tex<Texture2D>(tex + "_D");
            l.normalMapTexture = Tex<Texture2D>(tex + "_N");
            l.tileSize = new Vector2(tile, tile);
            l.normalScale = 1f;
            l.smoothness = 0f;
            l.metallic = 0f;
            l.diffuseRemapMax = tint;
            EditorUtility.SetDirty(l);
            return l;
        }
        td.terrainLayers = new[]
        {
            Layer("Layer_GrassLush", "Grass_Lush", 7f, Color.white),
            Layer("Layer_GrassMeadow", "Grass_Meadow", 9f, Color.white),
            Layer("Layer_Dirt", "Dirt_Path", 5f, Color.white),
            Layer("Layer_Cobble", "Cobble", 3f, Color.white),
            Layer("Layer_Rock", "Rock_Mossy", 10f, Color.white),
        };

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain";
        go.transform.SetParent(parent, false);
        go.transform.position = origin;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic | StaticEditorFlags.OccludeeStatic);
        var terrain = go.GetComponent<Terrain>();
        terrain.heightmapPixelError = 3f;
        terrain.basemapDistance = 250f;
        terrain.drawInstanced = true;
        terrain.treeDistance = 450f;
        terrain.treeBillboardDistance = 450f;
        terrain.detailObjectDistance = 85f;
        terrain.detailObjectDensity = 1f;
        terrain.shadowCastingMode = ShadowCastingMode.TwoSided;

        PaintTerrain(td, origin);
        PlaceDetails(td, origin);
        PlaceTrees(td, origin, prefabs);
        terrain.Flush();
        EditorUtility.SetDirty(td);
        return terrain;
    }

    static Vector2 AlphaToWorld(int ix, int iz, int res, Vector3 origin) =>
        new Vector2(origin.x + (ix + 0.5f) / res * TerrainSize, origin.z + (iz + 0.5f) / res * TerrainSize);

    static float[,,] splat;

    static void PaintTerrain(TerrainData td, Vector3 origin)
    {
        td.alphamapResolution = AlphaRes;
        int layers = td.terrainLayers.Length;
        splat = new float[AlphaRes, AlphaRes, layers];
        var w = new float[layers];
        for (int iz = 0; iz < AlphaRes; iz++)
            for (int ix = 0; ix < AlphaRes; ix++)
            {
                Vector2 p = AlphaToWorld(ix, iz, AlphaRes, origin);
                float nx = (ix + 0.5f) / AlphaRes, nz = (iz + 0.5f) / AlphaRes;
                float slope = td.GetSteepness(nx, nz);
                float n1 = Fbm(p.x * 0.045f, p.y * 0.045f, 3);
                float n2 = Mathf.PerlinNoise(p.x * 0.21f + 100f, p.y * 0.21f + 100f);

                Array.Clear(w, 0, layers);
                float meadow = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.42f, 0.62f, n1));
                w[(int)L.GrassLush] = 1f - meadow;
                w[(int)L.GrassMeadow] = meadow;

                float dirt = 0f, cobble = 0f;
                // trampled castle grounds: dirt lanes with grass tufts surviving in patches
                if (InsideWalls(p, -4f)) dirt = Mathf.Max(dirt, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.62f, 0.38f, n2 * 0.6f + n1 * 0.4f)) * 0.85f + 0.1f);
                // dirt roads
                float road = RoadDistance(p);
                dirt = Mathf.Max(dirt, 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.2f, 4.2f, road)));
                // stone aprons and worn soil around every building so they sit *in* the ground
                float sd = NearestBuilding(p, out Building nb);
                if (nb != null)
                {
                    bool paved = nb.kind == "House" || nb.kind == "Castle";
                    float apron = paved ? 1.1f + n2 * 0.8f : 0.4f;
                    cobble = Mathf.Max(cobble, 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(apron * 0.6f, apron + 0.5f, sd)));
                    dirt = Mathf.Max(dirt, 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.8f, 3.8f + n2 * 1.8f, sd)));
                }
                float rock = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(26f, 38f, slope));

                for (int l = 0; l < layers; l++) w[l] *= 1f - dirt;
                w[(int)L.Dirt] += dirt;
                for (int l = 0; l < layers; l++) w[l] *= 1f - cobble;
                w[(int)L.Cobble] += cobble;
                for (int l = 0; l < layers; l++) w[l] *= 1f - rock;
                w[(int)L.Rock] += rock;

                float sum = w.Sum();
                for (int l = 0; l < layers; l++) splat[iz, ix, l] = w[l] / sum;
            }
        td.SetAlphamaps(0, 0, splat);
    }

    static float SplatAt(Vector2 p, Vector3 origin, L layer)
    {
        int ix = Mathf.Clamp((int)((p.x - origin.x) / TerrainSize * AlphaRes), 0, AlphaRes - 1);
        int iz = Mathf.Clamp((int)((p.y - origin.z) / TerrainSize * AlphaRes), 0, AlphaRes - 1);
        return splat[iz, ix, (int)layer];
    }

    static void PlaceDetails(TerrainData td, Vector3 origin)
    {
        DetailPrototype Proto(string tex, float w0, float w1, float h0, float h1, Color healthy, Color dry, DetailRenderMode mode) => new DetailPrototype
        {
            prototypeTexture = Tex<Texture2D>(tex),
            usePrototypeMesh = false,
            renderMode = mode,
            minWidth = w0, maxWidth = w1,
            minHeight = h0, maxHeight = h1,
            healthyColor = healthy, dryColor = dry,
            noiseSpread = 0.3f,
        };
        td.detailPrototypes = new[]
        {
            Proto("GrassBlades", 0.45f, 0.8f, 0.22f, 0.48f, new Color(0.42f, 0.66f, 0.24f), new Color(0.6f, 0.66f, 0.3f), DetailRenderMode.Grass),
            Proto("FlowersYellow", 0.35f, 0.5f, 0.3f, 0.45f, Color.white, Color.white, DetailRenderMode.GrassBillboard),
            Proto("FlowersRed", 0.35f, 0.5f, 0.3f, 0.45f, Color.white, Color.white, DetailRenderMode.GrassBillboard),
            Proto("FlowersWhite", 0.35f, 0.5f, 0.3f, 0.45f, Color.white, Color.white, DetailRenderMode.GrassBillboard),
            Proto("FlowersPurple", 0.35f, 0.5f, 0.3f, 0.45f, Color.white, Color.white, DetailRenderMode.GrassBillboard),
        };
        td.wavingGrassTint = new Color(0.92f, 1f, 0.9f);
        td.wavingGrassStrength = 0.35f;
        td.wavingGrassAmount = 0.3f;
        td.wavingGrassSpeed = 0.4f;
        td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        td.SetDetailResolution(DetailRes, 16);

        var grass = new int[DetailRes, DetailRes];
        var flowers = Enumerable.Range(0, 4).Select(_ => new int[DetailRes, DetailRes]).ToArray();
        for (int iz = 0; iz < DetailRes; iz++)
            for (int ix = 0; ix < DetailRes; ix++)
            {
                Vector2 p = AlphaToWorld(ix, iz, DetailRes, origin);
                if ((p - Center).magnitude > PlainRadius + 60f) continue;
                float grassy = SplatAt(p, origin, L.GrassLush) + SplatAt(p, origin, L.GrassMeadow);
                float sd = NearestBuilding(p, out _);
                if (sd < 0.35f || grassy < 0.35f) continue;
                float clump = Mathf.PerlinNoise(p.x * 0.15f + 7f, p.y * 0.15f + 3f);
                // thicker grass hugging building bases and wall feet, hides the seam
                float hug = sd < 5f ? 1.5f : 0f;
                grass[iz, ix] = Mathf.RoundToInt(Mathf.Clamp(grassy * (clump * 3f + hug) + (float)rng.NextDouble() * 0.6f, 0f, 4f));

                float flowerChance = 0.03f + (sd < 4.5f ? 0.25f : 0f) + (SplatAt(p, origin, L.GrassMeadow) > 0.5f ? 0.05f : 0f);
                if (rng.NextDouble() < flowerChance * grassy)
                {
                    int kind = (int)(Mathf.PerlinNoise(p.x * 0.05f, p.y * 0.05f) * 4f * 0.999f + rng.Next(2)) % 4;
                    flowers[kind][iz, ix] = 1 + rng.Next(2);
                }
            }
        td.SetDetailLayer(0, 0, 0, grass);
        for (int i = 0; i < 4; i++) td.SetDetailLayer(0, 0, i + 1, flowers[i]);
    }

    static void PlaceTrees(TerrainData td, Vector3 origin, Prefabs prefabs)
    {
        var protos = prefabs.trees.Concat(prefabs.rocks).Select(p => new TreePrototype { prefab = p }).ToArray();
        td.treePrototypes = protos;
        int rockStart = prefabs.trees.Length;
        const float scale = 2.4f; // ~8-11 m trees next to a 1.8 m knight
        var list = new List<TreeInstance>();
        const float cell = 5.5f;
        for (float z = origin.z + 2; z < origin.z + TerrainSize - 2; z += cell)
            for (float x = origin.x + 2; x < origin.x + TerrainSize - 2; x += cell)
            {
                Vector2 p = new Vector2(x + R(-cell, cell) * 0.45f, z + R(-cell, cell) * 0.45f);
                float d = (p - Center).magnitude;
                if (d < CastleRadius + 6f || RoadDistance(p) < 6f) continue;
                float nx = (p.x - origin.x) / TerrainSize, nz = (p.y - origin.z) / TerrainSize;
                if (nx <= 0 || nx >= 1 || nz <= 0 || nz >= 1) continue;
                float slope = td.GetSteepness(nx, nz);
                float worldY = td.GetInterpolatedHeight(nx, nz) - TerrainBase;
                float forest = Fbm(p.x * 0.018f + 50f, p.y * 0.018f, 3);
                float density = Mathf.InverseLerp(0.42f, 0.62f, forest) * Mathf.InverseLerp(CastleRadius + 6f, PlainRadius + 20f, d);
                bool high = worldY > 30f;

                if (slope < 32f && rng.NextDouble() < density * 0.95f + 0.03f)
                {
                    bool pine = high || forest > 0.58f ? rng.NextDouble() < 0.8 : rng.NextDouble() < 0.25;
                    int proto = pine ? rng.Next(2) : 2 + (rng.NextDouble() < 0.12 ? 3 : rng.Next(3));
                    float s = scale * R(0.85f, 1.35f) * (pine ? 1.15f : 1f);
                    list.Add(Instance(nx, nz, proto, s, s * R(0.9f, 1.15f)));
                }
                else if (rng.NextDouble() < (slope > 22f ? 0.35f : 0.035f))
                {
                    float s = scale * R(0.8f, 2.4f) * (slope > 22f ? 1.6f : 1f);
                    list.Add(Instance(nx, nz, rockStart + rng.Next(prefabs.rocks.Length), s, s * R(0.7f, 1.1f)));
                }
            }
        td.SetTreeInstances(list.ToArray(), true);
        Debug.Log($"[EnvironmentBuilder] {list.Count} trees/rocks placed");
    }

    static TreeInstance Instance(float nx, float nz, int proto, float w, float h) => new TreeInstance
    {
        position = new Vector3(nx, 0, nz),
        prototypeIndex = proto,
        widthScale = w,
        heightScale = h,
        rotation = R(0, Mathf.PI * 2f),
        color = Color.Lerp(Color.white, new Color(0.85f, 0.95f, 0.8f), R(0, 1)),
        lightmapColor = Color.white,
    };

    // ------------------------------------------------------------------ grounding details

    static void CreateFoundations(Transform parent, Mats mats)
    {
        var root = new GameObject("Foundations").transform;
        root.SetParent(parent, false);
        foreach (var b in buildings.Where(b => b.kind == "House"))
        {
            // each house gets its own warm wash so the village doesn't look copy-pasted
            Color[] washes = { new Color(1f, 0.97f, 0.9f), new Color(1.06f, 0.95f, 0.82f), new Color(0.98f, 0.9f, 0.84f), new Color(1.04f, 1f, 0.92f), new Color(0.95f, 0.92f, 0.88f) };
            if (!b.go.TryGetComponent(out TintVariation tv)) tv = b.go.AddComponent<TintVariation>();
            tv.tint = washes[rng.Next(washes.Length)];
            tv.Apply();

            // a stone plinth slightly wider than the walls, half buried
            var mb = new MeshBuilder(1);
            Vector3 h = new Vector3(b.half.x + 0.25f, 0, b.half.y + 0.25f);
            mb.Box(0, new Vector3(-h.x, -0.5f, -h.z), new Vector3(h.x, 0.4f, h.z), 0.45f);
            var mesh = mb.ToMesh(b.go.name + "_Plinth");
            string path = $"{GenDir}/Plinths/{b.go.name.Replace(" ", "_").Replace("(", "").Replace(")", "")}.asset";
            EnsureFolder(GenDir + "/Plinths");
            mesh = SaveMesh(mesh, path);

            var go = new GameObject(b.go.name + " Foundation");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(b.center.x, b.bottom + 0.12f, b.center.y);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(b.axisZ.x, 0, b.axisZ.y), Vector3.up);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mats.foundation;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
    }

    static void ScatterBaseProps(Transform parent, Prefabs prefabs)
    {
        var root = new GameObject("Base Props").transform;
        root.SetParent(parent, false);
        const float s = 1.6f;
        int count = 0;
        foreach (var b in buildings.Where(b => b.kind == "House" || b.kind == "Wall" || b.kind == "Tower" || b.kind == "Castle"))
        {
            float perimeter = 4f * (b.half.x + b.half.y);
            int n = Mathf.Clamp(Mathf.RoundToInt(perimeter / (b.kind == "Wall" ? 1.6f : 2.2f)), 2, 14);
            for (int i = 0; i < n; i++)
            {
                if (rng.NextDouble() < 0.3) continue;
                // random point on the footprint edge, pushed just outside
                float t = (float)rng.NextDouble() * 4f;
                int side = (int)t; float u = (t - side) * 2f - 1f;
                Vector2 local = side switch
                {
                    0 => new Vector2(u * b.half.x, b.half.y),
                    1 => new Vector2(u * b.half.x, -b.half.y),
                    2 => new Vector2(b.half.x, u * b.half.y),
                    _ => new Vector2(-b.half.x, u * b.half.y),
                };
                Vector2 outward = side switch { 0 => b.axisZ, 1 => -b.axisZ, 2 => b.axisX, _ => -b.axisX };
                Vector2 p = b.center + b.axisX * local.x + b.axisZ * local.y + outward * R(0.15f, 0.45f) * s;
                if (NearestBuilding(p, out var other) < 0.05f && other != b) continue;
                if (RoadDistance(p) < 2.2f) continue;

                bool rock = rng.NextDouble() < (b.kind == "House" ? 0.25 : 0.45);
                var prefab = rock ? prefabs.rocks[rng.Next(prefabs.rocks.Length)] : prefabs.bushes[rng.Next(prefabs.bushes.Length)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.transform.position = new Vector3(p.x, -0.05f, p.y);
                go.transform.rotation = Quaternion.Euler(0, R(0, 360), 0);
                go.transform.localScale = Vector3.one * s * (rock ? R(0.35f, 0.7f) : R(0.6f, 1.05f));
                count++;
            }
        }
        Debug.Log($"[EnvironmentBuilder] {count} base props");
    }

    // Arrow (and some healing) supplies on crates beside the houses. They respawn, so the bow stays useful.
    static void PlaceSupplyCrates(Transform parent)
    {
        var prefab = GameplayBuilder.CratePrefab;
        if (prefab == null) { Debug.LogWarning("[EnvironmentBuilder] no crate prefab"); return; }
        var root = new GameObject("Supply Crates").transform;
        root.SetParent(parent, false);
        var houses = buildings.Where(b => b.kind == "House")
            .OrderBy(b => Mathf.Atan2(b.center.y - Center.y, b.center.x - Center.x)).ToList();
        int step = Mathf.Max(1, houses.Count / 14);
        int placed = 0;
        for (int i = 0; i < houses.Count; i += step)
        {
            var b = houses[i];
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int side = rng.Next(4);
                float u = R(-0.6f, 0.6f);
                Vector2 local = side switch { 0 => new Vector2(u * b.half.x, b.half.y), 1 => new Vector2(u * b.half.x, -b.half.y), 2 => new Vector2(b.half.x, u * b.half.y), _ => new Vector2(-b.half.x, u * b.half.y) };
                Vector2 outward = side switch { 0 => b.axisZ, 1 => -b.axisZ, 2 => b.axisX, _ => -b.axisX };
                Vector2 p = b.center + b.axisX * local.x + b.axisZ * local.y + outward * 1.5f;
                if (NearestBuilding(p, out _) < 1.1f) continue;
                if (Mathf.Abs(WorldHeight(p.x, p.y)) > 0.2f) continue;
                var crate = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                crate.name = "Supply Crate " + placed;
                crate.transform.position = new Vector3(p.x, -0.02f, p.y);
                crate.transform.rotation = Quaternion.LookRotation(new Vector3(outward.x, 0, outward.y)) * Quaternion.Euler(0, R(-12f, 12f), 0);
                var pickupGo = new GameObject(placed % 4 == 3 ? "Healing Draught" : "Arrow Bundle");
                pickupGo.transform.SetParent(crate.transform, false);
                pickupGo.transform.localPosition = new Vector3(0, 0.62f, 0);
                var pickup = pickupGo.AddComponent<Pickup>();
                pickup.respawns = true;
                if (placed % 4 == 3) { pickup.kind = PickupKind.Health; pickup.amount = 25; pickup.respawnTime = 75f; }
                else { pickup.kind = PickupKind.Arrows; pickup.amount = 8; pickup.respawnTime = 40f; }
                placed++;
                break;
            }
        }
        Debug.Log($"[EnvironmentBuilder] {placed} supply crates");
    }

    // The pack's textures are very dark (~50/255): in linear lighting that reads as near-black stone.
    // Point the castle materials at warmer, brighter graded copies with generated normal maps.
    static void ApplyCastleGrade()
    {
        const string matDir = "Assets/Advance Studios/Medieval Castle/Art/Model/Materials";
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { matDir }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/Castle/{m.name}_D.jpg");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/Castle/{m.name}_N.jpg");
            if (albedo == null) continue;
            m.mainTexture = albedo;
            m.color = Color.white;
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", 0.8f);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Glossiness", 0.12f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }
    }

    // Start the knight in an open, sunny part of the courtyard, facing the keep.
    static void PlacePlayerStart(Scene game)
    {
        var player = game.GetRootGameObjects().FirstOrDefault(g => g.CompareTag("Player"));
        if (player == null) return;
        Vector2 best = Center; float bestScore = float.MinValue;
        Vector3 toSun = -(Quaternion.Euler(38f, -142f, 0f) * Vector3.forward);
        for (float z = -CastleRadius; z <= CastleRadius; z += 2f)
            for (float x = -CastleRadius; x <= CastleRadius; x += 2f)
            {
                Vector2 p = Center + new Vector2(x, z);
                if (!InsideWalls(p, -10f)) continue;
                float clear = NearestBuilding(p, out _);
                if (clear < 5f) continue;
                // in sunlight? (cast towards the sun over the buildings)
                bool lit = !Physics.Raycast(new Vector3(p.x, 1.2f, p.y), toSun, 200f);
                float score = Mathf.Min(clear, 12f) + (lit ? 10f : 0f) - (p - Center).magnitude * 0.08f;
                if (score > bestScore) { bestScore = score; best = p; }
            }
        Vector3 pos = new Vector3(best.x, 0.05f, best.y);
        Vector3 look = new Vector3(Center.x, 0, Center.y) - pos; look.y = 0;
        if (look.sqrMagnitude < 1f) look = Vector3.forward;
        player.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look));
        var cam = game.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).FirstOrDefault(c => c.CompareTag("MainCamera"));
        if (cam != null)
        {
            cam.transform.position = pos - look.normalized * 6.5f + Vector3.up * 3.2f;
            cam.transform.LookAt(pos + Vector3.up * 1.5f);
        }
        Debug.Log($"[EnvironmentBuilder] player start {pos} (clear {NearestBuilding(best, out _):F1} m)");
    }

    static void TameTorchLights(Scene world)
    {
        foreach (var go in world.GetRootGameObjects())
            foreach (var light in go.GetComponentsInChildren<Light>())
                if (light.type == LightType.Point)
                {
                    // warm lantern glow that reads in daylight without orange blowouts
                    light.color = new Color(1f, 0.68f, 0.32f);
                    light.intensity = 1.4f;
                    light.shadows = LightShadows.None;
                }
    }

    // ------------------------------------------------------------------ lighting & post

    static Material CreateSky()
    {
        string path = GenDir + "/Sky.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Skybox/Cubemap")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Skybox/Cubemap");
        m.SetTexture("_Tex", Tex<Cubemap>("Sky_PartlyCloudy"));
        m.SetFloat("_Exposure", 1.05f);
        m.SetFloat("_Rotation", 40f);
        m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.52f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static void ApplyLighting(Scene scene, Material sky)
    {
        Scene previous = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);
        RenderSettings.skybox = sky;
        // trilight ambient: no lightmap bake needed, consistent in editor and builds
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.74f, 0.8f, 0.94f);
        RenderSettings.ambientEquatorColor = new Color(0.66f, 0.66f, 0.6f);
        RenderSettings.ambientGroundColor = new Color(0.4f, 0.38f, 0.3f);
        RenderSettings.ambientIntensity = 1.15f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = Tex<Cubemap>("Sky_PartlyCloudy");
        RenderSettings.reflectionIntensity = 0.6f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.7f, 0.8f, 0.9f);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 380f;

        Light sun = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>()).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            // warm late-morning sun, long-ish shadows that give the buildings weight
            sun.transform.rotation = Quaternion.Euler(38f, -142f, 0f);
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sun.shadowResolution = LightShadowResolution.VeryHigh;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        SceneManager.SetActiveScene(previous);
    }

    static void SetupCameraAndPostFx(Scene game)
    {
        var cam = game.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).FirstOrDefault(c => c.CompareTag("MainCamera"));
        if (cam == null) throw new Exception("Main Camera not found");
        cam.farClipPlane = 900f;
        cam.nearClipPlane = 0.1f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.allowHDR = true;
        cam.allowMSAA = false; // SMAA below; MSAA + AO is wasteful

        string profilePath = GenDir + "/PostFX.asset";
        AssetDatabase.DeleteAsset(profilePath);
        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(profile, profilePath);
        T Add<T>() where T : PostProcessEffectSettings
        {
            var s = profile.AddSettings<T>();
            s.name = typeof(T).Name;
            s.enabled.Override(true);
            AssetDatabase.AddObjectToAsset(s, profile);
            return s;
        }
        var ao = Add<AmbientOcclusion>();
        ao.mode.Override(AmbientOcclusionMode.MultiScaleVolumetricObscurance);
        ao.intensity.Override(0.75f);
        ao.thicknessModifier.Override(1.4f);
        ao.color.Override(new Color(0.1f, 0.08f, 0.12f));
        var bloom = Add<Bloom>();
        bloom.intensity.Override(1.1f);
        bloom.threshold.Override(1.05f);
        bloom.softKnee.Override(0.6f);
        bloom.diffusion.Override(7f);
        var grading = Add<ColorGrading>();
        grading.gradingMode.Override(GradingMode.HighDefinitionRange);
        grading.tonemapper.Override(Tonemapper.ACES);
        grading.postExposure.Override(0.3f);
        grading.temperature.Override(5f);
        grading.tint.Override(1f);
        grading.saturation.Override(6f);
        grading.contrast.Override(8f);
        var vignette = Add<Vignette>();
        vignette.intensity.Override(0.26f);
        vignette.smoothness.Override(0.45f);
        EditorUtility.SetDirty(profile);

        foreach (var old in game.GetRootGameObjects().Where(g => g.name == "PostFX Volume")) Object.DestroyImmediate(old);
        var volGo = new GameObject("PostFX Volume") { layer = 0 };
        SceneManager.MoveGameObjectToScene(volGo, game);
        var vol = volGo.AddComponent<PostProcessVolume>();
        vol.isGlobal = true;
        vol.priority = 1;
        vol.sharedProfile = profile;

        var layer = cam.GetComponent<PostProcessLayer>() ?? cam.gameObject.AddComponent<PostProcessLayer>();
        var resources = AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
        if (resources == null) throw new Exception("PostProcessResources not found");
        layer.Init(resources);
        layer.volumeTrigger = cam.transform;
        layer.volumeLayer = 1; // Default layer
        layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
        layer.subpixelMorphologicalAntialiasing.quality = SubpixelMorphologicalAntialiasing.Quality.High;
        var so = new SerializedObject(layer);
        so.FindProperty("m_Resources").objectReferenceValue = resources;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(layer);
    }

    static void SetupQuality()
    {
        QualitySettings.shadowDistance = 140f;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.antiAliasing = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.lodBias = 2f;
    }

    // ------------------------------------------------------------------ navigation

    static void BakeNavMeshAndSpawns(Scene game, Terrain terrain)
    {
        // the old flat plane is replaced by the terrain
        foreach (var plane in game.GetRootGameObjects().Where(g => g.name == "Plane")) Object.DestroyImmediate(plane);
        foreach (var old in game.GetRootGameObjects().Where(g => g.name == "NavMesh")) Object.DestroyImmediate(old);

        var navGo = new GameObject("NavMesh");
        SceneManager.MoveGameObjectToScene(navGo, game);
        navGo.transform.position = new Vector3(Center.x, 0, Center.y);
        var surface = navGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Volume;
        surface.center = new Vector3(0, 10, 0);
        surface.size = new Vector3((PlainRadius + 40f) * 2f, 90, (PlainRadius + 40f) * 2f);
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = ~0;

        // don't let the player (or anything with an agent/obstacle) punch holes in the bake
        var player = game.GetRootGameObjects().FirstOrDefault(g => g.CompareTag("Player"));
        bool playerActive = player != null && player.activeSelf;
        if (player != null) player.SetActive(false);
        surface.BuildNavMesh();
        if (player != null) player.SetActive(playerActive);

        string dir = Path.GetDirectoryName(game.path).Replace('\\', '/') + "/" + game.name;
        EnsureFolder(dir);
        AssetDatabase.DeleteAsset(dir + "/NavMesh-Plane.asset");
        string navPath = dir + "/NavMesh.asset";
        AssetDatabase.DeleteAsset(navPath);
        AssetDatabase.CreateAsset(surface.navMeshData, navPath);

        // spawn points: the original one plus the road ends, keeping only those that can reach the keep
        var spawner = game.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ZombieSpawner>()).FirstOrDefault();
        if (spawner == null || player == null) return;
        Vector3 target = player.transform.position;
        bool Reachable(Vector3 from)
        {
            if (!NavMesh.SamplePosition(from, out var a, 6f, NavMesh.AllAreas) || !NavMesh.SamplePosition(target, out var b, 6f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        Transform spawnRoot = spawner.transform;
        foreach (Transform child in spawnRoot.Cast<Transform>().Where(c => c.name.StartsWith("Road Spawn")).ToList()) Object.DestroyImmediate(child.gameObject);
        var extra = new List<Transform>();
        var candidates = Roads.Select(r => r[Mathf.Min(2, r.Length - 1)]).ToList();
        for (int a = 0; a < 360; a += 60) candidates.Add(Center + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * (CastleRadius + 14f));
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2 c = candidates[i];
            var pos = new Vector3(c.x, WorldHeight(c.x, c.y), c.y);
            bool ok = Reachable(pos);
            Debug.Log($"[EnvironmentBuilder] spawn candidate {c} reachable={ok}");
            if (!ok) continue;
            var t = new GameObject("Road Spawn " + i).transform;
            t.SetParent(spawnRoot, true);
            t.position = pos;
            t.rotation = Quaternion.LookRotation(target - pos);
            extra.Add(t);
        }
        Transform main = spawner.spawnPoint != null ? spawner.spawnPoint : spawner.transform;
        bool mainOk = Reachable(main.position);
        Debug.Log($"[EnvironmentBuilder] original spawn {main.position} reachable={mainOk}");
        if (!mainOk && extra.Count > 0) spawner.spawnPoint = extra[0];
        spawner.extraSpawnPoints = extra.ToArray();
        EditorUtility.SetDirty(spawner);
    }

    // ------------------------------------------------------------------ screenshots (batch verification)

    static void CaptureShots(string dir)
    {
        Directory.CreateDirectory(dir);
        Scene game = SceneManager.GetSceneByPath(GameScene);
        var cam = game.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).First(c => c.CompareTag("MainCamera"));
        var player = game.GetRootGameObjects().First(g => g.CompareTag("Player")).transform;
        Vector3 pp = player.position;
        Vector3 keep = new Vector3(Center.x, 2f, Center.y);
        Building house = buildings.Where(b => b.kind == "House").OrderBy(b => (b.center - new Vector2(pp.x, pp.z)).sqrMagnitude).First();
        Vector3 hc = new Vector3(house.center.x, 0.8f, house.center.y);

        var shots = new (string name, Vector3 pos, Vector3 look)[]
        {
            ("1_overview", keep + new Vector3(0.9f, 0.65f, -1.0f) * CastleRadius, keep),
            ("2_gameplay", pp + new Vector3(0, 3.2f, -6.5f), pp + Vector3.up * 1.5f),
            ("3_house_base", hc + new Vector3(house.half.magnitude + 4f, 1.5f, -(house.half.magnitude + 4f)), hc),
            ("4_landscape", keep + new Vector3(-0.3f, 0.12f, -2.1f) * CastleRadius, keep + new Vector3(0, 8, 0)),
            ("5_walls_outside", keep + new Vector3(0.75f, 0.03f, -1.15f) * CastleRadius + Vector3.up * 2f, keep + new Vector3(0, 6, 0)),
        };
        Vector3 oldPos = cam.transform.position; Quaternion oldRot = cam.transform.rotation;
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        foreach (var s in shots)
        {
            cam.transform.position = s.pos;
            cam.transform.LookAt(s.look);
            cam.targetTexture = rt;
            cam.Render(); cam.Render(); // second pass lets temporal effects settle
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, s.name + ".png"), tex.EncodeToPNG());
        }
        cam.targetTexture = null;
        RenderTexture.active = null;
        cam.transform.SetPositionAndRotation(oldPos, oldRot);
        Debug.Log("[EnvironmentBuilder] screenshots -> " + dir);
    }

    // ------------------------------------------------------------------ util

    // overwrite in place when the asset exists, so GUIDs and references stay stable
    public static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static string GetArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
