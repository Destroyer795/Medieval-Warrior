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
        if (!clips.TryGetValue(name, out var c))
        {
            c = Resources.Load<AudioClip>("Audio/" + name);
            clips[name] = c;
        }
        return c;
    }

    AudioSource FreeVoice()
    {
        foreach (var v in voices) if (!v.isPlaying) return v;
        if (voices.Count >= 24) return voices[Random.Range(0, voices.Count)];
        var go = new GameObject("Voice");
        go.transform.SetParent(transform);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 4f;
        s.maxDistance = 55f;
        s.dopplerLevel = 0f;
        voices.Add(s);
        return s;
    }

    /// <summary>Plays a 2D UI/HUD sound.</summary>
    public static void Play2D(string name, float volume = 1f, float pitch = 1f)
    {
        var clip = Instance.Clip(name);
        if (clip == null) return;
        var v = Instance.FreeVoice();
        v.transform.position = Vector3.zero;
        v.spatialBlend = 0f;
        v.pitch = pitch;
        v.volume = volume * GameSettings.SfxVolume;
        v.clip = clip;
        v.Play();
    }

    /// <summary>Plays a positional sound with a little pitch variation so repeats don't sound robotic.</summary>
    public static void PlayAt(string name, Vector3 position, float volume = 1f, float pitchJitter = 0.08f)
    {
        var clip = Instance.Clip(name);
        if (clip == null) return;
        var v = Instance.FreeVoice();
        v.transform.position = position;
        v.spatialBlend = 0.85f;
        v.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        v.volume = volume * GameSettings.SfxVolume;
        v.clip = clip;
        v.Play();
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
        if (battle == null) return;
        battleLevel = Mathf.MoveTowards(battleLevel, battleTarget, Time.unscaledDeltaTime * (battleTarget > battleLevel ? 0.5f : 0.2f));
        battle.volume = battleLevel * 0.85f * GameSettings.MusicVolume;
        if (music != null) music.volume = musicScale * GameSettings.MusicVolume * (1f - battleLevel * 0.6f);
    }

    public static void RefreshMusicVolume()
    {
        if (instance != null && instance.music != null) instance.music.volume = instance.musicScale * GameSettings.MusicVolume;
    }
}
