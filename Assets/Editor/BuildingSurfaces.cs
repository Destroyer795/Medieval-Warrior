using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class BuildingBuilder
{
    static readonly Dictionary<string,Material> houseSurfaces=new Dictionary<string,Material>();
    static bool IsHouse(BuiltPart part)=>part.name=="House"||part.name=="HouseB";
    static Material SurfaceFor(BuiltPart part,int lod,Material fallback)
    {
        return IsHouse(part)?houseSurfaces[part.name+"_LOD"+lod]:architectureSurfaces.TryGetValue(part.key,out var surface)?surface:fallback;
    }
    static void PrepareHouseSurfaces(Material source)
    {
        Directory.CreateDirectory(Output+"/Materials");AssetDatabase.Refresh();houseSurfaces.Clear();
        foreach(string house in new[]{"House","HouseB"})for(int lod=0;lod<3;lod++)
        {
            string name=house+"_LOD"+lod;string aoPath=Output+"/Textures/HouseAO/"+name+".png";
            var importer=AssetImporter.GetAtPath(aoPath) as TextureImporter;
            if(importer==null)throw new Exception("Generate the dry house textures/contact AO first: blender --background --python Tools/ArtPipeline/buildings.py -- --only Textures");
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=false;importer.mipmapEnabled=true;importer.maxTextureSize=2048;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            string path=Output+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(source);AssetDatabase.CreateAsset(material,path);}else material.CopyPropertiesFromMaterial(source);
            material.shader=source.shader;
            material.name=name+" - dry lime, aged timber and unglazed clay";
            material.SetFloat("_Glossiness",0);material.SetFloat("_SpecularHighlights",0);material.SetFloat("_GlossyReflections",0);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            material.SetTexture("_OcclusionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath));material.SetFloat("_OcclusionStrength",1);
            var model=JsonUtility.FromJson<Reference>(File.ReadAllText(ReferenceFile)).meshes.First(p=>p.name==house);
            ConfigureMeasuredSurface(material,model);material.SetFloat("_VertexAO",0);
            material.enableInstancing=true;EditorUtility.SetDirty(material);houseSurfaces[name]=material;
        }
    }
    static int ApplySurfaceOnly(GameObject root,Dictionary<string,Material> lookup,bool scene)
    {
        int count=0;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.sharedMesh==null||!lookup.TryGetValue(AssetDatabase.GetAssetPath(filter.sharedMesh),out var material))continue;
            var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null)continue;
            if(renderer.sharedMaterials.Length==1&&renderer.sharedMaterial==material)continue;
            renderer.sharedMaterials=new[]{material};EditorUtility.SetDirty(renderer);
            if(scene&&PrefabUtility.IsPartOfPrefabInstance(renderer))PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            count++;
        }
        return count;
    }
    [MenuItem("Medieval Warrior/Buildings/Refresh matte house surfaces")]
    public static void RefreshSurfaces()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        AssetDatabase.Refresh();var surface=BuildSurface();var manifest=ReadManifest();var lookup=new Dictionary<string,Material>();
        foreach(var part in manifest.parts)for(int lod=0;lod<3;lod++)lookup[MeshPath(part.key,lod)]=SurfaceFor(part,lod,surface);
        var reference=JsonUtility.FromJson<Reference>(File.ReadAllText(ReferenceFile));int count=0;
        foreach(var prefab in reference.prefabs)
        {
            var root=PrefabUtility.LoadPrefabContents(prefab.path);
            try{int changes=ApplySurfaceOnly(root,lookup,false);if(changes>0)PrefabUtility.SaveAsPrefabAsset(root,prefab.path);count+=changes;}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(string path in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);int changes=0;
            foreach(var root in scene.GetRootGameObjects())changes+=ApplySurfaceOnly(root,lookup,true);
            if(changes>0)EditorSceneManager.SaveScene(scene);count+=changes;
        }
        AssetDatabase.SaveAssets();
        foreach(var entry in houseSurfaces)
        {
            var material=entry.Value;
            if(material.GetFloat("_SpecularHighlights")!=0||material.GetFloat("_GlossyReflections")!=0||!material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF")||!material.IsKeywordEnabled("_GLOSSYREFLECTIONS_OFF"))
                throw new Exception("House still has reflective lighting enabled: "+entry.Key);
        }
        // Read the actual PNG channel data; all targeted atlas cells must have zero smoothness.
        var mask=new Texture2D(2,2,TextureFormat.RGBA32,false,true);ImageConversion.LoadImage(mask,File.ReadAllBytes(Output+"/Textures/Masks.png"));
        var pixels=mask.GetPixels32();int tile=mask.width/4;
        foreach(int region in new[]{2,3,4,7})
        {
            int x=region%4*tile,y=region/4*tile;
            for(int yy=y;yy<y+tile;yy++)for(int xx=x;xx<x+tile;xx++)if(pixels[yy*mask.width+xx].a!=0)throw new Exception("Nonzero smoothness on dry house surface tile "+region);
        }
        UnityEngine.Object.DestroyImmediate(mask);
        Debug.Log("[Buildings] Dry surface PASS: six non-reflective house/LOD materials, zero plaster/timber/clay smoothness, geometry-derived AO; updated "+count+" renderer assignments");
    }
    static void CaptureHouseInWorldLight()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Scenes[0]);
        var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First(c=>c.CompareTag("MainCamera"));
        var player=scene.GetRootGameObjects().First(g=>g.CompareTag("Player"));
        string housePath=MeshPath(ReadManifest().parts.First(p=>p.name=="House").key,0);
        var houses=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Scenes[1]).GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true))
            .Where(f=>f.sharedMesh!=null&&AssetDatabase.GetAssetPath(f.sharedMesh)==housePath);
        var house=houses.OrderBy(f=>(f.transform.position-player.transform.position).sqrMagnitude).First();var bounds=house.GetComponent<MeshRenderer>().bounds;
        var position=camera.transform.position;var rotation=camera.transform.rotation;var previous=camera.targetTexture;float near=camera.nearClipPlane;
        var groups=house.GetComponentsInChildren<LODGroup>();foreach(var group in groups)group.ForceLOD(0);
        var target=new RenderTexture(1600,1200,24);var texture=new Texture2D(1600,1200,TextureFormat.RGB24,false);
        try
        {
            float span=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);camera.nearClipPlane=.05f;
            camera.transform.position=bounds.center+house.transform.TransformDirection(new Vector3(1,.38f,-1.2f)).normalized*span*1.55f;
            camera.transform.LookAt(bounds.center);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1600,1200),0,0);texture.Apply();
            File.WriteAllBytes("ArtSource/Buildings/Review/House_WorldLighting.png",texture.EncodeToPNG());
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position,rotation);camera.targetTexture=previous;camera.nearClipPlane=near;RenderTexture.active=null;
            foreach(var group in groups)group.ForceLOD(-1);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);
        }
    }
    public static void RefreshSurfacesAndReview(){RefreshSurfaces();Review();InspectNavigation();CaptureHouseInWorldLight();}
}
