using System.Collections.Generic;
using UnityEngine;

// Fire-and-forget sound playback with a small voice pool. Clips live in Resources/Audio.
public class AudioManager : MonoBehaviour
{
    static AudioManager instance;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly List<AudioSource> voices = new List<AudioSource>();
    AudioSource music, battle;
    float musicScale = 1f, battleTarget, battleLevel;
    static float nextAmbientVoice;
    float creatureFocusUntil,creatureFocus;
    static readonly string[] combatBank={"sword_swing","sword_hit","shield_block","bow_draw","bow_release","arrow_hit","arrow_flesh","dodge","player_hurt",
        "zombie_groan0","zombie_groan1","zombie_groan2","zombie_attack","zombie_die","brute_roar","spit","splat","bloater_fuse","explosion","emerge",
        "parry","thunder_guard","phantom_cleave","splinter","boss_horn","wave_horn","victory","defeat","ambient_meadow","music_battle",
        "ambient_last_stand","last_stand_rain","last_stand_thunder","music_last_stand"};

    static AudioManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("Audio Manager");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AudioManager>();
            }
            return instance;
        }
    }

    AudioClip Clip(string name)
    {
        if (!clips.TryGetValue(name, out var c)||c==null)
        {
            c = Resources.Load<AudioClip>("Audio/" + name);
            clips[name] = c;
        }
        return c;
    }

    public static void WarmCombatBank()
    {
        using var timing=BattleLoadMetrics.Measure("Combat audio synchronous warmup");
        foreach(string name in combatBank)
        {
            var clip=Instance.Clip(name);if(clip!=null)clip.LoadAudioData();
        }
    }
    public static System.Collections.IEnumerator PreloadCombatBank(System.Action<float> progress)
    {
        var manager=Instance;
        for(int i=0;i<combatBank.Length;i++)
        {
            string name=combatBank[i];
            if(!manager.clips.TryGetValue(name,out var clip)||clip==null)
            {
                var request=Resources.LoadAsync<AudioClip>("Audio/"+name);yield return request;
                clip=request.asset as AudioClip;manager.clips[name]=clip;
            }
            if(clip!=null)
            {
                if(clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
                while(clip.loadState==AudioDataLoadState.Loading)yield return null;
            }
            progress?.Invoke((i+1f)/combatBank.Length);
            yield return null;
        }
    }

    AudioSource FreeVoice(int priority = 128)
    {
        foreach (var v in voices) if (!v.isPlaying) return v;
        if (voices.Count >= 24)
        {
            AudioSource leastImportant = voices[0];
            foreach (var v in voices) if (v.priority > leastImportant.priority) leastImportant = v;
            return leastImportant.priority >= priority ? leastImportant : null;
        }
        var go = new GameObject("Voice");
        go.transform.SetParent(transform);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 3f;
        s.maxDistance = 45f;
        s.dopplerLevel = 0f;
        voices.Add(s);
        return s;
    }

    /// <summary>Plays a 2D UI/HUD sound.</summary>
    public static void Play2D(string name, float volume = 1f, float pitch = 1f)
    {
        var clip = Instance.Clip(name);
        if (clip == null) return;
        var v = Instance.FreeVoice(64);
        if (v == null) return;
        if (v.TryGetComponent<AudioLowPassFilter>(out var filter)) filter.enabled = false;
        v.priority = 64;
        v.transform.position = Vector3.zero;
        v.spatialBlend = 0f;
        v.pitch = pitch;
        v.volume = volume * GameSettings.SfxVolume;
        v.clip = clip;
        v.Play();
    }

    /// <summary>Plays a positional sound with a little pitch variation so repeats don't sound robotic.</summary>
    public static bool PlayAt(string name, Vector3 position, float volume = 1f, float pitchJitter = 0.08f,
        float basePitch = 1f, float lowPass = 22000f, int priority = 128, float spatialBlend = 0.85f,
        float minDistance=3f,float maxDistance=45f)
    {
        var clip = Instance.Clip(name);
        if (clip == null) return false;
        var v = Instance.FreeVoice(priority);
        if (v == null) return false;
        var filter = v.GetComponent<AudioLowPassFilter>();
        if (lowPass < 22000f && filter == null) filter = v.gameObject.AddComponent<AudioLowPassFilter>();
        if (filter != null) { filter.enabled = lowPass < 22000f; filter.cutoffFrequency = lowPass; }
        v.priority = priority;
        // Pooled voices must restore their distance profile on every use.
        bool creature=CreatureCue(name)&&volume>=.45f;
        v.minDistance=creature?Mathf.Max(minDistance,8):minDistance;
        v.maxDistance=creature?Mathf.Max(maxDistance,60):maxDistance;
        v.transform.position = position;
        v.spatialBlend = spatialBlend;
        // Relative variation preserves each archetype's register while keeping
        // every repeated enemy/combat contact subtly different. UI/music are fixed.
        float variation=Mathf.Lerp(1,Random.Range(.92f,1.08f),Mathf.Clamp01(pitchJitter/.08f));
        v.pitch = Mathf.Clamp(basePitch * variation, 0.4f, 1.6f);
        v.volume = volume * GameSettings.SfxVolume;
        v.clip = clip;
        v.Play();
        if(creature&&GameSettings.SfxVolume>.01f&&Camera.main!=null&&Vector3.Distance(Camera.main.transform.position,position)<35)
            Instance.creatureFocusUntil=Mathf.Max(Instance.creatureFocusUntil,Time.unscaledTime+Mathf.Min(clip.length/v.pitch,2.5f));
        return true;
    }

    public static void PlayPlayerDamage(Vector3 position, float damage, float healthFraction, bool blocked)
    {
        float force = Mathf.InverseLerp(3f, 30f, damage);
        PlayAt("shield_block", position, blocked ? 0.35f : Mathf.Lerp(0.12f, 0.28f, force), 0.06f,
            0.85f, 6500f, 40, 0.2f);
        if (!blocked)
            PlayAt("player_hurt", position, Mathf.Lerp(0.65f, 1f, force), 0.07f,
                healthFraction < 0.25f ? 0.84f : 0.97f, 10000f, 24, 0.1f);
    }

    static bool CreatureCue(string name)=>name.StartsWith("zombie_",System.StringComparison.Ordinal)||name=="brute_roar"||
        name=="spit"||name=="splat"||name=="bloater_fuse"||name=="emerge"||name=="explosion";

    public static bool PlayZombieVoice(string clip, Vector3 position, ZombieType type)
    {
        float distance = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, position) : 0f;
        if (distance > 60f) return false;
        bool ambient = clip.StartsWith("zombie_groan", System.StringComparison.Ordinal);
        // Stagger the horde instead of letting dozens of identical groans mask attacks.
        if (ambient && Time.time < nextAmbientVoice) return false;
        if(ambient)
        {
            int audible=0;
            foreach(var voice in Instance.voices)
                if(voice.isPlaying&&voice.clip!=null&&voice.clip.name.StartsWith("zombie_groan",System.StringComparison.Ordinal))audible++;
            if(audible>=3)return false;
        }
        float pitch = type == ZombieType.Warlord ? .82f : type == ZombieType.Brute ? .86f
            : type == ZombieType.Bloater ? .9f : type == ZombieType.Runner ? 1.04f : 1f;
        float volume = ambient ? .88f : clip == "brute_roar" ? 1f : .95f;
        float filter = Mathf.Lerp(14000f,6500f,Mathf.InverseLerp(10,55,distance));
        bool played=PlayAt(clip,position+Vector3.up*1.2f,volume,.08f,pitch,filter,ambient?148:72,1,ambient?9:11,60);
        if(played&&ambient)nextAmbientVoice=Time.time+.38f;
        return played;
    }

    public static void PlayMusic(string name, float volume = 1f)
    {
        var m = Instance.music;
        if (m == null)
        {
            m = Instance.music = Instance.gameObject.AddComponent<AudioSource>();
            m.loop = true;
            m.spatialBlend = 0f;
            m.playOnAwake = false;
        }
        var clip = Instance.Clip(name);
        if(name=="music_menu"){Instance.creatureFocusUntil=0;Instance.creatureFocus=0;nextAmbientVoice=0;}
        Instance.musicScale = volume;
        m.volume = volume * GameSettings.MusicVolume;
        if (m.clip == clip && m.isPlaying) return;
        m.clip = clip;
        if (clip != null) m.Play();
    }

    public static void StopMusic()
    {
        if (instance != null && instance.music != null) instance.music.Stop();
        SetBattleIntensity(0f, true);
    }

    /// <summary>War drums that swell as the fighting gets close (0 = calm, 1 = full battle).</summary>
    public static void SetBattleIntensity(float level, bool immediate = false)
    {
        var m = Instance;
        if (m.battle == null)
        {
            if (level <= 0f) return;
            m.battle = m.gameObject.AddComponent<AudioSource>();
            m.battle.loop = true;
            m.battle.spatialBlend = 0f;
            m.battle.playOnAwake = false;
            m.battle.clip = m.Clip("music_battle");
            m.battle.volume = 0f;
            if (m.battle.clip != null) m.battle.Play();
        }
        m.battleTarget = Mathf.Clamp01(level);
        if (immediate) m.battleLevel = m.battleTarget;
    }

    void Update()
    {
        creatureFocus=Mathf.MoveTowards(creatureFocus,Time.unscaledTime<creatureFocusUntil?1:0,Time.unscaledDeltaTime*(Time.unscaledTime<creatureFocusUntil?8:2));
        bool menu=music!=null&&music.clip!=null&&music.clip.name=="music_menu";
        float space=menu?1:Mathf.Lerp(1,.62f,creatureFocus);
        battleLevel = Mathf.MoveTowards(battleLevel, battleTarget, Time.unscaledDeltaTime * (battleTarget > battleLevel ? 0.5f : 0.2f));
        if(battle!=null)battle.volume = battleLevel * 0.85f * GameSettings.MusicVolume*space;
        if (music != null) music.volume = musicScale * GameSettings.MusicVolume * (1f - battleLevel * 0.6f)*space;
    }

    public static void RefreshMusicVolume()
    {
        if (instance != null && instance.music != null) instance.music.volume = instance.musicScale * GameSettings.MusicVolume;
    }
}
