using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static partial class BuildingBuilder
{
    [Serializable] public class SlopeLane
    {
        public string between,obstacle;public Vector2 start,end;public float width,maxGrade,maxTerrainSlope,rise;public Vector3 steepest,obstruction;
    }
    [Serializable] public class SlopeReport {public SlopeLane[] lanes;public float playerSlopeLimit;}
    static float PassageBlend(float low,float high,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(low,high,value));
    static System.Collections.Generic.List<HouseLane> VillageRoutes(HousePathInspection layout,Terrain terrain)
    {
        Physics.SyncTransforms();var routes=new System.Collections.Generic.List<HouseLane>();
        bool StructureAt(Vector2 p)
        {
            float y=GroundHeight(terrain,p);var low=new Vector3(p.x,y+.4f,p.y);
            return Physics.OverlapCapsule(low,low+Vector3.up*1.1f,.34f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                .Any(c=>c is MeshCollider mesh&&AssetDatabase.GetAssetPath(mesh.sharedMesh).StartsWith(Output+"/"));
        }
        foreach(var lane in HouseLanes(layout,6))
        {
            var direction=(lane.end-lane.start).normalized;
            // Projected house rectangles can place a route endpoint against a
            // third house corner. Keep capsule clearance at both junctions.
            for(int i=0;i<8&&StructureAt(lane.start);i++)lane.start+=direction*.15f;
            for(int i=0;i<8&&StructureAt(lane.end);i++)lane.end-=direction*.15f;
            int samples=Mathf.CeilToInt(Vector2.Distance(lane.start,lane.end)/.2f);bool open=true;
            for(int i=0;i<=samples;i++)if(StructureAt(Vector2.Lerp(lane.start,lane.end,i/(float)samples))){open=false;break;}
            if(open)routes.Add(lane);
        }
        return routes;
    }
    [MenuItem("Medieval Warrior/Buildings/Audit village passage slopes")]
    public static void AuditHouseSlopes()
    {
        CaptureHouseSlopes("before");
    }
    static SlopeReport CaptureHouseSlopes(string name)
    {
        var layout=ReadHouseLayout();var terrain=UnityEngine.Object.FindFirstObjectByType<Terrain>();var td=terrain.terrainData;
        var origin=terrain.transform.position;var size=td.size;
        var report=new SlopeReport {playerSlopeLimit=UnityEngine.Object.FindFirstObjectByType<PlayerControlSpeed>().maxSlope,
            lanes=VillageRoutes(layout,terrain).Select(l=>new SlopeLane {between=l.between,start=l.start,end=l.end,width=l.width}).ToArray()};
        foreach(var lane in report.lanes)
        {
            int n=Mathf.CeilToInt(Vector2.Distance(lane.start,lane.end)/.1f);float last=GroundHeight(terrain,lane.start);
            lane.rise=GroundHeight(terrain,lane.end)-last;
            for(int i=0;i<=n;i++)
            {
                var p=Vector2.Lerp(lane.start,lane.end,i/(float)n);float h=GroundHeight(terrain,p);
                float slope=Vector3.Angle(td.GetInterpolatedNormal((p.x-origin.x)/size.x,(p.y-origin.z)/size.z),Vector3.up);
                if(slope>lane.maxTerrainSlope){lane.maxTerrainSlope=slope;lane.steepest=new Vector3(p.x,h,p.y);}
                if(i>0)lane.maxGrade=Mathf.Max(lane.maxGrade,Mathf.Atan2(Mathf.Abs(h-last),Vector2.Distance(lane.start,lane.end)/n)*Mathf.Rad2Deg);
                last=h;
                if(lane.obstacle==null)
                {
                    var low=new Vector3(p.x,h+.4f,p.y);var high=low+Vector3.up*1.1f;
                    var blocking=Physics.OverlapCapsule(low,high,.3f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                        .FirstOrDefault(c=>!(c is TerrainCollider)&&c.GetComponentInParent<PlayerHealth>()==null&&c.GetComponentInParent<EnemyController>()==null);
                    if(blocking!=null){lane.obstacle=blocking.name;lane.obstruction=new Vector3(p.x,h,p.y);}
                }
            }
        }
        Directory.CreateDirectory("Artifacts/Buildings/Slopes");
        File.WriteAllText("Artifacts/Buildings/Slopes/"+name+".json",JsonUtility.ToJson(report,true));
        Debug.Log("[House slope audit] "+report.lanes.Length+" routes; "+string.Join("; ",report.lanes.OrderByDescending(l=>l.maxTerrainSlope).Take(10).Select(l=>l.between+": grade "+l.maxGrade.ToString("F1")+", ground "+l.maxTerrainSlope.ToString("F1")+" deg at "+l.steepest)));
        return report;
    }
    public static void AuditHouseObstacles()
    {
        var report=CaptureHouseSlopes("obstacles");
        foreach(var lane in report.lanes.Where(l=>l.obstacle!=null))Debug.Log("[Passage obstacle] "+lane.between+": "+lane.obstacle+" at "+lane.obstruction);
    }
    public static void ClearVillageRoutes()
    {
        var layout=ReadHouseLayout();var terrain=UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var routes=VillageRoutes(layout,terrain);int props=ClearLaneProps(terrain,layout,routes);
        PaintHousePaths(terrain,routes);var game=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Scenes[0]);BakeTerrainContact(game,terrain);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Scenes[1]));
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        var after=CaptureHouseSlopes("circulation");File.WriteAllText("ArtSource/Buildings/slope-audit.json",JsonUtility.ToJson(after,true));
        RebakeNavigation();Review();InspectNavigation();VerifyEnvironmentAssets();
        Debug.Log("[Village circulation] PASS: "+routes.Count+" traversable corridors; "+props+" props moved clear of the walking strip");
    }

    [MenuItem("Medieval Warrior/Buildings/Soften village passage slopes")]
    public static void SoftenHouseSlopes()
    {
        var before=CaptureHouseSlopes("before");var terrain=UnityEngine.Object.FindFirstObjectByType<Terrain>();
        var layout=ReadLoadedHouseLayout(UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Scenes[0]));
        var repairs=before.lanes.Where(l=>l.maxTerrainSlope>32).ToArray();var data=terrain.terrainData;var origin=terrain.transform.position;var size=data.size;
        if(repairs.Length==0){Debug.Log("[House slope repair] No passages exceed the 32 degree target");return;}
        int n=data.heightmapResolution;var heights=data.GetHeights(0,0,n,n);
        var ends=repairs.Select(l=>new Vector2(GroundHeight(terrain,l.start),GroundHeight(terrain,l.end))).ToArray();int changed=0;
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            var p=new Vector2(origin.x+x*size.x/(n-1),origin.z+z*size.z/(n-1));float sum=0,weight=0;
            for(int i=0;i<repairs.Length;i++)
            {
                var lane=repairs[i];var ab=lane.end-lane.start;float u=Vector2.Dot(p-lane.start,ab)/ab.sqrMagnitude;
                float along=u*ab.magnitude;
                if(along< -2.5f||along>ab.magnitude+2.5f)continue;
                float side=Mathf.Abs(Vector2.Dot(p-lane.start,new Vector2(-ab.y,ab.x).normalized));if(side>3.5f)continue;
                float w=(1-PassageBlend(1f,3.5f,side))*PassageBlend(-2.5f,0,along)*
                    (1-PassageBlend(ab.magnitude,ab.magnitude+2.5f,along));
                if(w<=0)continue;
                float houseDistance=layout.houses.Min(h=>FootprintDistance(h,p));
                w*=PassageBlend(0,.4f,houseDistance);if(w<=0)continue;
                sum+=w*Mathf.LerpUnclamped(ends[i].x,ends[i].y,u);weight+=w;
            }
            if(weight<=0)continue;
            float old=origin.y+heights[z,x]*size.y;
            float target=Mathf.Lerp(old,sum/weight,Mathf.Clamp01(weight));
            if(Mathf.Abs(old-target)<.001f)continue;
            heights[z,x]=(target-origin.y)/size.y;changed++;
        }
        data.SetHeights(0,0,heights);EditorUtility.SetDirty(data);Physics.SyncTransforms();
        var lanes=repairs.Select(l=>new HouseLane {between=l.between,start=l.start,end=l.end,width=l.width}).ToList();
        ReseatHouseDressing(terrain);ClearLaneProps(terrain,layout,lanes);PaintHousePaths(terrain,lanes);
        var game=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Scenes[0]);BakeTerrainContact(game,terrain);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Scenes[1]));
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        var after=CaptureHouseSlopes("after");
        foreach(var lane in after.lanes.Where(l=>repairs.Any(r=>r.between==l.between)))
            if(lane.maxTerrainSlope>32)throw new Exception("Passage still too steep: "+lane.between+" "+lane.maxTerrainSlope);
        Directory.CreateDirectory("ArtSource/Buildings");File.WriteAllText("ArtSource/Buildings/slope-audit.json",JsonUtility.ToJson(after,true));
        RebakeNavigation();Review();InspectNavigation();VerifyEnvironmentAssets();
        Debug.Log("[House slope repair] PASS: "+repairs.Length+" paths, "+changed+" local terrain cells; house/fortification transforms preserved");
    }
}
