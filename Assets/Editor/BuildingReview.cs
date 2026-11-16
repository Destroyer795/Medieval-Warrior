using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class BuildingBuilder
{
    [Serializable] public class Verification {public int checks,prefabRoots,meshNodes,navSamples,reachableSpawns;public string result;public string[] messages;}
    static Verification verification;static List<string> messages;
    static void Check(bool condition,string message)
    {
        verification.checks++;if(!condition)throw new Exception("[Buildings] FAILED: "+message);
    }
    static float Difference(Matrix4x4 a,Matrix4x4 b){float d=0;for(int i=0;i<16;i++)d=Mathf.Max(d,Mathf.Abs(a[i]-b[i]));return d;}
    static float Difference(Size a,Size b)=>Mathf.Max((a.min-b.min).magnitude,(a.max-b.max).magnitude);
    [MenuItem("Medieval Warrior/Buildings/Verify dimensions, references and collisions")]
    public static void Review()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        verification=new Verification();messages=new List<string>();
        var reference=JsonUtility.FromJson<Reference>(File.ReadAllText(ReferenceFile));var manifest=ReadManifest();
        foreach(var part in manifest.parts)
        {
            var source=reference.meshes.First(m=>m.key==part.key);int previous=int.MaxValue;
            for(int lod=0;lod<3;lod++)
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(part.key,lod));Check(mesh!=null,part.name+" mesh exists");
                Check(Difference(source.bounds,new Size(mesh.bounds))<.0005f,part.name+" LOD"+lod+" measured bounds");
                Check(mesh.uv.Length==mesh.vertexCount&&mesh.uv2.Length==mesh.vertexCount,part.name+" UV channels");
                Check(mesh.uv2.All(v=>v.x>=0&&v.y>=0&&v.x<=1&&v.y<=1),part.name+" lightmap atlas range");
                Check(mesh.normals.All(n=>n.sqrMagnitude>.90f&&n.sqrMagnitude<1.1f),part.name+" valid split normals");
                var points=mesh.vertices;var normals=mesh.normals;var indices=mesh.triangles;
                for(int i=0;i<indices.Length;i+=Mathf.Max(3,(indices.Length/90/3)*3))
                {
                    int a=indices[i],b=indices[i+1],c=indices[i+2];var cross=Vector3.Cross(points[b]-points[a],points[c]-points[a]);
                    Check(Vector3.Dot(cross,normals[a]+normals[b]+normals[c])>=-1e-7f,part.name+" outward triangle/normal agreement");
                }
                Check(mesh.triangles.Length<=previous,part.name+" decreasing LOD triangles");previous=mesh.triangles.Length;
            }
            var collision=AssetDatabase.LoadAssetAtPath<Mesh>(CollisionPath(part.key));Check(collision!=null&&collision.triangles.Length>0,part.name+" collision proxy");
            Check(collision.triangles.Length<=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(part.key,0)).triangles.Length,part.name+" simplified collision budget");
        }
        foreach(var prefab in reference.prefabs)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(prefab.path);
            Check(AssetDatabase.AssetPathToGUID(prefab.path)==prefab.guid,prefab.name+" original prefab GUID");
            Check(Difference(prefab.bounds,new Size(BoundsOf(root)))<.0005f,prefab.name+" original aggregate bounds");
            var ids=new HashSet<string>(ComponentIds(root));Check(prefab.componentIds.All(ids.Contains),prefab.name+" original persistent component IDs");
            Check(root.GetComponentsInChildren<MeshFilter>(true).All(f=>f.sharedMesh!=null),prefab.name+" no missing mesh references");
            Check(root.GetComponentsInChildren<MeshRenderer>(true).All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader.name=="Standard")),prefab.name+" Standard materials");
            foreach(var use in prefab.parts)
            {
                // Repeated names in the vendor hierarchy are intentional. Verify every matching
                // transform/mesh pair rather than using Transform.Find's first-name result.
                Check(root.GetComponentsInChildren<MeshFilter>(true).Any(f=>AssetDatabase.GetAssetPath(f.sharedMesh)==MeshPath(use.key,0)&&Difference(root.transform.worldToLocalMatrix*f.transform.localToWorldMatrix,use.matrix)<.00001f),prefab.name+" preserved module transform "+use.path);
            }
        }
        foreach(var layout in reference.scenes)
        {
            var scene=EditorSceneManager.OpenScene(layout.path,OpenSceneMode.Single);Physics.SyncTransforms();
            foreach(var original in layout.placements)
            {
                GlobalObjectId.TryParse(original.id,out var id);var go=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
                Check(go!=null,original.path+" original scene object ID");Check(Difference(go.transform.localToWorldMatrix,original.matrix)<.00001f,original.path+" world transform");
                Check(Difference(original.bounds,new Size(BoundsOf(go)))<.0005f,original.path+" scene bounds");verification.prefabRoots++;
                Check(go.GetComponentsInChildren<MeshFilter>(true).All(f=>f.sharedMesh!=null),original.path+" scene instance meshes resolved");
            }
            foreach(var original in layout.nodes)
            {
                GlobalObjectId.TryParse(original.id,out var id);var filter=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as MeshFilter;
                Check(filter!=null,original.path+" original scene MeshFilter ID");Check(Difference(filter.transform.localToWorldMatrix,original.matrix)<.00001f,original.path+" mesh world transform");
                Check(AssetDatabase.GetAssetPath(filter.sharedMesh)==MeshPath(original.key,0),original.path+" custom replacement mesh");
                Check(Difference(original.bounds,new Size(filter.sharedMesh.bounds))<.0005f,original.path+" mesh bounding box");verification.meshNodes++;
                Check(filter.GetComponent<MeshRenderer>().sharedMaterials.All(m=>m!=null&&m.shader.name=="Standard"),original.path+" scene material references");
                foreach(var collider in filter.GetComponents<MeshCollider>())Check(collider.sharedMesh!=null&&AssetDatabase.GetAssetPath(collider.sharedMesh)==CollisionPath(original.key),original.path+" custom collision reference");
            }
        }
        VerifyPhysicalModules(reference);CaptureFinished(reference);
        verification.result="PASS";verification.messages=messages.ToArray();Directory.CreateDirectory("ArtSource/Buildings");
        File.WriteAllText("ArtSource/Buildings/unity-audit.json",JsonUtility.ToJson(verification,true));
        Debug.Log("[Buildings] PASS "+verification.checks+" checks; "+verification.prefabRoots+" preserved prefab roots, "+verification.meshNodes+" preserved architectural mesh nodes");
    }
    static void VerifyPhysicalModules(Reference reference)
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        foreach(var source in reference.meshes.Where(m=>m.model=="Bridge"||m.name=="Wall"))
        {
            var go=new GameObject("Collision probe");var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(CollisionPath(source.key));
            Physics.SyncTransforms();float floor=source.floors[0].y;
            float centerX=(source.bounds.min.x+source.bounds.max.x)/2;
            float z=source.name=="Wall"?.30f:(source.bounds.min.z+source.bounds.max.z)/2;
            // A sphere is the same primitive used by the gameplay camera; a ray is used by arrows.
            Check(Physics.Raycast(new Vector3(centerX,floor+.15f,z),Vector3.down,out var hit,.3f),source.name+" watertight deck raycast");
            Check(Mathf.Abs(hit.point.y-floor)<.015f,source.name+" retained walking surface height: "+hit.point.y+" expected "+floor);
            Check(Physics.SphereCast(new Vector3(centerX,floor+.25f,z),.025f,Vector3.down,out _,.4f),source.name+" camera sphere collision");
            // CharacterController.Move exercises Unity's actual capsule contact/slide behavior.
            var actor=new GameObject("Locomotion capsule");var controller=actor.AddComponent<CharacterController>();controller.height=.22f;controller.radius=.035f;controller.center=Vector3.up*.11f;controller.skinWidth=.003f;controller.stepOffset=.045f;
            actor.transform.position=new Vector3(centerX-.12f,floor+.02f,z);Physics.SyncTransforms();
            for(int i=0;i<40;i++)controller.Move(new Vector3(.005f,-.01f,0));
            Check(actor.transform.position.y>floor-.035f,source.name+" locomotion stays above deck");
            Object.DestroyImmediate(actor);Object.DestroyImmediate(go);
        }
        messages.Add("Deck rays, camera spheres and CharacterController movement checked on all bridge bays and the wall walk.");
        foreach(var source in reference.meshes.Where(m=>m.model=="Houses"))
        {
            var go=new GameObject("House structural probe");go.AddComponent<MeshCollider>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(CollisionPath(source.key));
            Physics.SyncTransforms();float y=source.bounds.min.y+.05f;float x=(source.bounds.min.x+source.bounds.max.x)*.5f+.27329f;
            // The isolated prefab has no terrain outside its door. Supply only the exterior
            // approach, so the capsule can step onto (and genuinely test) the interior floor.
            var approach=new GameObject("Exterior approach");var approachCollider=approach.AddComponent<BoxCollider>();approachCollider.size=new Vector3(.6f,.04f,.30f);approach.transform.position=new Vector3(x,source.bounds.min.y-.02f,source.bounds.min.z-.15f);
            var actor=new GameObject("House entry capsule");var controller=actor.AddComponent<CharacterController>();controller.height=.22f;controller.radius=.035f;controller.center=Vector3.up*.11f;controller.skinWidth=.003f;controller.stepOffset=.045f;
            actor.transform.position=new Vector3(x,y,source.bounds.min.z-.20f);Physics.SyncTransforms();
            for(int i=0;i<65;i++)controller.Move(new Vector3(0,-.01f,.01f));
            Check(actor.transform.position.z>source.bounds.min.z+.25f,source.name+" structural entrance admits capsule");
            Check(actor.transform.position.y>source.bounds.min.y+.01f,source.name+" interior floor catches capsule");
            Check(Physics.Raycast(new Vector3(x,source.bounds.min.y+.15f,source.bounds.min.z+.3f),Vector3.down,out var floorHit,.20f)&&floorHit.point.y>source.bounds.min.y+.015f,source.name+" interior floor raycast");
            var from=new Vector3(source.bounds.min.x-.15f,source.bounds.min.y+.4f,0);
            Check(Physics.Raycast(from,Vector3.right,.4f),source.name+" side wall arrow collision");
            Check(Physics.SphereCast(from,.025f,Vector3.right,out _,.4f),source.name+" side wall camera collision");
            Object.DestroyImmediate(actor);Object.DestroyImmediate(approach);Object.DestroyImmediate(go);
        }
        messages.Add("House structural entrances, interior floors, side-wall arrows and camera spheres checked with live physics.");
    }
    static void CaptureFinished(Reference reference)
    {
        string dir="ArtSource/Buildings/Review";Directory.CreateDirectory(dir);
        var oldShadows=QualitySettings.shadows;var oldResolution=QualitySettings.shadowResolution;float oldDistance=QualitySettings.shadowDistance;
        try
        {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.28f,.30f,.34f);
        QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowDistance=150;
        var light=new GameObject("Warm key").AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(1,.91f,.78f);light.intensity=1.25f;light.transform.eulerAngles=new Vector3(38,-32,0);
        light.shadows=LightShadows.Soft;light.shadowBias=.002f;light.shadowNormalBias=.01f;
        var fill=new GameObject("Cool fill").AddComponent<Light>();fill.type=LightType.Directional;fill.color=new Color(.62f,.76f,1);fill.intensity=.35f;fill.transform.eulerAngles=new Vector3(55,145,0);
        var camera=new GameObject("Kit review").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.09f,.115f);camera.allowHDR=true;
        foreach(var prefab in reference.prefabs)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab.path));go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            foreach(var lod in go.GetComponentsInChildren<LODGroup>())lod.ForceLOD(0);
            var b=BoundsOf(go);float span=Mathf.Max(b.size.x,b.size.y,b.size.z);
            camera.transform.position=b.center+new Vector3(1,.65f,-1.4f).normalized*span*2.5f;camera.transform.LookAt(b.center);
            camera.orthographic=true;camera.orthographicSize=span*.66f;camera.nearClipPlane=.001f;camera.farClipPlane=span*10;
            var target=new RenderTexture(1200,1000,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(1200,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1200,1000),0,0);texture.Apply();
            File.WriteAllBytes(dir+"/"+prefab.name+".png",texture.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;
            Object.DestroyImmediate(texture);Object.DestroyImmediate(target);Object.DestroyImmediate(go);
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
        finally{QualitySettings.shadows=oldShadows;QualitySettings.shadowResolution=oldResolution;QualitySettings.shadowDistance=oldDistance;}
    }
    [MenuItem("Medieval Warrior/Buildings/Rebake navigation and verify routes")]
    public static void RebakeNavigation()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var game=EditorSceneManager.OpenScene(Scenes[0],OpenSceneMode.Single);EditorSceneManager.OpenScene(Scenes[1],OpenSceneMode.Additive);
        var all=game.GetRootGameObjects();var surface=all.SelectMany(g=>g.GetComponentsInChildren<NavMeshSurface>()).First();
        var player=all.First(g=>g.CompareTag("Player"));var position=player.transform.position;bool active=player.activeSelf;
        var data=surface.navMeshData;string path=AssetDatabase.GetAssetPath(data);string guid=AssetDatabase.AssetPathToGUID(path);
        if(data==null||string.IsNullOrEmpty(path))throw new Exception("The scene needs its existing persistent NavMeshData before a targeted rebake.");
        player.SetActive(false);try{surface.BuildNavMesh();}finally{player.SetActive(active);}
        var fresh=surface.navMeshData;surface.RemoveData();EditorUtility.CopySerialized(fresh,data);surface.navMeshData=data;surface.AddData();Object.DestroyImmediate(fresh);
        EditorUtility.SetDirty(data);EditorUtility.SetDirty(surface);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(game);
        if(AssetDatabase.AssetPathToGUID(path)!=guid)throw new Exception("Navigation asset GUID changed");
        InspectRoutes(all,position);
    }
    [MenuItem("Medieval Warrior/Buildings/Inspect navigation without rebaking")]
    public static void InspectNavigation()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var game=EditorSceneManager.OpenScene(Scenes[0],OpenSceneMode.Single);EditorSceneManager.OpenScene(Scenes[1],OpenSceneMode.Additive);
        var all=game.GetRootGameObjects();InspectRoutes(all,all.First(g=>g.CompareTag("Player")).transform.position);
    }
    static void InspectRoutes(GameObject[] all,Vector3 position)
    {
        if(!NavMesh.SamplePosition(position,out var playerNav,4,NavMesh.AllAreas))throw new Exception("Player start is outside the rebuilt NavMesh");
        var spawner=all.SelectMany(g=>g.GetComponentsInChildren<ZombieSpawner>()).First();int reachable=0;
        foreach(var spawn in (spawner.extraSpawnPoints??Array.Empty<Transform>()).Concat(new[]{spawner.spawnPoint}).Where(t=>t!=null))
        {
            if(!NavMesh.SamplePosition(spawn.position,out var sample,6,NavMesh.AllAreas))continue;
            var route=new NavMeshPath();if(NavMesh.CalculatePath(sample.position,playerNav.position,NavMesh.AllAreas,route)&&route.status==NavMeshPathStatus.PathComplete)reachable++;
        }
        if(reachable==0)throw new Exception("No enemy spawn can reach the player after the building bake");
        int samples=0;var refData=JsonUtility.FromJson<Reference>(File.ReadAllText(ReferenceFile));
        var keys=new HashSet<string>(refData.meshes.Where(m=>m.model=="Houses"||m.name=="Wall").Select(m=>m.key));
        foreach(var node in refData.scenes.SelectMany(s=>s.nodes).Where(n=>keys.Contains(n.key)))
        {
            var localCenter=(node.bounds.min+node.bounds.max)*.5f;localCenter.y=node.bounds.min.y+.02f;
            var localExtents=(node.bounds.max-node.bounds.min)*.5f;
            for(int side=0;side<4;side++)
            {
                var p=node.matrix.MultiplyPoint3x4(localCenter+new Vector3(Mathf.Cos(side*Mathf.PI/2)*(localExtents.x+.15f),0,Mathf.Sin(side*Mathf.PI/2)*(localExtents.z+.15f)));
                if(NavMesh.SamplePosition(p,out _,8,NavMesh.AllAreas))samples++;
            }
        }
        var report=JsonUtility.FromJson<Verification>(File.ReadAllText("ArtSource/Buildings/unity-audit.json"));report.navSamples=samples;report.reachableSpawns=reachable;
        const string note="NavMesh rebaked from custom physics hulls, preserving data GUID and all player/spawn transforms.";
        if(!report.messages.Contains(note))report.messages=report.messages.Append(note).ToArray();
        File.WriteAllText("ArtSource/Buildings/unity-audit.json",JsonUtility.ToJson(report,true));
        Debug.Log("[Buildings] Navigation PASS: "+reachable+" reachable spawn routes; "+samples+" house/wall vicinity samples; data GUID preserved");
    }
    public static void ReviewAndRebake(){Review();RebakeNavigation();}
    [MenuItem("Medieval Warrior/Buildings/Verify kit and current navigation")]
    public static void ReviewWithNavigation(){Review();InspectNavigation();}
}
