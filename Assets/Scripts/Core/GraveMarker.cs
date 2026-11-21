using System.Collections.Generic;
using TMPro;
using UnityEngine;

// A shouldered limestone headstone with a stepped plinth, recessed face and weathered bevels.
internal sealed class GraveMarker : MonoBehaviour
{
    Mesh carving;

    internal static Transform Create(Transform parent, string name)
    {
        var stone = new GameObject("Carved headstone").AddComponent<GraveMarker>();
        stone.transform.SetParent(parent, false);
        stone.carving = BuildStone();
        stone.gameObject.AddComponent<MeshFilter>().sharedMesh = stone.carving;
        var surface = stone.gameObject.AddComponent<MeshRenderer>();
        surface.sharedMaterial = HordeVFX.Stone;
        var tint = new MaterialPropertyBlock();
        tint.SetColor("_Color", new Color(.89f,.87f,.77f));
        surface.SetPropertyBlock(tint);

        Letter(stone.transform, "Epitaph", "HERE LIES", 1.45f, .96f, .17f);
        Letter(stone.transform, "Engraved name", name, 1.04f, 1.64f, .38f);
        // A glancing light picks out the bevel and the cold edge of the emerging silhouette.
        var light = new GameObject("Graveside light").AddComponent<Light>();
        light.transform.SetParent(stone.transform, false);
        light.transform.localPosition = new Vector3(-1.25f, 1.85f, -1.4f);
        light.type = LightType.Point;
        light.color = new Color(.74f,.85f,1f);
        light.range = 5.6f;
        light.intensity = 2.5f;
        return stone.transform;
    }

    static void Letter(Transform parent, string label, string content, float y, float width, float height)
    {
        var text = new GameObject(label).AddComponent<TextMeshPro>();
        text.transform.SetParent(parent, false);
        text.transform.localPosition = new Vector3(0f, y, -.223f);
        text.font = UIKit.TitleFont;
        text.color = new Color(.96f,.87f,.66f);
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.Center;
        text.text = content;
        text.fontSize = 5f;
        // Measure the rendered glyphs. Long hero names shrink proportionally within the panel.
        text.rectTransform.sizeDelta = new Vector2(100f, 12f);
        text.ForceMeshUpdate();
        Vector3 size = text.textBounds.size;
        text.transform.localScale = Vector3.one * Mathf.Min(width / Mathf.Max(.01f,size.x), height / Mathf.Max(.01f,size.y));
    }

    static Mesh BuildStone()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();
        var colors = new List<Color32>();
        Color32 stoneColor = new Color32(241, 241, 234, 255);

        // Each polygon is wound counterclockwise in the XY plane. Individual faces keep
        // their chiseled edges and project the shared stone texture in world-sized units.
        void Face(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b-a,c-a);
            foreach (Vector3 v in new[]{a,b,c})
            {
                triangles.Add(vertices.Count);
                vertices.Add(v);
                colors.Add(stoneColor);
                uvs.Add(Mathf.Abs(normal.z) >= Mathf.Max(Mathf.Abs(normal.x),Mathf.Abs(normal.y)) ?
                    new Vector2(v.x,v.y) : Mathf.Abs(normal.y) > Mathf.Abs(normal.x) ? new Vector2(v.x,v.z) : new Vector2(v.z,v.y));
            }
        }
        void Carve(Vector2[] outline, Vector2 middle, float back, float front, float bevel)
        {
            Vector3 Point(int index, float z, float inset)
            {
                var xy = Vector2.Lerp(outline[index % outline.Length], middle, inset);
                return new Vector3(xy.x,xy.y,z);
            }
            for(int i=0;i<outline.Length;i++)
            {
                Vector3 a=Point(i,front+bevel,0), b=Point(i+1,front+bevel,0);
                Vector3 c=Point(i,back,0), d=Point(i+1,back,0);
                Vector3 e=Point(i,front,bevel), f=Point(i+1,front,bevel);
                Face(c,b,d); Face(c,a,b);
                Face(a,e,f); Face(a,f,b);
                Face(new Vector3(middle.x,middle.y,front),f,e);
                Face(new Vector3(middle.x,middle.y,back),c,d);
            }
        }
        void Base(float half, float bottom, float top, float depth) =>
            Carve(new[]{new Vector2(-half,bottom), new Vector2(half,bottom), new Vector2(half,top),new Vector2(-half,top)},
                new Vector2(0f,(bottom+top)*.5f),depth,-depth,.045f);
        Base(1.12f,.01f,.16f,.43f);
        Base(1.01f,.15f,.29f,.35f);
        Carve(new[]{new Vector2(-.94f,.27f),new Vector2(.94f,.27f),new Vector2(.94f,1.55f),
            new Vector2(.83f,1.64f),new Vector2(.72f,1.69f),new Vector2(.64f,1.85f),new Vector2(.43f,2.05f),
            new Vector2(.19f,2.16f),new Vector2(0f,2.2f),new Vector2(-.24f,2.14f),new Vector2(-.46f,2.03f),
            new Vector2(-.66f,1.84f),new Vector2(-.72f,1.68f),new Vector2(-.86f,1.62f),new Vector2(-.94f,1.55f)},
            new Vector2(0f,1.12f),.22f,-.175f,.07f);
        stoneColor = new Color32(169, 175, 174, 255);
        Carve(new[]{new Vector2(-.86f,.42f),new Vector2(.86f,.42f),new Vector2(.86f,1.49f),
            new Vector2(.66f,1.64f),new Vector2(0f,1.94f),new Vector2(-.66f,1.64f),new Vector2(-.86f,1.49f)},
            new Vector2(0f,1.02f),-.166f,-.216f,.045f);
        stoneColor = new Color32(223, 218, 191, 255);
        Carve(new[]{new Vector2(0f,.53f),new Vector2(.07f,.6f),new Vector2(0f,.67f),new Vector2(-.07f,.6f)},
            new Vector2(0f,.6f),-.211f,-.245f,.035f);
        var mesh = new Mesh{name="Limestone grave carving"};
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles,0);
        mesh.SetUVs(0,uvs);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    void OnDestroy()
    {
        if (carving != null) Destroy(carving);
    }
}
