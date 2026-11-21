using System.IO;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class HordeArenaReview
{
    [MenuItem("Medieval Warrior/Horde/Review bridge navigation")]
    public static void Run()
    {
        var setup=EditorSceneManager.GetSceneManagerSetup();
        var report=new StringBuilder();
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Semi-Final.unity");
            EditorSceneManager.OpenScene("Assets/Scenes/Background.unity",OpenSceneMode.Additive);
            foreach(var surface in Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None))surface.AddData();
            Physics.SyncTransforms();
            var player=GameObject.FindWithTag("Player");
            NavMesh.SamplePosition(player.transform.position,out var start,5,NavMesh.AllAreas);
            report.AppendLine("Player "+player.transform.position+" nav "+start.position);
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(f=>f.name.StartsWith("Bridge")))
            {
                var b=filter.GetComponent<Renderer>().bounds;Vector3 probe=new Vector3(b.center.x,b.max.y+.3f,b.center.z);
                bool floor=Physics.Raycast(probe,Vector3.down,out var hit,b.size.y+2,~0,QueryTriggerInteraction.Ignore);
                bool nav=NavMesh.SamplePosition(hit.point,out var sample,1.3f,NavMesh.AllAreas);
                var path=new NavMeshPath();NavMesh.CalculatePath(start.position,sample.position,NavMesh.AllAreas,path);
                report.AppendLine($"{filter.name} floor={floor} hit={hit.point} nav={nav} sample={sample.position} route={path.status} corners={string.Join(" / ",path.corners.Select(p=>p.ToString()))}");
            }
            foreach(float x in new[]{-3f,0,3,27,30,33})
            {
                var at=new Vector3(x,30,20.79f);
                foreach(var hit in Physics.RaycastAll(at,Vector3.down,60).OrderBy(h=>h.distance))report.AppendLine($"Approach x={x} {hit.collider.name}: {hit.point}");
            }
            Directory.CreateDirectory("Artifacts/Horde");File.WriteAllText("Artifacts/Horde/bridge-navigation.txt",report.ToString());Debug.Log(report);
        }
        finally{if(setup.Any(s=>s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
}
