using UnityEngine;

// One owner for gameplay time. Hit-stop is a short unscaled overlay; pause,
// boon selection and cinematics explicitly replace it instead of being resumed
// by a stale coroutine. Fixed timestep and Rigidbody velocities stay untouched.
public sealed class CombatTime : MonoBehaviour
{
    static CombatTime instance;
    static float gameplayScale=1, stopStarted, stopUntil;
    public static bool IsHitStopped => instance!=null && gameplayScale>0 && Time.unscaledTime<stopUntil;
    public static float GameplayScale => gameplayScale;

    public static void SetScale(float scale)
    {
        gameplayScale=Mathf.Max(0,scale);stopUntil=0;Time.timeScale=gameplayScale;
    }
    public static void HitStop(float seconds)
    {
        if(gameplayScale<=0)return;
        if(instance==null)
        {
            var go=new GameObject("Combat time");DontDestroyOnLoad(go);instance=go.AddComponent<CombatTime>();
        }
        float now=Time.unscaledTime;
        if(!IsHitStopped)stopStarted=now;
        // Simultaneous cleaves/arrow pierces coalesce, never accumulating a long freeze.
        stopUntil=Mathf.Min(stopStarted+.08f,Mathf.Max(stopUntil,now+Mathf.Clamp(seconds,.05f,.08f)));
        Time.timeScale=0;
    }
    void Update()
    {
        if(stopUntil<=0||Time.unscaledTime<stopUntil)return;
        stopUntil=0;Time.timeScale=gameplayScale;
    }
    void OnDestroy()
    {
        if(instance!=this)return;
        instance=null;stopUntil=0;Time.timeScale=gameplayScale;
    }
}
