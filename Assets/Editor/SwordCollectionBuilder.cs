using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SwordCollectionBuilder
{
    const string Root = "Assets/Resources/SwordCollection";
    [Serializable] class Surface { public string id, color; public float metallic, smoothness; }
    [Serializable] class Slot { public int[] indices; }
    [Serializable] class Model { public string id; public Vector3[] vertices, normals; public Slot[] slots; public string[] finishes; }
    [Serializable] class Collection { public Model[] models; public Surface[] materials; }

    [MenuItem("Medieval Warrior/Art/Build sword collection")]
    public static void Build()
    {
        var data = JsonUtility.FromJson<Collection>(File.ReadAllText("Artifacts/SwordCollection/geometry.json"));
        Directory.CreateDirectory(Root + "/Meshes");
        Directory.CreateDirectory(Root + "/Materials");
        AssetDatabase.Refresh();
        var materials = data.materials.ToDictionary(s => s.id, s =>
        {
            string path = Root + "/Materials/" + s.id + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (!ColorUtility.TryParseHtmlString("#" + s.color, out var color)) throw new InvalidDataException(s.id);
            mat.color = color;
            mat.SetFloat("_Metallic", s.metallic);
            mat.SetFloat("_Glossiness", s.smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        });
        foreach (var model in data.models)
        {
            string path = Root + "/Meshes/" + model.id + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.name = model.id;
            mesh.vertices = model.vertices;
            mesh.normals = model.normals;
            mesh.subMeshCount = model.slots.Length;
            for (int i = 0; i < model.slots.Length; i++) mesh.SetTriangles(model.slots[i].indices, i);
            EditorUtility.SetDirty(mesh);
            var go = new GameObject(model.id);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = model.finishes.Select(key => materials[key]).ToArray();
                PrefabUtility.SaveAsPrefabAsset(go, Root + "/" + model.id + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        AssetDatabase.SaveAssets();
    }
}
