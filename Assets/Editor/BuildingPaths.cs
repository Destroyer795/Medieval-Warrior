using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class BuildingBuilder
{
    const string PathLayoutFile = "Tools/ArtPipeline/Buildings/path-layout.json";
    const float HousePassageWidth = 2f;

    [Serializable] public class HouseFootprint
    {
        public string id, name;
        public Vector2 center, x, z, half;
        public float bottom, top;
        [NonSerialized] public GameObject go;
        [NonSerialized] public Vector2 initial;
        public Vector2[] Corners() => new[] { center-x*half.x-z*half.y, center+x*half.x-z*half.y,
            center+x*half.x+z*half.y, center-x*half.x+z*half.y };
    }
    [Serializable] public class HousePathInspection
    {
        public float playerRadius, playerHeight, minimumPassageWidth;
        public HouseFootprint[] houses, obstacles;
    }
    [Serializable] public class HouseLayoutMove {public string id,name;public Vector3 from,to;}
    [Serializable] public class HouseLayoutRevision {public string reason;public float minimumGap;public HouseLayoutMove[] moves;}
    [Serializable] public class HouseLane
    {
        public string between;public Vector2 start,end;public float width;
        public int capsuleSamples;public float forwardError,reverseError,navLength;
    }
    [Serializable] public class HousePathReport
    {
        public string result;public int houses,movedHouses,originalBlockedPairs,terrainCells,movedProps;
        public float playerRadius,playerHeight,minimumGap,maximumHouseMove;
        public HouseLane[] lanes;
    }

    // An explicit, translation-only level-design revision layered over the immutable
    // source-art baseline. All other world matrices are still checked against that baseline.
    static Matrix4x4 ReviewedLayoutMatrix(Placement original,Transform node)
    {
        if(!File.Exists(PathLayoutFile))return original.matrix;
        var revision=JsonUtility.FromJson<HouseLayoutRevision>(File.ReadAllText(PathLayoutFile));
        for(var t=node;t!=null;t=t.parent)
        {
            string id=GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString();
            var move=revision.moves.FirstOrDefault(m=>m.id==id);
            if(move!=null)return Matrix4x4.Translate(move.to-move.from)*original.matrix;
        }
        return original.matrix;
    }
    static void ValidateHouseLayoutRevision(Reference reference)
    {
        if(!File.Exists(PathLayoutFile))return;
        var revision=JsonUtility.FromJson<HouseLayoutRevision>(File.ReadAllText(PathLayoutFile));
        var ids=new HashSet<string>();
        foreach(var move in revision.moves)
        {
            var original=reference.scenes.SelectMany(s=>s.placements).FirstOrDefault(p=>p.id==move.id);
            if(original==null||!(original.asset.EndsWith("/House A.prefab")||original.asset.EndsWith("/House B.prefab"))||!ids.Add(move.id))
                throw new Exception("Layout revision must contain unique existing house IDs only");
            Vector3 start=original.matrix.GetColumn(3);
            if((start-move.from).magnitude>.001f||Mathf.Abs(move.to.y-move.from.y)>.001f||(move.to-move.from).magnitude>8)
                throw new Exception("Layout revision must preserve the source pivot elevation and remain within eight metres: "+move.name);
        }
    }

    static float FootprintDistance(HouseFootprint box,Vector2 p)
    {
        var q=p-box.center;var d=new Vector2(Mathf.Abs(Vector2.Dot(q,box.x))-box.half.x,Mathf.Abs(Vector2.Dot(q,box.z))-box.half.y);
        return new Vector2(Mathf.Max(d.x,0),Mathf.Max(d.y,0)).magnitude+Mathf.Min(Mathf.Max(d.x,d.y),0);
    }
    static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
    {
        var ab=b-a;return Vector2.Distance(p,a+ab*Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude));
    }
    static float GroundHeight(Terrain terrain,Vector2 p)
    {
        var origin=terrain.transform.position;var size=terrain.terrainData.size;
        return origin.y+terrain.terrainData.GetInterpolatedHeight((p.x-origin.x)/size.x,(p.y-origin.z)/size.z);
    }

    static HouseFootprint Footprint(GameObject go, string id)
    {
        // Include eaves and door frames, not just a centre-to-centre radius.
        var bounds=BoundsOf(go);var t=go.transform;var c=t.TransformPoint(bounds.center);
        var ax=t.TransformVector(Vector3.right);var az=t.TransformVector(Vector3.forward);
        return new HouseFootprint {go=go,id=id,name=go.name,center=new Vector2(c.x,c.z),initial=new Vector2(c.x,c.z),
            x=new Vector2(ax.x,ax.z).normalized,z=new Vector2(az.x,az.z).normalized,
            half=new Vector2(bounds.extents.x*ax.magnitude,bounds.extents.z*az.magnitude),
            bottom=c.y-bounds.extents.y*t.TransformVector(Vector3.up).magnitude,
            top=c.y+bounds.extents.y*t.TransformVector(Vector3.up).magnitude};
    }
    static HousePathInspection ReadHouseLayout()
    {
        var game=EditorSceneManager.OpenScene(Scenes[0],OpenSceneMode.Single);
        EditorSceneManager.OpenScene(Scenes[1],OpenSceneMode.Additive);
        return ReadLoadedHouseLayout(game);
    }
    static HousePathInspection ReadLoadedHouseLayout(Scene game)
    {
        var reference=JsonUtility.FromJson<Reference>(File.ReadAllText(ReferenceFile));
        var houses=new List<HouseFootprint>();var obstacles=new List<HouseFootprint>();
        foreach(var placement in reference.scenes.SelectMany(s=>s.placements))
        {
            GlobalObjectId.TryParse(placement.id,out var id);
            var go=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as GameObject;
            if(go==null)throw new Exception("Missing architecture placement "+placement.id);
            var item=Footprint(go,placement.id);
            if(placement.asset.EndsWith("/House A.prefab")||placement.asset.EndsWith("/House B.prefab"))houses.Add(item);
            else if(!placement.asset.Contains("Door Frame"))obstacles.Add(item);
        }
        var player=game.GetRootGameObjects().First(g=>g.CompareTag("Player"));
        var capsule=player.GetComponent<CapsuleCollider>();var scale=player.transform.lossyScale;
        return new HousePathInspection {houses=houses.ToArray(),obstacles=obstacles.ToArray(),minimumPassageWidth=HousePassageWidth,
            playerRadius=capsule.radius*Mathf.Max(scale.x,scale.z),playerHeight=capsule.height*scale.y};
    }
    // Positive = separation on an OBB axis; negative = overlap. A 2m separating axis
    // guarantees at least 2m of clear space, including diagonally rotated houses.
    static float PassageGap(HouseFootprint a,HouseFootprint b,out Vector2 direction)
    {
        float gap=float.NegativeInfinity;direction=Vector2.zero;
        foreach(var axis in new[]{a.x,a.z,b.x,b.z})
        {
            float distance=Vector2.Dot(b.center-a.center,axis);
            float extentA=Mathf.Abs(Vector2.Dot(a.x,axis))*a.half.x+Mathf.Abs(Vector2.Dot(a.z,axis))*a.half.y;
            float extentB=Mathf.Abs(Vector2.Dot(b.x,axis))*b.half.x+Mathf.Abs(Vector2.Dot(b.z,axis))*b.half.y;
            float value=Mathf.Abs(distance)-extentA-extentB;
            if(value>gap){gap=value;direction=axis*(distance>=0?1:-1);}
        }
        return gap;
    }
    [MenuItem("Medieval Warrior/Buildings/Inspect house pathways")]
    public static void InspectHousePaths()
    {
        var layout=ReadHouseLayout();Directory.CreateDirectory("Artifacts/Buildings/Paths");
        File.WriteAllText("Artifacts/Buildings/Paths/before.json",JsonUtility.ToJson(layout,true));
        var tight=new List<string>();
        for(int a=0;a<layout.houses.Length;a++)for(int b=a+1;b<layout.houses.Length;b++)
        {
            float gap=PassageGap(layout.houses[a],layout.houses[b],out _);
            if(gap<HousePassageWidth)tight.Add(layout.houses[a].name+" / "+layout.houses[b].name+": "+gap.ToString("F2")+"m");
        }
        Debug.Log("[House paths] "+layout.houses.Length+" houses; player capsule "+layout.playerRadius.ToString("F2")+"m radius, "+layout.playerHeight.ToString("F2")+"m high; "+tight.Count+" narrow/overlapping pairs:\n"+string.Join("\n",tight));
        CaptureVillagePaths("Before");
    }

    static void SpaceHouses(HousePathInspection layout)
    {
        var limits=new List<(HouseFootprint house,HouseFootprint obstacle,float minimum)>();
        foreach(var house in layout.houses)foreach(var obstacle in layout.obstacles)
        {
            // The keep's aggregate bounds also contain its raised towers/terrace. Its
            // empty courtyard is not an obstacle. Actual collision is tested below.
            if(obstacle.name=="Castle"||obstacle.bottom>house.top||obstacle.top<house.bottom)continue;
            float gap=PassageGap(house,obstacle,out _);
            if(gap<9)limits.Add((house,obstacle,Mathf.Min(gap,HousePassageWidth+.04f)));
        }
        // Deterministic projection with a decaying attraction to original positions.
        // The final iterations enforce clearance without pulling houses back into lanes.
        for(int iteration=0;iteration<1400;iteration++)
        {
            float attraction=.025f*Mathf.Max(0,1-iteration/1000f);
            foreach(var house in layout.houses)house.center+=(house.initial-house.center)*attraction;
            foreach(var limit in limits)
            {
                float gap=PassageGap(limit.house,limit.obstacle,out var direction);
                if(gap<limit.minimum)limit.house.center-=direction*(limit.minimum-gap);
            }
            for(int a=0;a<layout.houses.Length;a++)for(int b=a+1;b<layout.houses.Length;b++)
            {
                var first=layout.houses[a];var second=layout.houses[b];float gap=PassageGap(first,second,out var direction);
                if(gap<HousePassageWidth+.04f)
                {
                    var correction=direction*((HousePassageWidth+.04f-gap)*.5f);
                    first.center-=correction;second.center+=correction;
                }
            }
        }
        foreach(var limit in limits)if(PassageGap(limit.house,limit.obstacle,out _)<limit.minimum-.002f)
            throw new Exception("House spacing would narrow the existing fortification approach: "+limit.house.name+" / "+limit.obstacle.name);
        foreach(var house in layout.houses)if(Vector2.Distance(house.center,house.initial)>8)
            throw new Exception("House spacing needs a manual layout review: "+house.name);
    }

    static List<HouseLane> HouseLanes(HousePathInspection layout,float maximumGap=HousePassageWidth+.45f)
    {
        var lanes=new List<HouseLane>();
        for(int i=0;i<layout.houses.Length;i++)for(int j=i+1;j<layout.houses.Length;j++)
        {
            var a=layout.houses[i];var b=layout.houses[j];float gap=PassageGap(a,b,out var d);
            if(gap>maximumGap)continue;
            var side=new Vector2(-d.y,d.x);var ac=a.Corners();var bc=b.Corners();
            float lo=Mathf.Max(ac.Min(p=>Vector2.Dot(p,side)),bc.Min(p=>Vector2.Dot(p,side)));
            float hi=Mathf.Min(ac.Max(p=>Vector2.Dot(p,side)),bc.Max(p=>Vector2.Dot(p,side)));
            if(hi-lo<.25f)continue; // separated corners have no shared alley
            float middle=(ac.Max(p=>Vector2.Dot(p,d))+bc.Min(p=>Vector2.Dot(p,d)))*.5f;
            lanes.Add(new HouseLane {between=a.name+" / "+b.name,start=d*middle+side*(lo-1.2f),end=d*middle+side*(hi+1.2f),width=gap});
        }
        return lanes;
    }

    static int SeatMovedHousePads(Terrain terrain,HousePathInspection layout)
    {
        var data=terrain.terrainData;int n=data.heightmapResolution;var origin=terrain.transform.position;var size=data.size;
        var heights=data.GetHeights(0,0,n,n);int edited=0;
        var changed=layout.houses.Where(h=>Vector2.Distance(h.initial,h.center)>.001f).ToArray();
        if(changed.Length==0)return 0;
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            var p=new Vector2(origin.x+x*size.x/(n-1),origin.z+z*size.z/(n-1));float weight=0,value=0;
            foreach(var house in changed)
            {
                if((p-house.center).sqrMagnitude>Mathf.Pow(house.half.magnitude+3.5f,2))continue;
                float distance=FootprintDistance(house,p);if(distance>3.5f)continue;
                // Keep the original house elevation; feather the local pad into the
                // existing terrain. Neighbours contribute to a smooth shared slope.
                float w=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,3.5f,distance));
                weight+=w;value+=w*(house.bottom+.12f);
            }
            if(weight<=0)continue;
            float y=origin.y+heights[z,x]*size.y;
            float target=Mathf.Lerp(y,value/weight,Mathf.Clamp01(weight));
            if(Mathf.Abs(target-y)<.001f)continue;
            heights[z,x]=(target-origin.y)/size.y;edited++;
        }
        data.SetHeights(0,0,heights);EditorUtility.SetDirty(data);return edited;
    }

    static void MoveHouseDressing(HousePathInspection layout)
    {
        var world=SceneManager.GetSceneByPath(Scenes[1]);var environment=world.GetRootGameObjects().First(g=>g.name=="Environment").transform;
        var foundations=environment.Find("Foundations");var smoke=environment.Find("Banners & Smoke");
        var baseProps=environment.Find("Base Props");var crates=environment.Find("Supply Crates");
        // Associate before moving anything, so neighbouring houses cannot claim an
        // already translated prop. Child pickup and particle transforms remain intact.
        var shifts=new Dictionary<Transform,Vector3>();
        foreach(var house in layout.houses)
        {
            var delta=house.center-house.initial;if(delta.sqrMagnitude<.000001f)continue;
            var foundation=foundations==null?null:foundations.Find(house.name+" Foundation");
            if(foundation!=null)shifts[foundation]=new Vector3(delta.x,0,delta.y);
        }
        foreach(var group in new[]{smoke,baseProps,crates})if(group!=null)foreach(Transform prop in group)
        {
            if(group==smoke&&prop.name!="Chimney Smoke")continue;
            var p=new Vector2(prop.position.x,prop.position.z);
            var owner=layout.houses.OrderBy(h=>Vector2.Distance(p,h.initial)).First();
            if(Vector2.Distance(p,owner.initial)>owner.half.magnitude+3)continue;
            var delta=owner.center-owner.initial;if(delta.sqrMagnitude>.000001f)shifts[prop]=new Vector3(delta.x,0,delta.y);
        }
        foreach(var shift in shifts)shift.Key.position+=shift.Value;
    }

    static int ClearLaneProps(Terrain terrain,HousePathInspection layout,List<HouseLane> lanes)
    {
        var environment=SceneManager.GetSceneByPath(Scenes[1]).GetRootGameObjects().First(g=>g.name=="Environment").transform;
        int moved=0;
        foreach(var groupName in new[]{"Base Props","Supply Crates"})
        {
            var group=environment.Find(groupName);if(group==null)continue;
            foreach(Transform prop in group)
            {
                var p=new Vector2(prop.position.x,prop.position.z);
                // Reserve the centre of the alley for player, shield and camera.
                if(!lanes.Any(l=>SegmentDistance(p,l.start,l.end)<1.8f))continue;
                bool placed=false;var owner=layout.houses.OrderBy(h=>FootprintDistance(h,p)).First();
                for(int side=0;side<4&&!placed;side++)for(int step=0;step<7&&!placed;step++)
                {
                    var outward=side<2?owner.x*(side==0?1:-1):owner.z*(side==2?1:-1);
                    var along=side<2?owner.z:owner.x;float half=side<2?owner.half.x:owner.half.y;
                    float alongHalf=side<2?owner.half.y:owner.half.x;
                    var candidate=owner.center+outward*(half+1.1f)+along*(Mathf.Lerp(-.8f,.8f,step/6f)*alongHalf);
                    if(layout.houses.Any(h=>FootprintDistance(h,candidate)<.75f)||lanes.Any(l=>SegmentDistance(candidate,l.start,l.end)<2.2f))continue;
                    float y=GroundHeight(terrain,candidate);var position=new Vector3(candidate.x,y+.55f,candidate.y);
                    var hits=Physics.OverlapBox(position,new Vector3(.55f,.45f,.55f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
                    if(hits.Any(c=>!(c is TerrainCollider)&&!c.transform.IsChildOf(prop)))continue;
                    prop.position=new Vector3(candidate.x,y-.02f,candidate.y);placed=true;moved++;Physics.SyncTransforms();
                }
                if(!placed)throw new Exception("No safe position outside the house alley for "+prop.name);
            }
        }
        return moved;
    }

    static void ReseatHouseDressing(Terrain terrain)
    {
        var environment=SceneManager.GetSceneByPath(Scenes[1]).GetRootGameObjects().First(g=>g.name=="Environment").transform;
        foreach(var groupName in new[]{"Base Props","Supply Crates"})
        {
            var group=environment.Find(groupName);if(group==null)continue;
            foreach(Transform prop in group)
            {
                var renderers=prop.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                float bottom=renderers.Min(r=>r.bounds.min.y);var p=new Vector2(prop.position.x,prop.position.z);
                prop.position+=Vector3.up*(GroundHeight(terrain,p)-.025f-bottom);
            }
        }
        Physics.SyncTransforms();
    }

    static void PaintHousePaths(Terrain terrain,List<HouseLane> lanes)
    {
        var data=terrain.terrainData;var origin=terrain.transform.position;var size=data.size;
        int n=data.alphamapResolution;
        // Work only within the village rectangle; leave all meadow/mountain painting intact.
        float x0=lanes.Min(l=>Mathf.Min(l.start.x,l.end.x))-2,x1=lanes.Max(l=>Mathf.Max(l.start.x,l.end.x))+2;
        float z0=lanes.Min(l=>Mathf.Min(l.start.y,l.end.y))-2,z1=lanes.Max(l=>Mathf.Max(l.start.y,l.end.y))+2;
        int ix=Mathf.Clamp(Mathf.FloorToInt((x0-origin.x)/size.x*n),0,n-1),iz=Mathf.Clamp(Mathf.FloorToInt((z0-origin.z)/size.z*n),0,n-1);
        int width=Mathf.Min(n-ix,Mathf.CeilToInt((x1-x0)/size.x*n)+2),height=Mathf.Min(n-iz,Mathf.CeilToInt((z1-z0)/size.z*n)+2);
        var alpha=data.GetAlphamaps(ix,iz,width,height);int dirt=Array.FindIndex(data.terrainLayers,l=>l.name.IndexOf("Dirt",StringComparison.OrdinalIgnoreCase)>=0);
        if(dirt<0)throw new Exception("Existing terrain dirt layer not found");
        for(int z=0;z<height;z++)for(int x=0;x<width;x++)
        {
            var p=new Vector2(origin.x+(ix+x)*size.x/n,origin.z+(iz+z)*size.z/n);
            float distance=lanes.Min(l=>SegmentDistance(p,l.start,l.end));if(distance>1.35f)continue;
            float target=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1.35f,distance)))*.8f;
            float w=alpha[z,x,dirt]>=target?0:(target-alpha[z,x,dirt])/Mathf.Max(.0001f,1-alpha[z,x,dirt]);
            for(int layer=0;layer<alpha.GetLength(2);layer++)alpha[z,x,layer]=Mathf.Lerp(alpha[z,x,layer],layer==dirt?1:0,w);
        }
        data.SetAlphamaps(ix,iz,alpha);
        int d=data.detailResolution;
        int dx=Mathf.Clamp(Mathf.FloorToInt((x0-origin.x)/size.x*d),0,d-1),dz=Mathf.Clamp(Mathf.FloorToInt((z0-origin.z)/size.z*d),0,d-1);
        int dw=Mathf.Min(d-dx,Mathf.CeilToInt((x1-x0)/size.x*d)+2),dh=Mathf.Min(d-dz,Mathf.CeilToInt((z1-z0)/size.z*d)+2);
        for(int layer=0;layer<data.detailPrototypes.Length;layer++)
        {
            var detail=data.GetDetailLayer(dx,dz,dw,dh,layer);
            for(int z=0;z<dh;z++)for(int x=0;x<dw;x++)
            {
                var p=new Vector2(origin.x+(dx+x)*size.x/d,origin.z+(dz+z)*size.z/d);
                if(lanes.Any(l=>SegmentDistance(p,l.start,l.end)<1.3f))detail[z,x]=0;
            }
            data.SetDetailLayer(dx,dz,layer,detail);
        }
        EditorUtility.SetDirty(data);
    }

    // The normal environment rebuild reuses the edited house placements. Keep its
    // new random rocks, shrubs and supply crates out of the same reserved alleys.
    public static void PrepareHousePassagesForEnvironment(Scene game,Terrain terrain)
    {
        var layout=ReadLoadedHouseLayout(game);var lanes=VillageRoutes(layout,terrain);if(lanes.Count==0)return;
        Physics.SyncTransforms();ClearLaneProps(terrain,layout,lanes);PaintHousePaths(terrain,lanes);
    }

    static void RecordHouseLayout(HousePathInspection layout)
    {
        var reference=JsonUtility.FromJson<Reference>(File.ReadAllText(ReferenceFile));
        var moves=new List<HouseLayoutMove>();
        foreach(var house in layout.houses)
        {
            var original=reference.scenes.SelectMany(s=>s.placements).First(p=>p.id==house.id);
            Vector3 from=original.matrix.GetColumn(3),to=house.go.transform.position;
            if((to-from).magnitude<.001f)continue;
            if((to-from).magnitude>8||Mathf.Abs(to.y-from.y)>.001f)throw new Exception("Invalid house-only layout revision");
            moves.Add(new HouseLayoutMove {id=house.id,name=house.name,from=from,to=to});
        }
        File.WriteAllText(PathLayoutFile,JsonUtility.ToJson(new HouseLayoutRevision {
            reason="Open runnable two-metre alleys between existing village houses; preserve original art bounds, rotations, scales and fortifications.",
            minimumGap=HousePassageWidth,moves=moves.ToArray()},true));
    }

    [MenuItem("Medieval Warrior/Buildings/Fix house pathways and rebake navigation")]
    public static void FixHousePaths()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var layout=ReadHouseLayout();var report=new HousePathReport {houses=layout.houses.Length,playerRadius=layout.playerRadius,playerHeight=layout.playerHeight};
        for(int i=0;i<layout.houses.Length;i++)for(int j=i+1;j<layout.houses.Length;j++)
            if(PassageGap(layout.houses[i],layout.houses[j],out _)<HousePassageWidth)report.originalBlockedPairs++;
        bool changed=report.originalBlockedPairs>0;
        if(!changed&&File.Exists("ArtSource/Buildings/pathways-audit.json"))
            report=JsonUtility.FromJson<HousePathReport>(File.ReadAllText("ArtSource/Buildings/pathways-audit.json"));
        if(changed)
        {
            CaptureVillagePaths("Before");SpaceHouses(layout);MoveHouseDressing(layout);
            foreach(var house in layout.houses)
            {
                var delta=house.center-house.initial;house.go.transform.position+=new Vector3(delta.x,0,delta.y);
                if(delta.magnitude>.001f)report.movedHouses++;report.maximumHouseMove=Mathf.Max(report.maximumHouseMove,delta.magnitude);
            }
        }
        report.minimumGap=float.PositiveInfinity;
        for(int i=0;i<layout.houses.Length;i++)for(int j=i+1;j<layout.houses.Length;j++)report.minimumGap=Mathf.Min(report.minimumGap,PassageGap(layout.houses[i],layout.houses[j],out _));
        if(report.minimumGap<HousePassageWidth)throw new Exception("House alley still too narrow: "+report.minimumGap);
        Physics.SyncTransforms();var terrain=Object.FindFirstObjectByType<Terrain>();
        report.terrainCells+=SeatMovedHousePads(terrain,layout);Physics.SyncTransforms();var lanes=HouseLanes(layout);
        ReseatHouseDressing(terrain);report.movedProps+=ClearLaneProps(terrain,layout,lanes);PaintHousePaths(terrain,lanes);RecordHouseLayout(layout);
        var game=SceneManager.GetSceneByPath(Scenes[0]);BakeTerrainContact(game,terrain);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetSceneByPath(Scenes[1]));EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
        CaptureVillagePaths("After");CaptureHouseLaneViews(terrain,lanes);
        File.WriteAllText("Artifacts/Buildings/Paths/after.json",JsonUtility.ToJson(layout,true));
        report.lanes=lanes.ToArray();report.result="LAYOUT SAVED; verification pending";
        File.WriteAllText("ArtSource/Buildings/pathways-audit.json",JsonUtility.ToJson(report,true));
        RebakeNavigation();VerifyHousePaths();Review();InspectNavigation();VerifyEnvironmentAssets();
        Debug.Log("[House paths] PASS: "+report.originalBlockedPairs+" blocked pairs opened; minimum gap "+report.minimumGap.ToString("F2")+"m");
    }

    [MenuItem("Medieval Warrior/Buildings/Verify house pathways")]
    public static void VerifyHousePaths()
    {
        var layout=ReadHouseLayout();var terrain=Object.FindFirstObjectByType<Terrain>();var lanes=HouseLanes(layout);
        var player=SceneManager.GetSceneByPath(Scenes[0]).GetRootGameObjects().First(g=>g.CompareTag("Player"));bool active=player.activeSelf;player.SetActive(false);
        var probe=new GameObject("Actual-size warrior passage probe");var controller=probe.AddComponent<CharacterController>();
        controller.radius=layout.playerRadius;controller.height=layout.playerHeight;controller.center=Vector3.up*(layout.playerHeight*.5f);
        controller.skinWidth=.025f;controller.stepOffset=.3f;controller.slopeLimit=player.GetComponent<PlayerControlSpeed>().maxSlope;
        int mask=~(1<<2);Physics.SyncTransforms();
        try
        {
            if(!NavMesh.SamplePosition(player.transform.position,out var playerNav,4,NavMesh.AllAreas))throw new Exception("Player start missing from navigation");
            foreach(var lane in lanes)
            {
                int steps=Mathf.CeilToInt(Vector2.Distance(lane.start,lane.end)/.15f);
                for(int i=0;i<=steps;i++)
                {
                    var p=Vector2.Lerp(lane.start,lane.end,i/(float)steps);float y=GroundHeight(terrain,p);
                    var low=new Vector3(p.x,y+layout.playerRadius+.1f,p.y);var high=low+Vector3.up*(layout.playerHeight-2*layout.playerRadius);
                    var hits=Physics.OverlapCapsule(low,high,layout.playerRadius+.12f,mask,QueryTriggerInteraction.Ignore);
                    var blocked=hits.Where(c=>c.gameObject!=probe&&!(c is TerrainCollider)).ToArray();
                    if(blocked.Length>0)throw new Exception("Alley blocked at "+p+" between "+lane.between+": "+string.Join(", ",blocked.Select(c=>c.name)));
                    lane.capsuleSamples++;
                }
                for(int direction=0;direction<2;direction++)
                {
                    var start=direction==0?lane.start:lane.end;var end=direction==0?lane.end:lane.start;
                    controller.enabled=false;probe.transform.position=new Vector3(start.x,GroundHeight(terrain,start)+.08f,start.y);controller.enabled=true;
                    var heading=(end-start).normalized;
                    // Same 10m/s sprint speed as gameplay, integrated at 50Hz, including
                    // gravity and actual CharacterController collisions with the terrain.
                    int ticks=Mathf.CeilToInt(Vector2.Distance(start,end)/.2f);
                    for(int tick=0;tick<ticks;tick++)controller.Move(new Vector3(heading.x*.2f,-.12f,heading.y*.2f));
                    var expected=start+heading*(ticks*.2f);float error=Vector2.Distance(new Vector2(probe.transform.position.x,probe.transform.position.z),expected);
                    if(error>.28f)throw new Exception("Warrior movement caught in "+lane.between+": "+error.ToString("F2")+"m error");
                    if(direction==0)lane.forwardError=error;else lane.reverseError=error;
                }
                var a=new Vector3(lane.start.x,GroundHeight(terrain,lane.start),lane.start.y);var b=new Vector3(lane.end.x,GroundHeight(terrain,lane.end),lane.end.y);
                if(!NavMesh.SamplePosition(a,out var na,.7f,NavMesh.AllAreas)||!NavMesh.SamplePosition(b,out var nb,.7f,NavMesh.AllAreas))throw new Exception("Alley endpoint missing from NavMesh: "+lane.between);
                var route=new NavMeshPath();if(!NavMesh.CalculatePath(na.position,nb.position,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete)throw new Exception("Alley route incomplete: "+lane.between);
                for(int i=1;i<route.corners.Length;i++)lane.navLength+=Vector3.Distance(route.corners[i-1],route.corners[i]);
                if(lane.navLength>Vector3.Distance(a,b)*1.6f+1)throw new Exception("NavMesh detours around the alley: "+lane.between);
                var approach=new NavMeshPath();
                if(!NavMesh.CalculatePath(playerNav.position,na.position,NavMesh.AllAreas,approach)||approach.status!=NavMeshPathStatus.PathComplete)
                    throw new Exception("Alley is on an isolated navigation island: "+lane.between);
            }
        }
        finally {Object.DestroyImmediate(probe);player.SetActive(active);}
        var report=JsonUtility.FromJson<HousePathReport>(File.ReadAllText("ArtSource/Buildings/pathways-audit.json"));
        report.lanes=lanes.ToArray();report.result="PASS";
        File.WriteAllText("ArtSource/Buildings/pathways-audit.json",JsonUtility.ToJson(report,true));
        Debug.Log("[House paths] Traversal PASS: "+lanes.Count+" alleys, "+lanes.Sum(l=>l.capsuleSamples)+" capsule clearance probes, both sprint directions and direct NavMesh routes");
    }
    static void CaptureVillagePaths(string label)
    {
        var go=new GameObject("House pathway review camera");var camera=go.AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.Skybox;camera.allowHDR=true;camera.farClipPlane=500;
        camera.transform.position=new Vector3(-19,135,18);camera.transform.rotation=Quaternion.Euler(90,0,0);
        camera.orthographic=true;camera.orthographicSize=65;
        var target=new RenderTexture(1500,1500,24);camera.targetTexture=target;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(1500,1500,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1500,1500),0,0);image.Apply();
        Directory.CreateDirectory("Artifacts/Buildings/Paths");File.WriteAllBytes("Artifacts/Buildings/Paths/"+label+".png",image.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(target);Object.DestroyImmediate(go);
    }

    static void CaptureHouseLaneViews(Terrain terrain,List<HouseLane> lanes)
    {
        int index=0;
        foreach(string pair in new[]{"House (6) / House B","House (11) / HouseB (1)"})
        {
            var lane=lanes.FirstOrDefault(l=>l.between==pair);if(lane==null)continue;
            var go=new GameObject("Alley review camera");var camera=go.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;
            camera.fieldOfView=65;camera.nearClipPlane=.08f;camera.farClipPlane=400;camera.allowHDR=true;
            var p=lane.start;var end=lane.end;
            camera.transform.position=new Vector3(p.x,GroundHeight(terrain,p)+1.8f,p.y);
            camera.transform.LookAt(new Vector3(end.x,GroundHeight(terrain,end)+1.65f,end.y));
            var target=new RenderTexture(1400,1000,24);camera.targetTexture=target;camera.Render();
            var old=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1400,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();
            File.WriteAllBytes("Artifacts/Buildings/Paths/Alley_"+(++index)+".png",image.EncodeToPNG());
            RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(image);Object.DestroyImmediate(target);Object.DestroyImmediate(go);
        }
    }
}
