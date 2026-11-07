using UnityEngine;

// Spitter projectile: lobbed in an arc, splashes on impact and leaves a burning acid pool.
public class BileGlob : MonoBehaviour
{
    Vector3 velocity;
    float damage;
    EnemyController owner;

    public static void Throw(Vector3 from, Vector3 target, float damage, EnemyController owner)
    {
        const float flightTime = 1.05f;
        Vector3 v = (target - from) / flightTime - 0.5f * Physics.gravity * flightTime;
        var go = ProceduralModels.Spawn("Bile", ProceduralModels.PotionMesh(), new[] { Combat.ResourceMaterial("Acid", new Color(0.5f, 0.85f, 0.1f)) }, null, false);
        go.transform.position = from;
        go.transform.localScale = Vector3.one * 1.3f;
        var b = go.AddComponent<BileGlob>();
        b.velocity = v;
        b.damage = damage;
        b.owner = owner;
        Destroy(go, 4f);
    }

    void Update()
    {
        velocity += Physics.gravity * Time.deltaTime;
        Vector3 step = velocity * Time.deltaTime;
        transform.Rotate(400f * Time.deltaTime, 220f * Time.deltaTime, 0f);
        foreach (var hit in Physics.RaycastAll(transform.position, step.normalized, step.magnitude + 0.15f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && hit.transform.IsChildOf(owner.transform)) continue;
            if (hit.collider.GetComponentInParent<EnemyController>() != null) continue;
            Splash(hit.point);
            return;
        }
        transform.position += step;
    }

    void Splash(Vector3 at)
    {
        AudioManager.PlayAt("splat", at, 1f);
        FX.Blood(at, Vector3.up, 2f);
        var player = GameObject.FindWithTag("Player");
        if (player != null && Vector3.Distance(player.transform.position + Vector3.up * 0.9f, at) < 2.1f)
        {
            var ph = player.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(damage, owner != null ? owner.transform : null, 0f);
        }
        AcidPool.Create(at, 1.7f, damage * 0.6f, 4.5f);
        Destroy(gameObject);
    }
}

public class AcidPool : MonoBehaviour
{
    const int Sectors = 72, Rings = 7;
    float dps, life, age;
    readonly float[] outline = new float[Sectors];
    Mesh surface;
    MeshRenderer surfaceRenderer;
    MaterialPropertyBlock properties;
    static Material puddleMaterial;
    static readonly int Fade = Shader.PropertyToID("_Fade"), Spread = Shader.PropertyToID("_Spread");
    float Growth => Mathf.Lerp(.65f, 1f, Mathf.Clamp01(age * 4f));

    public static AcidPool Create(Vector3 at, float radius, float dps, float life)
    {
        at = GroundBelow(at);
        if (puddleMaterial == null) puddleMaterial = Resources.Load<Material>("FX/AcidPuddle");
        var go = new GameObject("Acid Pool");
        go.transform.position = at;
        var p = go.AddComponent<AcidPool>();
        p.dps = dps; p.life = Mathf.Max(.1f, life);
        p.surface = p.BuildSurface(Mathf.Max(.1f, radius));
        go.AddComponent<MeshFilter>().sharedMesh = p.surface;
        p.surfaceRenderer = go.AddComponent<MeshRenderer>();
        p.surfaceRenderer.sharedMaterial = puddleMaterial;
        p.surfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        p.surfaceRenderer.receiveShadows = false;
        p.properties = new MaterialPropertyBlock();
        p.UpdateAppearance();
        return p;
    }

    // the ground under a point, ignoring characters (otherwise the pool lands on someone's head)
    public static Vector3 GroundBelow(Vector3 at)
    {
        var hits = Physics.RaycastAll(at + Vector3.up * 3f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<PlayerHealth>() != null || h.collider.GetComponentInParent<EnemyController>() != null) continue;
            return h.point;
        }
        return at;
    }

    Mesh BuildSurface(float radius)
    {
        float phase = Random.Range(0f, Mathf.PI * 2), phase2 = Random.Range(0f, Mathf.PI * 2);
        float stretch = Random.Range(.76f, 1.24f), rotation = Random.Range(0f, Mathf.PI * 2), seed = Random.Range(0f, 500f);
        // Smooth lobes and smaller shoreline variations, with no repeated disc.
        for (int i = 0; i < Sectors; i++)
        {
            float a = i * Mathf.PI * 2 / Sectors;
            float ellipse = 1 / Mathf.Sqrt(Mathf.Pow(Mathf.Cos(a - rotation) / stretch, 2) + Mathf.Pow(Mathf.Sin(a - rotation) * stretch, 2));
            float lobes = 1 + .20f * Mathf.Sin(3*a + phase) + .12f * Mathf.Sin(5*a + phase2) + .065f * Mathf.Sin(9*a - phase);
            float shore = .10f * (Mathf.PerlinNoise(seed + Mathf.Cos(a)*2, seed + Mathf.Sin(a)*2) - .5f);
            outline[i] = radius * ellipse * (lobes + shore);
        }
        var vertices = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();
        Vector3 Project(float x, float z)
        {
            Vector3 ground = GroundBelow(transform.position + new Vector3(x, 0, z));
            return new Vector3(x, ground.y - transform.position.y + .018f, z);
        }
        vertices.Add(Project(0, 0)); uv.Add(Vector2.zero);
        for (int ring = 1; ring <= Rings; ring++)
            for (int i = 0; i < Sectors; i++)
            {
                float a = i * Mathf.PI * 2 / Sectors, fraction = ring / (float)Rings;
                vertices.Add(Project(Mathf.Cos(a)*outline[i]*fraction, Mathf.Sin(a)*outline[i]*fraction));
                uv.Add(new Vector2(fraction, 0));
            }
        for (int i = 0; i < Sectors; i++)
        {
            int next = (i + 1) % Sectors;
            triangles.AddRange(new[]{0, 1 + next, 1 + i});
            for (int ring = 1; ring < Rings; ring++)
            {
                int a = 1 + (ring-1)*Sectors+i, b = 1 + (ring-1)*Sectors+next;
                triangles.AddRange(new[]{a, b, a+Sectors, b, b+Sectors, a+Sectors});
            }
        }
        // Small satellite splashes share the same draw call and lifetime.
        for (int drop = 0; drop < 12; drop++)
        {
            int sector = Random.Range(0, Sectors); float a = sector*Mathf.PI*2/Sectors;
            float distance = outline[sector]*Random.Range(1.08f, 1.27f), size = radius*Random.Range(.025f,.065f);
            float x = Mathf.Cos(a)*distance, z = Mathf.Sin(a)*distance; int start = vertices.Count;
            vertices.Add(Project(x,z)); uv.Add(Vector2.zero);
            for(int j=0;j<10;j++)
            {
                float angle=j*Mathf.PI*2/10;
                vertices.Add(Project(x+Mathf.Cos(angle)*size,z+Mathf.Sin(angle)*size*.7f)); uv.Add(new Vector2(1,1));
            }
            for(int j=0;j<10;j++)triangles.AddRange(new[]{start,start+1+(j+1)%10,start+1+j});
        }
        var mesh = new Mesh { name = "Unique ground-fitted acid splash" };
        var coordinates=new System.Collections.Generic.List<Vector2>(vertices.Count);
        foreach(var vertex in vertices)coordinates.Add(new Vector2(vertex.x,vertex.z));
        mesh.SetVertices(vertices); mesh.SetUVs(0,coordinates); mesh.SetUVs(1,uv); mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        return mesh;
    }

    // Match the damaging footprint to the visible lobed pool, including its spread.
    public bool Contains(Vector3 position)
    {
        Vector3 delta = position-transform.position;
        if(Mathf.Abs(delta.y)>1.2f)return false;
        float angle = Mathf.Repeat(Mathf.Atan2(delta.z,delta.x),Mathf.PI*2)*Sectors/(Mathf.PI*2);
        int i = Mathf.FloorToInt(angle); float limit = Mathf.Lerp(outline[i],outline[(i+1)%Sectors],angle-i)*Growth;
        return delta.x*delta.x+delta.z*delta.z < limit*limit;
    }

    void UpdateAppearance()
    {
        properties.SetFloat(Fade,Mathf.Clamp01(life-age)); properties.SetFloat(Spread,Growth);
        surfaceRenderer.SetPropertyBlock(properties);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        UpdateAppearance();

        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        if (Contains(player.transform.position))
        {
            var ph = player.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeBurn(dps * Time.deltaTime);
        }
    }

    void OnDestroy() { if(surface != null) Destroy(surface); }
}
