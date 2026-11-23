using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SwordCollectionHandlingReview
{
    const float GripCenterX = -.352f;
    const float ShieldMargin = .004f;

    [MenuItem("Medieval Warrior/Combat/Review collection sword handling")]
    public static void Run()
    {
        var prefabs = Resources.LoadAll<GameObject>("SwordCollection").OrderBy(p => p.name).ToArray();
        if (prefabs.Length != 7)
            throw new InvalidOperationException($"Expected seven sword prefabs, found {prefabs.Length}.");
        var swords = prefabs.Select(p => new SwordGeometry(p)).ToArray();
        if (swords.Select(s => s.Mesh).Distinct().Count() != swords.Length)
            throw new InvalidOperationException("Two sword prefabs share a mesh.");

        var shieldBounds = MarchwardenVisual.Asset.shield.bounds;
        int poses = 0;
        float maximumPalmError = 0;
        SwordClearanceReview.ReviewPoses((pose, swordMount, shieldMount, palm) =>
        {
            poses++;
            var center = swordMount.MultiplyPoint3x4(new Vector3(GripCenterX, 0, 0));
            maximumPalmError = Mathf.Max(maximumPalmError, Vector3.Distance(center, palm));
            var toShield = shieldMount.inverse * swordMount;
            var padding = ShieldMargin / ((Vector3)shieldMount.GetColumn(0)).magnitude;
            var extents = shieldBounds.extents + Vector3.one * padding;
            foreach (var sword in swords)
                sword.CheckShield(pose, toShield, shieldBounds.center, extents);
        });

        var report = new StringBuilder();
        report.AppendLine($"Sampled {poses} held poses per model across block, all {SwordMoves.Count} strikes and their combo transitions.");
        report.AppendLine($"Maximum animated palm anchor error {maximumPalmError * 1000:F2} mm. Shield envelope margin {ShieldMargin * 1000:F0} mm.");
        bool passed = poses > 0 && maximumPalmError < .002f;
        foreach (var sword in swords)
        {
            bool ok = sword.GripsFit && sword.ClippedPoses == 0;
            passed &= ok;
            report.AppendLine($"{(ok ? "PASS" : "FAIL")} {sword.Name}: grip radii {sword.MinRadius * 1000:F1}–{sword.MaxRadius * 1000:F1} mm," +
                $" {sword.ClippedPoses} poses enter the shield envelope. {sword.FirstOverlap}");
        }
        const string directory = "Artifacts/SwordCollection";
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "sword-handling.txt"), report.ToString());
        Debug.Log("[Sword collection handling] " + report);
        if (!passed) throw new InvalidOperationException("Collection handling failed. See Artifacts/SwordCollection/sword-handling.txt");
    }

    sealed class SwordGeometry
    {
        public readonly string Name;
        public readonly Mesh Mesh;
        public int ClippedPoses { get; private set; }
        public string FirstOverlap { get; private set; }
        public float MinRadius { get; private set; } = float.PositiveInfinity;
        public float MaxRadius { get; private set; }
        public bool GripsFit => MinRadius >= .005f && MaxRadius <= .035f;

        readonly Vector3[] vertices, inShield, corners = new Vector3[8];
        readonly int[] triangles, sides;

        public SwordGeometry(GameObject prefab)
        {
            Name = prefab.name;
            Mesh = prefab.GetComponent<MeshFilter>()?.sharedMesh;
            var materials = prefab.GetComponent<MeshRenderer>()?.sharedMaterials;
            if (Mesh == null || Mesh.subMeshCount != 4 || materials == null || materials.Length != Mesh.subMeshCount ||
                materials.Any(m => m == null) || prefab.transform.localPosition != Vector3.zero ||
                prefab.transform.localRotation != Quaternion.identity || prefab.transform.localScale != Vector3.one)
                throw new InvalidOperationException($"{Name} lacks a mount-ready root mesh and its four assigned materials.");
            vertices = Mesh.vertices;
            triangles = Mesh.triangles;
            inShield = new Vector3[vertices.Length];
            sides = new int[vertices.Length];
            var bounds = Mesh.bounds;
            for (int i = 0; i < corners.Length; i++)
                corners[i] = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            // Cross the usable grip at the palm center and on either side. All
            // four outward rays must meet a surface within the closed fist.
            foreach (float offset in new[] { -.023f, 0f, .023f })
                foreach (var direction in new[] { Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
                {
                    float radius = SurfaceDistance(new Vector3(GripCenterX + offset, 0, 0), direction);
                    MinRadius = Mathf.Min(MinRadius, radius);
                    MaxRadius = Mathf.Max(MaxRadius, radius);
                }
        }

        float SurfaceDistance(Vector3 origin, Vector3 direction)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]];
                var ab = vertices[triangles[i + 1]] - a;
                var ac = vertices[triangles[i + 2]] - a;
                var p = Vector3.Cross(direction, ac);
                float determinant = Vector3.Dot(ab, p);
                if (Mathf.Abs(determinant) < 1e-10f) continue;
                var delta = origin - a;
                float u = Vector3.Dot(delta, p) / determinant;
                var q = Vector3.Cross(delta, ab);
                float v = Vector3.Dot(direction, q) / determinant;
                if (u < -.00001f || v < -.00001f || u + v > 1.00001f) continue;
                float t = Vector3.Dot(ac, q) / determinant;
                if (t > 0 && t < nearest) nearest = t;
            }
            return nearest;
        }

        public void CheckShield(string pose, Matrix4x4 toShield, Vector3 center, Vector3 extents)
        {
            // A common outside plane excludes the whole transformed sword. If
            // the boxes overlap, test all actual triangles, including curved
            // blades, side rings and guards with no vertex inside the shield.
            int commonSide = 63;
            foreach (var corner in corners)
                commonSide &= Outside(toShield.MultiplyPoint3x4(corner) - center, extents);
            if (commonSide != 0) return;
            for (int i = 0; i < vertices.Length; i++)
            {
                inShield[i] = toShield.MultiplyPoint3x4(vertices[i]) - center;
                sides[i] = Outside(inShield[i], extents);
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                if ((sides[ia] & sides[ib] & sides[ic]) != 0) continue;
                if (sides[ia] != 0 && sides[ib] != 0 && sides[ic] != 0 &&
                    !TriangleCrossesBox(inShield[ia], inShield[ib], inShield[ic], extents)) continue;
                ClippedPoses++;
                if (FirstOverlap == null) FirstOverlap = $"First overlap in {pose}, triangle {i / 3}.";
                return;
            }
        }

        static int Outside(Vector3 p, Vector3 e) =>
            (p.x < -e.x ? 1 : p.x > e.x ? 2 : 0) |
            (p.y < -e.y ? 4 : p.y > e.y ? 8 : 0) |
            (p.z < -e.z ? 16 : p.z > e.z ? 32 : 0);

        // Separating-axis test. The caller has checked the three box axes.
        static bool TriangleCrossesBox(Vector3 a, Vector3 b, Vector3 c, Vector3 e)
        {
            bool Separates(Vector3 axis)
            {
                float radius = e.x * Mathf.Abs(axis.x) + e.y * Mathf.Abs(axis.y) + e.z * Mathf.Abs(axis.z);
                float p = Vector3.Dot(a, axis), q = Vector3.Dot(b, axis), r = Vector3.Dot(c, axis);
                return (p > radius && q > radius && r > radius) || (p < -radius && q < -radius && r < -radius);
            }
            bool EdgeSeparates(Vector3 edge) => Separates(Vector3.Cross(edge, Vector3.right)) ||
                Separates(Vector3.Cross(edge, Vector3.up)) || Separates(Vector3.Cross(edge, Vector3.forward));
            return !Separates(Vector3.Cross(b - a, c - a)) &&
                !EdgeSeparates(b - a) && !EdgeSeparates(c - b) && !EdgeSeparates(a - c);
        }
    }
}
