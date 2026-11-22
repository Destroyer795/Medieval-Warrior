using UnityEngine;
using UnityEngine.Rendering;

// Projects heraldry onto the real plate, following the curve and animation
// of the shield in both equipment positions.
internal sealed class ShieldHeraldry : MonoBehaviour
{
    static Mesh face;
    static Material paint;
    MeshRenderer marks;

    internal static void Attach(Transform shield, Mesh source)
    {
        if (source == null || source.subMeshCount <= 12) return;
        if (face == null)
        {
            face = new Mesh { name = "Heater heraldry surface" };
            face.indexFormat = source.indexFormat;
            face.vertices = source.vertices;
            face.normals = source.normals;
            face.triangles = source.GetTriangles(12);
            face.bounds = source.bounds;
        }
        if (paint == null)
        {
            var shader = Resources.Load<Shader>("Marchwarden/ShieldHeraldry");
            if (shader == null) return;
            paint = new Material(shader) { name = "Painted shield heraldry" };
        }

        var go = new GameObject("Painted crest");
        go.transform.SetParent(shield, false);
        go.AddComponent<MeshFilter>().sharedMesh = face;
        var heraldry = go.AddComponent<ShieldHeraldry>();
        heraldry.marks = go.AddComponent<MeshRenderer>();
        heraldry.marks.sharedMaterial = paint;
        heraldry.marks.shadowCastingMode = ShadowCastingMode.Off;
        heraldry.Apply();
    }

    internal void Apply()
    {
        int crest = GameSettings.Crest;
        marks.enabled = crest != 1;
        var tabard = GameSettings.TabardColors[
            Mathf.Clamp(GameSettings.Tabard, 0, GameSettings.TabardColors.Length - 1)].color;
        var properties = new MaterialPropertyBlock();
        properties.SetFloat("_Crest", crest);
        properties.SetColor("_Paint", Color.Lerp(tabard, new Color(.92f, .83f, .60f), .6f));
        marks.SetPropertyBlock(properties);
    }
}
