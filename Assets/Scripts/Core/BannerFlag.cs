using UnityEngine;

// A heraldic banner on a pole, waving in the wind. The cloth is a small grid mesh whose
// vertices are animated with a couple of travelling sine waves (cheap, no cloth physics).
[ExecuteAlways]
public class BannerFlag : MonoBehaviour
{
    public Color field = new Color(0.62f, 0.08f, 0.08f);
    public Color trim = new Color(0.95f, 0.75f, 0.3f);
    public float width = 2.2f;
    public float height = 1.4f;
    public float poleHeight = 4f;
    public float windStrength = 1f;

    const int Cols = 12, Rows = 6;
    Mesh mesh;
    Vector3[] rest, verts;
    float phase;
    Transform cloth;

    void OnEnable()
    {
        phase = Random.Range(0f, 10f);
        Build();
    }

    void Build()
    {
        // the cloth is never saved (DontSave), but re-enabling in the editor must not stack copies
        var oldCloth = transform.Find("Cloth");
        if (oldCloth != null)
        {
            if (Application.isPlaying) Destroy(oldCloth.gameObject); else DestroyImmediate(oldCloth.gameObject);
        }
        // pole
        var poleGo = transform.Find("Pole")?.gameObject;
        if (poleGo == null)
        {
            var k = new ProceduralModels.MeshKit(2);
            k.Tube(0, Vector3.zero, Vector3.up * poleHeight, 0.06f, 0.045f, 6);
            k.Ball(1, Vector3.up * (poleHeight + 0.06f), Vector3.one * 0.12f, 3, 6);
            poleGo = ProceduralModels.Spawn("Pole", k.Build("BannerPole"), new[]
            {
                ProceduralModels.Mat("banner_pole", new Color(0.32f, 0.22f, 0.14f), 0.2f),
                ProceduralModels.Mat("banner_finial", new Color(0.95f, 0.75f, 0.3f), 0.6f, 0.8f),
            }, transform);
            poleGo.hideFlags = HideFlags.DontSave;
        }

        // cloth: a grid hanging from the top of the pole, with a gold trim band at the bottom
        var go = new GameObject("Cloth") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * (poleHeight - 0.15f);
        cloth = go.transform;
        mesh = new Mesh { name = "Banner", hideFlags = HideFlags.DontSave };
        rest = new Vector3[(Cols + 1) * (Rows + 1)];
        var colors = new Color[rest.Length];
        for (int r = 0; r <= Rows; r++)
            for (int c = 0; c <= Cols; c++)
            {
                int i = r * (Cols + 1) + c;
                rest[i] = new Vector3(c / (float)Cols * width, -r / (float)Rows * height, 0f);
                colors[i] = r >= Rows - 1 || c == Cols ? trim : field;
            }
        // single-sided; the shader culls nothing and flips the normal for the back face
        var tris = new int[Cols * Rows * 6];
        int t = 0;
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                int a = r * (Cols + 1) + c, b = a + 1, d = a + Cols + 1, e = d + 1;
                tris[t++] = a; tris[t++] = b; tris[t++] = d; tris[t++] = b; tris[t++] = e; tris[t++] = d;
            }
        verts = (Vector3[])rest.Clone();
        mesh.vertices = verts;
        mesh.colors = colors;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = BannerMaterial();
    }

    static Material bannerMat;
    static Material BannerMaterial()
    {
        if (bannerMat != null) return bannerMat;
        bannerMat = Resources.Load<Material>("FX/Banner");
        if (bannerMat == null) bannerMat = new Material(Shader.Find("Standard")) { color = Color.white };
        return bannerMat;
    }

    void Update()
    {
        if (mesh == null) return;
        float time = (Application.isPlaying ? Time.time : (float)Time.realtimeSinceStartup) + phase;
        for (int i = 0; i < rest.Length; i++)
        {
            Vector3 p = rest[i];
            float along = p.x / width;   // 0 at the pole, 1 at the free edge
            float wave = Mathf.Sin(time * 3.1f - p.x * 2.3f) * 0.28f + Mathf.Sin(time * 5.3f - p.x * 4.1f + p.y * 2f) * 0.08f;
            p.z = wave * along * windStrength;
            p.y += -Mathf.Abs(wave) * 0.12f * along;
            verts[i] = p;
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals();
    }
}
