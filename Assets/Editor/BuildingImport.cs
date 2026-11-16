using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static partial class BuildingBuilder
{
    [Serializable] public class BuiltPart {public string key,name,collision;public string[] files;}
    [Serializable] public class Manifest {public BuiltPart[] parts;}
    static Manifest ReadManifest()=>JsonUtility.FromJson<Manifest>(File.ReadAllText("Tools/ArtPipeline/Buildings/manifest.json"));
    static string MeshPath(string key,int lod)=>Output+"/Meshes/"+key+"_LOD"+lod+".asset";
    static string CollisionPath(string key)=>Output+"/Collision/"+key+".asset";
    static Mesh LoadBuffer(string filename,string destination,bool collision)
    {
        using(var reader=new BinaryReader(File.OpenRead("Tools/ArtPipeline/Buildings/"+filename)))
        {
            if(reader.ReadInt32()!=0x424c4431)throw new InvalidDataException(filename);
            int count=reader.ReadInt32(),indices=reader.ReadInt32();
            var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var lightmap=new Vector2[count];
            for(int i=0;i<count;i++)
            {
                vertices[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                normals[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());lightmap[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
            }
            var triangles=new int[indices];for(int i=0;i<indices;i++)triangles[i]=reader.ReadInt32();
            if(collision)
            {
                // Weld only the collision buffer's split face corners, not independent render UV islands.
                var unique=new List<Vector3>();var lookup=new Dictionary<Vector3,int>();var map=new int[count];
                for(int i=0;i<count;i++){if(!lookup.TryGetValue(vertices[i],out int j)){j=unique.Count;lookup[vertices[i]]=j;unique.Add(vertices[i]);}map[i]=j;}
                vertices=unique.ToArray();for(int i=0;i<indices;i++)triangles[i]=map[triangles[i]];
            }
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(destination);
            if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,destination);}else mesh.Clear();
            mesh.name=Path.GetFileNameWithoutExtension(filename);mesh.indexFormat=vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16;
            mesh.vertices=vertices;mesh.triangles=triangles;
            if(!collision){mesh.normals=normals;mesh.uv=uv;mesh.uv2=lightmap;mesh.RecalculateTangents();}else mesh.RecalculateNormals();
            mesh.RecalculateBounds();mesh.UploadMeshData(false);EditorUtility.SetDirty(mesh);return mesh;
        }
    }
    static Material BuildSurface()
    {
        string path=Output+"/Buildings.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
        mat.shader=Shader.Find("Standard");mat.color=Color.white;mat.enableInstancing=true;
        foreach(string label in new[]{"Albedo","Normal","Masks"})
        {
            string texturePath=Output+"/Textures/"+label+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType=label=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=label=="Albedo";importer.maxTextureSize=4096;importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Clamp;importer.anisoLevel=8;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(label=="Albedo")mat.SetTexture("_MainTex",texture);
            else if(label=="Normal")mat.SetTexture("_BumpMap",texture);
            else{mat.SetTexture("_MetallicGlossMap",texture);mat.SetTexture("_OcclusionMap",texture);}
        }
        mat.SetFloat("_GlossMapScale",1);mat.SetFloat("_BumpScale",.65f);mat.SetFloat("_OcclusionStrength",.55f);
        mat.SetFloat("_SmoothnessTextureChannel",0);mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICGLOSSMAP");
        EditorUtility.SetDirty(mat);return mat;
    }
    static BuiltPart FindPart(MeshFilter filter,Manifest manifest)
    {
        if(filter.transform.name.StartsWith("Buildings LOD"))return null;
        string key=filter.sharedMesh!=null?Key(filter.sharedMesh):null;
        var part=manifest.parts.FirstOrDefault(p=>p.key==key||MeshPath(p.key,0)==AssetDatabase.GetAssetPath(filter.sharedMesh));
        // Some vendor Castle children already reference a non-existent FBX subasset. Repair by the
        // explicitly named module, retaining their transforms and component file IDs.
        if(part==null&&filter.sharedMesh==null)part=manifest.parts.FirstOrDefault(p=>p.name==filter.name);
        return part;
    }
    static int ReplaceIn(GameObject root,Manifest manifest,Material material,bool prefab)
    {
        int changed=0;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true).ToArray())
        {
            var part=FindPart(filter,manifest);if(part==null)continue;
            filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(part.key,0));
            var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null)renderer=filter.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials=new[]{material};renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            if(filter.GetComponent<MeshCollider>()==null)filter.gameObject.AddComponent<MeshCollider>();
            foreach(var collider in filter.GetComponents<MeshCollider>())
            {
                collider.sharedMesh=null;collider.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(CollisionPath(part.key));
                collider.cookingOptions=MeshColliderCookingOptions.CookForFasterSimulation|MeshColliderCookingOptions.EnableMeshCleaning|MeshColliderCookingOptions.WeldColocatedVertices|MeshColliderCookingOptions.UseFastMidphase;
                EditorUtility.SetDirty(collider);
            }
            var group=filter.GetComponent<LODGroup>();if(group==null)group=filter.gameObject.AddComponent<LODGroup>();
            var renderers=new Renderer[3];renderers[0]=renderer;
            for(int lod=1;lod<3;lod++)
            {
                string nodeName="Buildings LOD"+lod;var node=filter.transform.Find(nodeName);
                if(node==null){node=new GameObject(nodeName).transform;node.SetParent(filter.transform,false);}
                node.gameObject.layer=filter.gameObject.layer;
                // Preserve the module's lightmap/static flags, but never add collision to the LOD nodes.
                GameObjectUtility.SetStaticEditorFlags(node.gameObject,GameObjectUtility.GetStaticEditorFlags(filter.gameObject));
                var mf=node.GetComponent<MeshFilter>();if(mf==null)mf=node.gameObject.AddComponent<MeshFilter>();mf.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(part.key,lod));
                var mr=node.GetComponent<MeshRenderer>();if(mr==null)mr=node.gameObject.AddComponent<MeshRenderer>();mr.sharedMaterials=new[]{material};renderers[lod]=mr;
            }
            group.SetLODs(new[]{new LOD(.38f,new[]{renderers[0]}),new LOD(.13f,new[]{renderers[1]}),new LOD(.008f,new[]{renderers[2]})});
            group.fadeMode=LODFadeMode.None;group.RecalculateBounds();
            EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(group);
            if(!prefab&&PrefabUtility.IsPartOfPrefabInstance(filter))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                foreach(var c in filter.GetComponents<MeshCollider>())PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            }
            changed++;
        }
        return changed;
    }
    [MenuItem("Medieval Warrior/Buildings/Import Blender kit and review")]
    public static void BuildAndReview()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        Build();Review();
    }
    [MenuItem("Medieval Warrior/Buildings/Import Blender kit")]
    public static void Build()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var reference=JsonUtility.FromJson<Reference>(File.ReadAllText(ReferenceFile));var manifest=ReadManifest();
        Directory.CreateDirectory(Output+"/Meshes");Directory.CreateDirectory(Output+"/Collision");AssetDatabase.Refresh();
        var surface=BuildSurface();
        foreach(var part in manifest.parts)
        {
            for(int lod=0;lod<3;lod++)LoadBuffer(part.files[lod],MeshPath(part.key,lod),false);
            LoadBuffer(part.collision,CollisionPath(part.key),true);
        }
        AssetDatabase.SaveAssets();int count=0;
        foreach(var prefab in reference.prefabs)
        {
            var root=PrefabUtility.LoadPrefabContents(prefab.path);
            try{count+=ReplaceIn(root,manifest,surface,true);PrefabUtility.SaveAsPrefabAsset(root,prefab.path);}finally{PrefabUtility.UnloadPrefabContents(root);}
            if(AssetDatabase.AssetPathToGUID(prefab.path)!=prefab.guid)throw new Exception("Prefab GUID changed: "+prefab.path);
        }
        // Keep scene property overrides (including source FBX overrides) consistent with prefab assets.
        // No transform, terrain pad, spawn point or layout-generation code is run here.
        foreach(string scenePath in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
            int changes=0;
            foreach(var root in scene.GetRootGameObjects())changes+=ReplaceIn(root,manifest,surface,false);
            if(changes>0)EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();Debug.Log("[Buildings] Imported "+manifest.parts.Length+" custom modules; updated "+count+" prefab mesh nodes with preserved GUIDs");
    }
}
