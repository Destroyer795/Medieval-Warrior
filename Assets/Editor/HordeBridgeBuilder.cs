using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class HordeBridgeBuilder
{
    [Serializable] class Source {public float[] vertices,normals,uv,colors;public int[] triangles;}
    const string Folder="Assets/Resources/Horde";
    [MenuItem("Medieval Warrior/Horde/Build stone bridge access")]
    public static void Build()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var game=EditorSceneManager.OpenScene("Assets/Scenes/Semi-Final.unity");
        var world=EditorSceneManager.OpenScene("Assets/Scenes/Background.unity",OpenSceneMode.Additive);
        var old=GameObject.Find("Horde stone bridge approach");if(old!=null)Object.DestroyImmediate(old);
        var data=JsonUtility.FromJson<Source>(File.ReadAllText("Tools/ArtPipeline/Horde/BridgeStairs.json"));
        var mesh=new Mesh{name="Dressed limestone bridge stairs",indexFormat=IndexFormat.UInt32};
        int count=data.vertices.Length/3;
        var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];var colors=new Color[count];
        for(int i=0;i<count;i++)
        {
            vertices[i]=new Vector3(data.vertices[i*3],data.vertices[i*3+1],data.vertices[i*3+2]);
            normals[i]=new Vector3(data.normals[i*3],data.normals[i*3+1],data.normals[i*3+2]);
            uv[i]=new Vector2(data.uv[i*2],data.uv[i*2+1]);colors[i]=new Color(data.colors[i*4],data.colors[i*4+1],data.colors[i*4+2],1);
        }
        mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.colors=colors;mesh.triangles=data.triangles;
        mesh.RecalculateTangents();mesh.RecalculateBounds();mesh=Save(mesh,Folder+"/BridgeStairs.asset");
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/Obelisk.shader");
        var stone=new Material(shader){name="Weathered limestone and infected basalt"};
        stone.SetTexture("_MainTex",Resources.Load<Texture2D>("Buildings/Textures/Masonry/RubbleAlbedo"));
        stone.SetTexture("_BumpMap",Resources.Load<Texture2D>("Buildings/Textures/Masonry/RubbleNormal"));
        stone.SetColor("_Color",new Color(.88f,.89f,.83f));stone.SetFloat("_Curse",0);
        stone=Save(stone,Folder+"/Stone.mat");
        Physics.SyncTransforms();
        var player=GameObject.FindWithTag("Player");NavMesh.SamplePosition(player.transform.position,out var start,4,NavMesh.AllAreas);
        var candidates=new List<(Vector3 lower,Vector3 upper)>();
        var overlaps=new Collider[64];
        foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.name.StartsWith("Bridge")))
        {
            var bounds=filter.GetComponent<Renderer>().bounds;
            if(!Physics.Raycast(new Vector3(bounds.center.x,bounds.max.y+.3f,bounds.center.z),Vector3.down,out var deck,bounds.size.y+2))continue;
            foreach(float side in new[]{-1f,1f})
            {
                Vector3 upper=deck.point+Vector3.forward*side*(bounds.extents.z+.12f);
                Vector3 lower=upper+Vector3.forward*side*18;
                var terrain=Terrain.activeTerrain;if(terrain==null)throw new Exception("The village terrain is required");
                lower.y=terrain.SampleHeight(lower)+terrain.transform.position.y;
                if(!NavMesh.SamplePosition(lower,out var bottom,1.5f,NavMesh.AllAreas)||Mathf.Abs(bottom.position.y-lower.y)>.75f)continue;
                lower=bottom.position;
                float rise=upper.y-lower.y;if(rise<3||rise>10)continue;
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(start.position,lower,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                bool clear=true;
                for(int i=0;i<=48&&clear;i++)
                {
                    Vector3 floor=Vector3.Lerp(lower,upper,i/48f);
                    // A narrower character-height probe: wide treads still need
                    // clearance on both sides, but the probe must not hit its floor.
                    int n=Physics.OverlapBoxNonAlloc(floor+Vector3.up*1.2f,new Vector3(1.85f,.85f,.22f),overlaps,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                    for(int j=0;j<n;j++)
                    {
                        var c=overlaps[j];if(c is TerrainCollider||c.GetComponentInParent<PlayerHealth>()!=null)continue;
                        clear=false;break;
                    }
                }
                if(clear)candidates.Add((lower,upper));
            }
        }
        if(candidates.Count==0)throw new Exception("No clear, reachable bridge stair footprint found; do not install an obstructed approach");
        var chosen=candidates.OrderBy(p=>(p.lower-start.position).sqrMagnitude).First();
        var root=new GameObject("Horde stone bridge approach");SceneManager.MoveGameObjectToScene(root,world);
        root.transform.position=chosen.lower;
        Vector3 forward=chosen.upper-chosen.lower;forward.y=0;root.transform.rotation=Quaternion.LookRotation(forward);
        root.transform.localScale=new Vector3(1,(chosen.upper.y-chosen.lower.y)/7,forward.magnitude/18);
        root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=stone;
        root.AddComponent<MeshCollider>().sharedMesh=Save(RampHull(),Folder+"/BridgeStairsCollision.asset");
        for(int side=-1;side<=1;side+=2)
        {
            var rail=new GameObject("Stone parapet collision");rail.transform.SetParent(root.transform,false);rail.transform.localPosition=new Vector3(side*1.55f,3.97f,9);
            rail.transform.localRotation=Quaternion.Euler(-Mathf.Atan(7f/18)*Mathf.Rad2Deg,0,0);
            rail.AddComponent<BoxCollider>().size=new Vector3(.28f,.92f,19.32f);
        }
        GameObjectUtility.SetStaticEditorFlags(root,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
        EditorSceneManager.SaveScene(world);AssetDatabase.SaveAssets();
        File.WriteAllText("ArtSource/Buildings/horde-access.json",JsonUtility.ToJson(new AccessAudit{lower=chosen.lower,upper=chosen.upper,vertices=mesh.vertexCount,candidates=candidates.Count},true));
        BuildingBuilder.RebakeNavigation();
        HordeArenaReview.Run();
        Debug.Log("[HORDE] Built bevelled stone bridge stairs from "+chosen.lower+" to "+chosen.upper);
    }
    [Serializable] class AccessAudit {public Vector3 lower,upper;public int vertices,candidates;}
    static T Save<T>(T fresh,string path) where T:Object
    {
        var existing=AssetDatabase.LoadAssetAtPath<T>(path);
        if(existing==null){AssetDatabase.CreateAsset(fresh,path);return fresh;}
        EditorUtility.CopySerialized(fresh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(fresh);return existing;
    }
    static Mesh RampHull()
    {
        // Smooth walking ramp plus watertight cheeks; visual treads sit around it.
        Vector3[] v={new Vector3(-1.48f,-.12f,0),new Vector3(1.48f,-.12f,0),new Vector3(-1.48f,.03f,0),new Vector3(1.48f,.03f,0),
            new Vector3(-1.48f,-.12f,18),new Vector3(1.48f,-.12f,18),new Vector3(-1.48f,7.03f,18),new Vector3(1.48f,7.03f,18)};
        var mesh=new Mesh{name="Watertight bridge stair walking hull"};mesh.vertices=v;
        mesh.triangles=new[]{2,6,7,2,7,3,0,1,5,0,5,4,0,4,6,0,6,2,1,3,7,1,7,5,4,5,7,4,7,6,0,2,3,0,3,1};mesh.RecalculateNormals();return mesh;
    }
}
