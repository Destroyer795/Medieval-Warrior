using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class BuildingBuilder
{
    [Serializable] class EnvironmentFinishReport
    {
        public string result="PASS";public int architectureMeshes,bakedAOVertices,groundProbes,groundTriangles,crateVertices;
        public string crateMeshGuid,cratePrefabGuid;public float parallaxMetres=.035f,parallaxFadeDistance=18;
        public string[] sources={"https://polyhaven.com/a/castle_wall_slates","https://polyhaven.com/a/rock_wall_02"};
    }
    static readonly Dictionary<string,Material> architectureSurfaces=new Dictionary<string,Material>();
    static bool IsArchitectureMaterial(Material material)=>material!=null&&(material.shader.name=="Standard"||material.shader.name=="Medieval Warrior/Weathered Architecture");
    static void ApplyBakedAO(Mesh mesh,string filename)
    {
        string path="Tools/ArtPipeline/Buildings/"+filename;if(!File.Exists(path))return;
        using(var reader=new BinaryReader(File.OpenRead(path)))
        {
            int count=reader.ReadInt32();if(count!=mesh.vertexCount)throw new Exception("Baked AO vertex layout mismatch: "+mesh.name);
            var colours=new Color32[count];for(int i=0;i<count;i++)colours[i]=new Color32(255,255,255,reader.ReadByte());mesh.colors32=colours;
        }
    }
    static void ConfigureMasonry(Material material)
    {
        foreach(string label in new[]{"Albedo","Normal","Surface","RubbleAlbedo","RubbleNormal","RubbleSurface"})
        {
            string path=Output+"/Textures/Masonry/"+label+".png";var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new Exception("Run Blender's Tools/ArtPipeline/environment_finishes.py first.");
            importer.textureType=label.EndsWith("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=label.EndsWith("Albedo");importer.maxTextureSize=4096;importer.wrapMode=TextureWrapMode.Repeat;
            importer.mipmapEnabled=true;importer.anisoLevel=8;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            material.SetTexture(label.StartsWith("Rubble")?"_"+label:label=="Albedo"?"_StoneAlbedo":label=="Normal"?"_StoneNormal":"_StoneSurface",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }
        material.SetFloat("_StoneMetres",2.5f);material.SetFloat("_RubbleMetres",2);material.SetFloat("_ParallaxMetres",.035f);material.SetFloat("_ParallaxDistance",18);
        material.SetFloat("_StoneNormalStrength",1.35f);material.SetFloat("_WeatherStrength",.72f);material.SetFloat("_VertexAO",.65f);
    }
    static void ConfigureMeasuredSurface(Material material,SourceMesh source)
    {
        material.SetVector("_LocalCenter",(source.bounds.min+source.bounds.max)*.5f);material.SetVector("_LocalSize",source.bounds.max-source.bounds.min);
        material.SetFloat("_ProjectionMode",source.model=="Tower B"?1:source.name=="Castle"?2:0);
    }
    static void PrepareArchitectureSurfaces(Material basis)
    {
        architectureSurfaces.Clear();var reference=JsonUtility.FromJson<Reference>(File.ReadAllText(ReferenceFile));
        foreach(var source in reference.meshes.Where(s=>s.model!="Houses"))
        {
            string path=Output+"/Materials/"+source.key+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(basis);AssetDatabase.CreateAsset(material,path);}else material.CopyPropertiesFromMaterial(basis);
            material.shader=basis.shader;
            material.name=source.name+" - weathered PBR masonry";ConfigureMeasuredSurface(material,source);material.enableInstancing=true;
            EditorUtility.SetDirty(material);architectureSurfaces[source.key]=material;
        }
    }
    static void ImportArchitecturalAO()
    {
        foreach(var part in ReadManifest().parts)for(int lod=0;lod<3;lod++)
        {
            string file=part.key+"_LOD"+lod+".ao.bin";
            if(!File.Exists("Tools/ArtPipeline/Buildings/"+file))throw new Exception("Missing Blender AO bake: "+file);
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(part.key,lod));ApplyBakedAO(mesh,file);EditorUtility.SetDirty(mesh);
        }
        AssetDatabase.SaveAssets();
    }
    static void ImportSupplyCrate()
    {
        const string prefabPath="Assets/Resources/Prefabs/SupplyCrate.prefab";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);var original=prefab.GetComponent<MeshFilter>().sharedMesh;
        string meshPath=AssetDatabase.GetAssetPath(original),guid=AssetDatabase.AssetPathToGUID(meshPath);var bounds=original.bounds;
        var mesh=LoadBuffer("SupplyCrate.bin",meshPath,false);ApplyBakedAO(mesh,"SupplyCrate.ao.bin");
        if((mesh.bounds.min-bounds.min).magnitude>.0001f||(mesh.bounds.max-bounds.max).magnitude>.0001f||AssetDatabase.AssetPathToGUID(meshPath)!=guid)throw new Exception("Crate envelope or mesh GUID changed");
        var basis=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Buildings.mat");string path=Output+"/Materials/SupplyCrate.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(basis);AssetDatabase.CreateAsset(material,path);}else material.CopyPropertiesFromMaterial(basis);
        material.shader=basis.shader;
        material.name="Supply crate - aged oak, recessed planks and worn iron";
        material.SetVector("_LocalCenter",bounds.center);material.SetVector("_LocalSize",bounds.size);material.SetFloat("_ProjectionMode",0);material.SetFloat("_VertexAO",.65f);
        EditorUtility.SetDirty(material);
        var root=PrefabUtility.LoadPrefabContents(prefabPath);string collider=EditorJsonUtility.ToJson(root.GetComponent<BoxCollider>());
        try
        {
            root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshRenderer>().sharedMaterials=new[]{material};
            if(EditorJsonUtility.ToJson(root.GetComponent<BoxCollider>())!=collider)throw new Exception("Crate collision was changed");
            PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        string dataPath=Output+"/EnvironmentArt.asset";var data=AssetDatabase.LoadAssetAtPath<EnvironmentArtAsset>(dataPath);
        if(data==null){data=ScriptableObject.CreateInstance<EnvironmentArtAsset>();AssetDatabase.CreateAsset(data,dataPath);}
        data.supplyCrate=mesh;data.crateMaterial=material;EditorUtility.SetDirty(data);
        // Explicit scene material overrides also follow the updated source mesh.
        foreach(string scenePath in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);bool changed=false;
            foreach(var filter in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).Where(f=>f.sharedMesh==mesh))
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer.sharedMaterials.Length==1&&renderer.sharedMaterial==material)continue;
                renderer.sharedMaterials=new[]{material};PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);changed=true;
            }
            if(changed)EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();Debug.Log("[Environment finish] Crate: bevelled planks, PBR atlas and baked AO; original mesh GUID/collider retained");
    }
    static void ConfigureEnvironmentLighting(Scene game)
    {
        foreach(var scene in new[]{game,SceneManager.GetSceneByPath(Scenes[1])})
        {
            foreach(var sun in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).Where(l=>l.type==LightType.Directional))
            {
                sun.shadows=LightShadows.Soft;sun.shadowResolution=LightShadowResolution.VeryHigh;
                sun.shadowBias=.015f;sun.shadowNormalBias=.06f;sun.shadowNearPlane=.2f;EditorUtility.SetDirty(sun);
            }
        }
        foreach(var volume in game.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PostProcessVolume>()))
        {
            if(volume.sharedProfile!=null&&volume.sharedProfile.TryGetSettings<AmbientOcclusion>(out var ao))
            {ao.intensity.Override(.55f);ao.thicknessModifier.Override(1);ao.color.Override(new Color(.08f,.08f,.075f));EditorUtility.SetDirty(ao);}
        }
    }
    static void VerifyEnvironmentAssets()
    {
        var report=new EnvironmentFinishReport{groundProbes=bakedGroundProbes,groundTriangles=bakedGroundTriangles};
        var basis=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Buildings.mat");
        foreach(var part in ReadManifest().parts)for(int lod=0;lod<3;lod++)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(part.key,lod));var colours=mesh.colors32;
            if(colours.Length!=mesh.vertexCount||colours.Any(c=>c.a<128))throw new Exception("Invalid live baked AO stream: "+part.name+" LOD"+lod);
            // Verification also runs independently of a material rebuild, when the
            // importer's transient material dictionaries have not been populated.
            string materialName=IsHouse(part)?part.name+"_LOD"+lod:part.key;
            var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Materials/"+materialName+".mat");
            if(material==null||material.shader.name!="Medieval Warrior/Weathered Architecture"||material.GetTexture("_StoneNormal")==null||material.GetTexture("_RubbleNormal")==null)throw new Exception("Missing weathered PBR material: "+part.name);
            report.architectureMeshes++;report.bakedAOVertices+=colours.Length;
        }
        foreach(string name in new[]{"Normal","RubbleNormal"})
            if(((TextureImporter)AssetImporter.GetAtPath(Output+"/Textures/Masonry/"+name+".png")).textureType!=TextureImporterType.NormalMap)throw new Exception("Stone normal importer is incorrect");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/SupplyCrate.prefab");var box=prefab.GetComponent<BoxCollider>();
        if((box.size-new Vector3(1.02f,.65f,.73f)).magnitude>.0001f||prefab.GetComponent<MeshFilter>().sharedMesh!=EnvironmentArtAsset.Asset.supplyCrate)throw new Exception("Crate art/collision reference mismatch");
        report.crateVertices=EnvironmentArtAsset.Asset.supplyCrate.vertexCount;report.crateMeshGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(EnvironmentArtAsset.Asset.supplyCrate));report.cratePrefabGuid=AssetDatabase.AssetPathToGUID("Assets/Resources/Prefabs/SupplyCrate.prefab");
        File.WriteAllText("ArtSource/Buildings/environment-audit.json",JsonUtility.ToJson(report,true));
    }
    static void CaptureCrate()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.30f,.32f,.35f);
        var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.eulerAngles=new Vector3(35,-35,0);light.shadows=LightShadows.Soft;
        var crate=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/SupplyCrate.prefab"));
        var camera=new GameObject("Crate review").AddComponent<Camera>();camera.backgroundColor=new Color(.08f,.1f,.12f);camera.clearFlags=CameraClearFlags.SolidColor;
        camera.transform.position=new Vector3(1.3f,1.15f,-1.6f);camera.transform.LookAt(new Vector3(0,.30f,0));camera.orthographic=true;camera.orthographicSize=.85f;
        Shader.SetGlobalFloat("_EnvironmentGroundMode",2);Shader.SetGlobalFloat("_EnvironmentFlatGround",0);
        var target=new RenderTexture(1100,950,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var texture=new Texture2D(1100,950,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1100,950),0,0);texture.Apply();
        Directory.CreateDirectory("ArtSource/Props/Review");File.WriteAllBytes("ArtSource/Props/Review/SupplyCrate.png",texture.EncodeToPNG());
        RenderTexture.active=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(target);Shader.SetGlobalFloat("_EnvironmentGroundMode",0);
    }
    static void CaptureFinishWorldViews()
    {
        var game=SceneManager.GetSceneByPath(Scenes[0]);var world=SceneManager.GetSceneByPath(Scenes[1]);
        var camera=game.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First(c=>c.CompareTag("MainCamera"));
        var player=game.GetRootGameObjects().First(g=>g.CompareTag("Player"));
        var filters=world.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).Where(f=>f.sharedMesh!=null).ToArray();
        var sun=game.GetRootGameObjects().Concat(world.GetRootGameObjects()).SelectMany(g=>g.GetComponentsInChildren<Light>()).First(l=>l.type==LightType.Directional);
        var towardSun=-sun.transform.forward;towardSun.y=0;towardSun.Normalize();
        var reference=JsonUtility.FromJson<Reference>(File.ReadAllText(ReferenceFile));
        var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;var oldTarget=camera.targetTexture;float near=camera.nearClipPlane;
        foreach(string name in new[]{"Castle","House"})
        {
            var model=reference.meshes.First(m=>m.name==name);string path=MeshPath(model.key,0);
            var filter=filters.Where(f=>AssetDatabase.GetAssetPath(f.sharedMesh)==path).OrderBy(f=>(f.transform.position-player.transform.position).sqrMagnitude).First();
            var group=filter.GetComponent<LODGroup>();if(group!=null)group.ForceLOD(0);
            var center=(model.bounds.min+model.bounds.max)*.5f;Vector3 local;
            var lit=filter.transform.InverseTransformDirection(towardSun);lit.y=0;lit.Normalize();var size=model.bounds.max-model.bounds.min;
            if(name=="Castle")
            {
                float radius=size.z*.25f;local=new Vector3(center.x+Mathf.Sign(lit.x)*(size.x*.5f-radius)+lit.x*radius,model.bounds.min.y+.32f,center.z+Mathf.Sign(lit.z)*(size.z*.5f-radius)+lit.z*radius);
            }
            else local=Mathf.Abs(lit.x)>Mathf.Abs(lit.z)?new Vector3(center.x+Mathf.Sign(lit.x)*size.x*.5f,model.bounds.min.y+.19f,center.z):new Vector3(center.x,model.bounds.min.y+.19f,center.z+Mathf.Sign(lit.z)*size.z*.5f);
            var point=filter.transform.TransformPoint(local);float distance=name=="Castle"?4.5f:2.8f;
            camera.transform.position=point+towardSun*distance+Vector3.Cross(towardSun,Vector3.up)*distance*.30f+Vector3.up*.65f;camera.transform.LookAt(point);camera.nearClipPlane=.05f;
            var target=new RenderTexture(1500,1100,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var image=new Texture2D(1500,1100,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1500,1100),0,0);image.Apply();
            File.WriteAllBytes("ArtSource/Buildings/Review/"+(name=="Castle"?"Masonry_Closeup":"Foundation_Closeup")+".png",image.EncodeToPNG());
            camera.targetTexture=oldTarget;RenderTexture.active=null;Object.DestroyImmediate(image);Object.DestroyImmediate(target);if(group!=null)group.ForceLOD(-1);
        }
        camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.nearClipPlane=near;
    }
    [MenuItem("Medieval Warrior/Buildings/Apply environment PBR, weathering and baked contact")]
    public static void ApplyEnvironmentFinishAndReview()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        AssetDatabase.Refresh();ImportArchitecturalAO();RefreshSurfaces();ImportSupplyCrate();
        var game=EditorSceneManager.OpenScene(Scenes[0],OpenSceneMode.Single);EditorSceneManager.OpenScene(Scenes[1],OpenSceneMode.Additive);
        var terrain=Object.FindFirstObjectByType<Terrain>();if(terrain==null)throw new Exception("Environment terrain not loaded");
        BakeTerrainContact(game,terrain);ConfigureEnvironmentLighting(game);
        EditorSceneManager.MarkSceneDirty(game);EditorSceneManager.MarkSceneDirty(SceneManager.GetSceneByPath(Scenes[1]));
        EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        Review();InspectNavigation();CaptureHouseInWorldLight();CaptureFinishWorldViews();CaptureCrate();
        var shader=Shader.Find("Medieval Warrior/Weathered Architecture");
        if(ShaderUtil.ShaderHasError(shader))throw new Exception("Weathered architecture shader failed to compile: "+string.Join("; ",ShaderUtil.GetShaderMessages(shader).Select(m=>m.message)));
        VerifyEnvironmentAssets();
        Debug.Log("[Environment finish] PASS: 4K POM masonry, gravity/terrain weathering, baked vertex/ground AO, softer contact shadows and supply crates");
    }
}
