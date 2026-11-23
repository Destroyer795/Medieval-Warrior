using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

// Read only the triangles an arrow can reach. Each group has a conservative
// bounds for every bone influencing it, so it follows animation without
// rebuilding mesh colliders or skinning the entire horde every frame.
internal static class ArrowSurface
{
    internal enum Part { Body, Head, Shield }

    readonly struct Group
    {
        public readonly int start, end;
        public readonly KeyValuePair<int, Bounds>[] bones;

        public Group(int start, int end, Dictionary<int, Bounds> bounds)
        {
            this.start = start;
            this.end = end;
            bones = new KeyValuePair<int, Bounds>[bounds.Count];
            int i = 0;
            foreach (var entry in bounds) bones[i++] = entry;
        }
    }

    sealed class Surface
    {
        public readonly Vector3[] points;
        public readonly int[] indices;
        public readonly BoneWeight[] weights;
        public readonly Matrix4x4[] bind;
        public readonly Vector2[] tiles;
        public readonly List<Group> groups = new List<Group>();

        public Surface(Mesh mesh, bool skinned)
        {
            points = mesh.vertices;
            indices = mesh.triangles;
            if (skinned)
            {
                weights = mesh.boneWeights;
                bind = mesh.bindposes;
                tiles = mesh.uv2;
            }
            const int indicesPerGroup = 96 * 3;
            var bounds = new Dictionary<int, Bounds>();
            for (int start = 0; start < indices.Length; start += indicesPerGroup)
            {
                int end = Mathf.Min(start + indicesPerGroup, indices.Length);
                bounds.Clear();
                for (int i = start; i < end; i++)
                {
                    int at = indices[i];
                    var point = points[at];
                    if (!skinned) Include(0, point);
                    else
                    {
                        var w = weights[at];
                        Include(w.boneIndex0, point);
                        if (w.weight1 > 0) Include(w.boneIndex1, point);
                        if (w.weight2 > 0) Include(w.boneIndex2, point);
                        if (w.weight3 > 0) Include(w.boneIndex3, point);
                    }
                }
                groups.Add(new Group(start, end, bounds));
            }

            void Include(int bone, Vector3 point)
            {
                if (!bounds.TryGetValue(bone, out var bound)) bound = new Bounds(point, Vector3.zero);
                else bound.Encapsulate(point);
                bounds[bone] = bound;
            }
        }
    }

    static readonly Dictionary<Mesh, Surface> surfaces = new Dictionary<Mesh, Surface>();
    static readonly ConditionalWeakTable<SkinnedMeshRenderer, Pose> poses = new ConditionalWeakTable<SkinnedMeshRenderer, Pose>();
    static readonly Matrix4x4[] rigid = new Matrix4x4[1];

    sealed class Pose
    {
        public Transform[] bones;
        public Matrix4x4[] matrices;
        public Bounds[] groupBounds;
        public Surface surface;
        public int frame = -1;
        public bool projectile;

        public void Refresh(SkinnedMeshRenderer body, Surface geometry, bool projectile)
        {
            if (frame == Time.frameCount && surface == geometry && this.projectile == projectile) return;
            if (bones == null)
            {
                bones = body.bones;
                matrices = new Matrix4x4[bones.Length];
            }
            if (surface != geometry)
                groupBounds = new Bounds[geometry.groups.Count];
            for (int i = 0; i < bones.Length; i++) matrices[i] = bones[i].localToWorldMatrix * geometry.bind[i];
            for (int i = 0; i < groupBounds.Length; i++) groupBounds[i] = WorldBounds(geometry.groups[i], matrices);
            surface = geometry;
            frame = Time.frameCount;
            this.projectile = projectile;
        }
    }

    static Surface Data(Mesh mesh, bool skinned)
    {
        if (!surfaces.TryGetValue(mesh, out var data))
        {
            data = new Surface(mesh, skinned);
            surfaces.Add(mesh, data);
        }
        return data;
    }

    public static bool CanTrace(EnemyController enemy) => enemy.ArrowBody != null;

    public static bool Trace(EnemyController enemy, Ray ray, bool projectile, ref float nearest, out Part part, out Transform anchor)
    {
        part = Part.Body;
        anchor = enemy.transform;
        var body = enemy.ArrowBody;
        if (body == null || !body.enabled || body.sharedMesh == null) return false;
        var geometry = Data(body.sharedMesh, true);
        var pose = poses.GetValue(body, _ => new Pose());
        pose.Refresh(body, geometry, projectile);
        var bones = pose.bones;
        Transform head = enemy.ArrowHead;
        Transform shieldHand = enemy.type == ZombieType.Bulwark ? enemy.ArrowShieldHand : null;
        bool hit = TraceGeometry(geometry, pose.matrices, pose.groupBounds, ray, ref nearest, out int triangle);
        if (hit)
        {
            int a = geometry.indices[triangle], b = geometry.indices[triangle + 1], c = geometry.indices[triangle + 2];
            int aBone = geometry.weights[a].boneIndex0, bBone = geometry.weights[b].boneIndex0, cBone = geometry.weights[c].boneIndex0;
            anchor = bones[aBone == bBone || aBone == cBone ? aBone : bBone];
            bool IsHead(int bone) => head != null && bones[bone].IsChildOf(head);
            if ((IsHead(aBone) ? 1 : 0) + (IsHead(bBone) ? 1 : 0) + (IsHead(cBone) ? 1 : 0) >= 2)
                part = Part.Head;
            // The wooden plate, metal rim and leather fittings all have rigid
            // left-palm weights. Bare fingers use the skin atlas tile.
            else if (shieldHand != null && aBone == bBone && bBone == cBone && bones[aBone] == shieldHand &&
                geometry.tiles.Length == geometry.points.Length &&
                IsShieldMaterial(geometry.tiles[a].x) && IsShieldMaterial(geometry.tiles[b].x) && IsShieldMaterial(geometry.tiles[c].x))
            {
                part = Part.Shield;
                anchor = shieldHand;
            }
        }
        var helmet = enemy.ArrowHelmet;
        if (TraceProp(helmet, ray, ref nearest))
        {
            hit = true;
            anchor = head;
            part = Part.Head;
        }
        var looseShield = enemy.ArrowShield;
        if (TraceProp(looseShield, ray, ref nearest))
        {
            hit = true;
            anchor = looseShield.transform;
            part = Part.Shield;
        }
        return hit;
    }

    static bool IsShieldMaterial(float tile) => tile == 2 || tile == 3 || tile == 5;

    static bool TraceProp(MeshFilter prop, Ray ray, ref float nearest)
    {
        if (prop == null || prop.sharedMesh == null || !prop.GetComponent<MeshRenderer>().enabled ||
            !prop.gameObject.activeInHierarchy) return false;
        rigid[0] = prop.transform.localToWorldMatrix;
        return TraceGeometry(Data(prop.sharedMesh, false), rigid, null, ray, ref nearest, out _);
    }

    static Bounds WorldBounds(Group group, Matrix4x4[] matrices)
    {
        Bounds bounds = new Bounds();
        bool first = true;
        foreach (var entry in group.bones)
        {
            var m = matrices[entry.Key];
            var local = entry.Value;
            var e = local.extents;
            var worldRadius = Abs(m.MultiplyVector(Vector3.right * e.x)) +
                Abs(m.MultiplyVector(Vector3.up * e.y)) + Abs(m.MultiplyVector(Vector3.forward * e.z));
            var world = new Bounds(m.MultiplyPoint3x4(local.center), worldRadius * 2);
            if (first) { bounds = world; first = false; }
            else bounds.Encapsulate(world);
        }
        bounds.Expand(.0001f);
        return bounds;
    }

    static bool TraceGeometry(Surface surface, Matrix4x4[] matrices, Bounds[] groupBounds, Ray ray, ref float nearest, out int triangle)
    {
        triangle = -1;
        for (int at = 0; at < surface.groups.Count; at++)
        {
            var group = surface.groups[at];
            var bounds = groupBounds != null ? groupBounds[at] : WorldBounds(group, matrices);
            if (!bounds.IntersectRay(ray, out float entryDistance) || entryDistance > nearest) continue;
            for (int i = group.start; i < group.end; i += 3)
            {
                Vector3 a = Position(surface.indices[i]);
                Vector3 b = Position(surface.indices[i + 1]);
                Vector3 c = Position(surface.indices[i + 2]);
                if (!Intersect(ray, a, b, c, out float distance) || distance > nearest) continue;
                nearest = distance;
                triangle = i;
            }
        }
        return triangle >= 0;

        Vector3 Position(int i)
        {
            var p = surface.points[i];
            if (surface.weights == null) return matrices[0].MultiplyPoint3x4(p);
            var w = surface.weights[i];
            var result = matrices[w.boneIndex0].MultiplyPoint3x4(p) * w.weight0;
            if (w.weight1 > 0) result += matrices[w.boneIndex1].MultiplyPoint3x4(p) * w.weight1;
            if (w.weight2 > 0) result += matrices[w.boneIndex2].MultiplyPoint3x4(p) * w.weight2;
            if (w.weight3 > 0) result += matrices[w.boneIndex3].MultiplyPoint3x4(p) * w.weight3;
            return result;
        }
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    static bool Intersect(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
    {
        distance = 0;
        Vector3 ab = b - a, ac = c - a, p = Vector3.Cross(ray.direction, ac);
        float determinant = Vector3.Dot(ab, p);
        if (Mathf.Abs(determinant) < 1e-9f) return false;
        float inverse = 1f / determinant;
        Vector3 offset = ray.origin - a;
        float u = Vector3.Dot(offset, p) * inverse;
        if (u < 0 || u > 1) return false;
        Vector3 q = Vector3.Cross(offset, ab);
        float v = Vector3.Dot(ray.direction, q) * inverse;
        if (v < 0 || u + v > 1) return false;
        distance = Vector3.Dot(ac, q) * inverse;
        return distance >= 0;
    }
}
