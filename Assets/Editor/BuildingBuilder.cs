using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class BuildingBuilder
{
    public const string Pack="Assets/Advance Studios/Medieval Castle";
    public const string Output="Assets/Resources/Buildings";
    public const string ReferenceFile="Tools/ArtPipeline/Buildings/reference.json";
    static readonly string[] Models={"Bridge","Castle","Doors and Windows","Houses","Tower A","Tower B","Wall"};
    static readonly string[] Scenes={"Assets/Scenes/Semi-Final.unity","Assets/Scenes/Background.unity"};
    [Serializable] public class Size {public Vector3 min,max;public Size(){}public Size(Bounds b){min=b.min;max=b.max;}}
    [Serializable] public class Plane {public float y,area;}
    [Serializable] public class Slice {public float y,x0,x1,z0,z1;}
    [Serializable] public class SourceMesh
    {
        public string key,name,path,guid,model;public long fileId;public Size bounds;public int triangles;public Plane[] floors;public Slice[] slices;
    }
    [Serializable] public class Use {public string path,key;public Matrix4x4 matrix;public bool collider;}
    [Serializable] public class Prefab {public string name,path,guid;public Size bounds;public Use[] parts;public string[] componentIds;}
    [Serializable] public class Placement {public string id,path,asset,key;public Matrix4x4 matrix;public Size bounds;}
    [Serializable] public class SceneLayout {public string path;public Placement[] placements,nodes;}
    [Serializable] public class Reference {public SourceMesh[] meshes;public Prefab[] prefabs;public SceneLayout[] scenes;}
    static string Key(Mesh mesh)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long id);
        return guid+"_"+id.ToString().Replace('-','n');
    }
    static string NodePath(Transform node,Transform root)=>AnimationUtility.CalculateTransformPath(node,root);
    static Bounds BoundsOf(GameObject root)
    {
        bool first=true;var bounds=new Bounds();
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if(filter.sharedMesh==null)continue;
            var b=filter.sharedMesh.bounds;var m=root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            for(int i=0;i<8;i++)
            {
                var p=m.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
        }
        return bounds;
    }
    [MenuItem("Medieval Warrior/Buildings/Export reference dimensions")]
    public static void ExportReference()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        Directory.CreateDirectory("Tools/ArtPipeline/Buildings");Directory.CreateDirectory("Artifacts/Buildings/Original");
        var sources=new List<SourceMesh>();
        foreach(string model in Models)
        {
            string path=Pack+"/Art/Model/"+model+".fbx";
            foreach(var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long id);
                var v=mesh.vertices;var tri=mesh.triangles;float tolerance=Mathf.Max(mesh.bounds.size.y*.0001f,.00001f);
                var planes=new Dictionary<int,Plane>();
                for(int i=0;i<tri.Length;i+=3)
                {
                    Vector3 a=v[tri[i]],b=v[tri[i+1]],c=v[tri[i+2]],cross=Vector3.Cross(b-a,c-a);
                    if(cross.magnitude<1e-10f||Mathf.Abs(cross.normalized.y)<.995f)continue;
                    float y=(a.y+b.y+c.y)/3;int bin=Mathf.RoundToInt(y/tolerance);
                    if(!planes.TryGetValue(bin,out var plane))planes[bin]=plane=new Plane{y=y};plane.area+=cross.magnitude*.5f;
                }
                var slices=new List<Slice>();
                for(int row=0;row<=20;row++)
                {
                    float y=Mathf.Lerp(mesh.bounds.min.y,mesh.bounds.max.y,row/20f);var points=v.Where(p=>Mathf.Abs(p.y-y)<mesh.bounds.size.y*.035f+tolerance).ToArray();
                    if(points.Length>0)slices.Add(new Slice{y=y,x0=points.Min(p=>p.x),x1=points.Max(p=>p.x),z0=points.Min(p=>p.z),z1=points.Max(p=>p.z)});
                }
                sources.Add(new SourceMesh{key=Key(mesh),name=mesh.name,path=path,guid=guid,fileId=id,model=model,bounds=new Size(mesh.bounds),triangles=tri.Length/3,
                    floors=planes.Values.OrderByDescending(p=>p.area).Take(12).ToArray(),slices=slices.ToArray()});
            }
        }
        var keys=new HashSet<string>(sources.Select(m=>m.key));var prefabs=new List<Prefab>();
        foreach(string path in AssetDatabase.FindAssets("t:Prefab",new[]{Pack+"/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p))
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var uses=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh!=null&&keys.Contains(Key(f.sharedMesh))).Select(f=>new Use{
                path=NodePath(f.transform,root.transform),key=Key(f.sharedMesh),matrix=root.transform.worldToLocalMatrix*f.transform.localToWorldMatrix,
                collider=f.GetComponent<MeshCollider>()!=null}).ToArray();
            if(root.GetComponentsInChildren<MeshFilter>(true).Any(f=>AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith(Output)))throw new Exception("Reference is immutable after integration. Restore original prefabs before exporting a new baseline.");
            prefabs.Add(new Prefab{name=root.name,path=path,guid=AssetDatabase.AssetPathToGUID(path),bounds=new Size(BoundsOf(root)),parts=uses,componentIds=ComponentIds(root)});
        }
        var layouts=new List<SceneLayout>();
        foreach(string scenePath in Scenes)
        {
            var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);var placements=new List<Placement>();
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject)!=t.gameObject)continue;
                string source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                if(!source.StartsWith(Pack+"/"))continue;
                placements.Add(new Placement{id=GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString(),path=NodePath(t,root.transform),asset=source,matrix=t.localToWorldMatrix,bounds=new Size(BoundsOf(t.gameObject))});
            }
            var nodes=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)).Where(f=>f.sharedMesh!=null&&keys.Contains(Key(f.sharedMesh))).Select(f=>new Placement{
                id=GlobalObjectId.GetGlobalObjectIdSlow(f).ToString(),path=f.name,key=Key(f.sharedMesh),asset=AssetDatabase.GetAssetPath(f.sharedMesh),matrix=f.transform.localToWorldMatrix,bounds=new Size(f.sharedMesh.bounds)}).ToArray();
            layouts.Add(new SceneLayout{path=scenePath,placements=placements.ToArray(),nodes=nodes});
        }
        File.WriteAllText(ReferenceFile,JsonUtility.ToJson(new Reference{meshes=sources.ToArray(),prefabs=prefabs.ToArray(),scenes=layouts.ToArray()},true));
        Debug.Log("[Buildings] Reference: "+sources.Count+" meshes, "+prefabs.Count+" prefabs, "+layouts.Sum(s=>s.placements.Length)+" prefab roots, "+layouts.Sum(s=>s.nodes.Length)+" architectural mesh nodes");
        CaptureOriginal(prefabs);
    }
    static string[] ComponentIds(GameObject root)=>root.GetComponentsInChildren<Transform>(true).SelectMany(t=>t.GetComponents<Component>().Cast<Object>().Append(t.gameObject)).Where(o=>o!=null).Select(o=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string g,out long id);return g+":"+id;}).ToArray();
    static void CaptureOriginal(List<Prefab> prefabs)
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientLight=new Color(.45f,.48f,.52f);RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.eulerAngles=new Vector3(35,-35,0);
        var camera=new GameObject("Architecture review").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.12f,.15f);
        foreach(var prefab in prefabs)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab.path));go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            Bounds bounds=BoundsOf(go);float span=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
            camera.transform.position=bounds.center+new Vector3(1,.65f,1.4f).normalized*span*2.5f;camera.transform.LookAt(bounds.center);
            camera.orthographic=true;camera.orthographicSize=span*.72f;camera.nearClipPlane=Mathf.Max(.0001f,span*.001f);camera.farClipPlane=span*10;
            var target=new RenderTexture(1000,800,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(1000,800,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1000,800),0,0);texture.Apply();
            File.WriteAllBytes("Artifacts/Buildings/Original/"+prefab.name+".png",texture.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(target);Object.DestroyImmediate(go);
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }
}
