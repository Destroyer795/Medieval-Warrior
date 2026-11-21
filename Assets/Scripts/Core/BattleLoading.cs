using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Keep the menu's castle scene and resident combat assets across runs. Scene and
// resource IO advance asynchronously before the player can enter combat.
public sealed class BattleLoading : MonoBehaviour
{
    static BattleLoading instance;
    bool preparing,ready,transitioning;
    float progress;
    GameObject zombiePrefab;
    readonly List<UnityEngine.Object> residentAssets=new List<UnityEngine.Object>();
    [Serializable] public sealed class Manifest {public string[] clips,meshes;}
    Canvas screen;
    Text caption;
    public static bool Ready=>instance!=null&&instance.ready;
    public static bool IsTransitioning=>instance!=null&&instance.transitioning;
    public static string Phase {get;private set;}="Menu";
    public static GameObject ZombiePrefab=>instance!=null?instance.zombiePrefab:null;
    static BattleLoading Instance
    {
        get
        {
            if(instance==null)
            {
                var go=new GameObject("Battle loading");DontDestroyOnLoad(go);instance=go.AddComponent<BattleLoading>();
            }
            return instance;
        }
    }
    public static void Prepare()
    {
        var loader=Instance;
        if(!loader.ready&&!loader.preparing)loader.StartCoroutine(loader.PrepareAssets());
    }
    IEnumerator PrepareAssets()
    {
        preparing=true;progress=0;
        yield return null; // Let the menu render before starting background IO.
        var previousPriority=Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority=UnityEngine.ThreadPriority.Low;
        try
        {
            var manifestRequest=Resources.LoadAsync<TextAsset>("Revenants/Preload");yield return manifestRequest;
            var manifestAsset=manifestRequest.asset as TextAsset;
            if(manifestAsset==null)throw new InvalidOperationException("Rebuild the Revenant preload manifest before preparing battle");
            var manifest=JsonUtility.FromJson<Manifest>(manifestAsset.text);
            Phase="Preparing animation clips";
            yield return PreloadBatch<AnimationClip>(manifest.clips,0,.48f);
            Phase="Preparing character meshes";
            yield return PreloadBatch<Mesh>(manifest.meshes,.48f,.30f);
            Phase="Loading roster";
            var roster=Resources.LoadAsync<RevenantAsset>("Revenants/Roster");
            var prefab=Resources.LoadAsync<GameObject>("Revenants/Revenant");
            while(!roster.isDone||!prefab.isDone)
            {
                progress=.78f+(roster.progress+prefab.progress)*.06f;yield return null;
            }
            RevenantVisual.CacheRoster(roster.asset as RevenantAsset);
            zombiePrefab=prefab.asset as GameObject;
            if(zombiePrefab==null)throw new InvalidOperationException("The Revenant spawn prefab is missing");
            Phase="Loading combat audio";
            yield return AudioManager.PreloadCombatBank(value=>progress=.9f+value*.1f);
            progress=1;ready=true;Phase="Prepared";
        }
        finally{Application.backgroundLoadingPriority=previousPriority;preparing=false;}
    }
    IEnumerator PreloadBatch<T>(string[] paths,float start,float amount) where T:UnityEngine.Object
    {
        const int batch=2;
        for(int i=0;i<paths.Length;i+=batch)
        {
            int count=Mathf.Min(batch,paths.Length-i);var requests=new ResourceRequest[count];
            for(int j=0;j<count;j++)requests[j]=Resources.LoadAsync<T>(paths[i+j]);
            bool complete=false;
            while(!complete)
            {
                complete=true;float fraction=0;
                foreach(var request in requests){complete&=request.isDone;fraction+=request.progress;}
                progress=start+amount*(i+fraction)/paths.Length;
                if(!complete)yield return null;
            }
            for(int j=0;j<count;j++)
            {
                if(requests[j].asset==null)throw new InvalidOperationException("Missing combat asset: "+paths[i+j]);
                residentAssets.Add(requests[j].asset);
            }
            progress=start+amount*(i+count)/(float)paths.Length;yield return null;
        }
    }
    public static void EnterBattle()=>Instance.BeginTransition("Semi-Final");
    public static void ReturnToMenu()=>Instance.BeginTransition("MainMenu");
    void BeginTransition(string target)
    {
        if(transitioning)return;
        transitioning=true;StartCoroutine(Transition(target));
    }
    void ShowScreen(bool show)
    {
        if(screen==null)
        {
            var go=new GameObject("Loading screen");go.transform.SetParent(transform,false);
            screen=go.AddComponent<Canvas>();screen.renderMode=RenderMode.ScreenSpaceOverlay;screen.sortingOrder=32000;
            go.AddComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
            var panel=new GameObject("Background").AddComponent<Image>();panel.transform.SetParent(go.transform,false);panel.color=new Color(.018f,.022f,.025f,1);
            var rect=panel.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            caption=new GameObject("Preparing the courtyard").AddComponent<Text>();caption.transform.SetParent(panel.transform,false);
            caption.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");caption.fontSize=28;caption.color=new Color(.86f,.77f,.56f);caption.alignment=TextAnchor.MiddleCenter;
            caption.rectTransform.anchorMin=Vector2.zero;caption.rectTransform.anchorMax=Vector2.one;caption.rectTransform.offsetMin=caption.rectTransform.offsetMax=Vector2.zero;
        }
        screen.gameObject.SetActive(show);
    }
    IEnumerator Transition(string target)
    {
        ShowScreen(true);CombatTime.SetScale(1);
        try
        {
            if(target=="Semi-Final")
            {
                Prepare();
                while(!ready)
                {
                    caption.text="Preparing the courtyard"+new string('.',1+(int)(Time.realtimeSinceStartup*2)%3)+"\n"+Mathf.RoundToInt(progress*100)+"%";
                    yield return null;
                }
            }
            caption.text=target=="MainMenu"?"Returning to the keep...":"The gates open...";
            yield return null;
            if(!SceneManager.GetSceneByName("Background").isLoaded)
                yield return SceneManager.LoadSceneAsync("Background",LoadSceneMode.Additive);
            // Unload only the per-run scene. The expensive world stays resident.
            foreach(string sceneName in new[]{"Semi-Final","MainMenu"})
            {
                var old=SceneManager.GetSceneByName(sceneName);
                if(old.isLoaded){Phase="Unloading "+sceneName;yield return SceneManager.UnloadSceneAsync(old);}
            }
            Phase="Activating "+target;
            void Activate(Scene scene,LoadSceneMode mode){if(scene.name==target)SceneManager.SetActiveScene(scene);}
            SceneManager.sceneLoaded+=Activate;
            try{yield return SceneManager.LoadSceneAsync(target,LoadSceneMode.Additive);}
            finally{SceneManager.sceneLoaded-=Activate;}
            Phase="Starting character";
            yield return null; // Let Awake/Start finish behind the loading screen.
        }
        finally{ShowScreen(false);transitioning=false;Phase="Playing";}
    }
}
