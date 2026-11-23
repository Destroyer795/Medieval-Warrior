using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    TextMeshProUGUI caption, progressLabel;
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
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            go.AddComponent<GraphicRaycaster>();
            var panel = UIKit.Img(go.transform, null, new Color(.02f, .015f, .01f), "Loading backdrop");
            panel.raycastTarget = true;
            panel.rectTransform.Fill();
            var center = new Vector2(.5f, .5f);
            caption = UIKit.Text(panel.transform, "", 56f, UIKit.Gold, TextAlignmentOptions.Center, true);
            caption.name = "Loading title";
            caption.rectTransform.Place(center, center, new Vector2(0f, 24f), new Vector2(960f, 80f));
            progressLabel = UIKit.Text(panel.transform, "", 26f, UIKit.Parchment);
            progressLabel.rectTransform.Place(center, center, new Vector2(0f, -52f), new Vector2(690f, 44f));
        }
        screen.gameObject.SetActive(show);
    }
    IEnumerator Transition(string target)
    {
        ShowScreen(true);CombatTime.SetScale(1);
        try
        {
            caption.text = target == "MainMenu" ? "Returning to the Keep" : "The gates open...";
            progressLabel.text = "";
            if(target=="Semi-Final")
            {
                Prepare();
                while(!ready)
                {
                    progressLabel.text = $"Preparing the courtyard  {Mathf.RoundToInt(progress * 100)}%";
                    yield return null;
                }
            }
            progressLabel.text = "";
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
