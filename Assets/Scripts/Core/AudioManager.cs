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
    public static void PlayAt(string name, Vector3 position, float volume = 1f, float pitchJitter = 0.08f,
        float basePitch = 1f, float lowPass = 22000f, int priority = 128, float spatialBlend = 0.85f)
    {
        var clip = Instance.Clip(name);
        if (clip == null) return;
        var v = Instance.FreeVoice(priority);
        if (v == null) return;
        var filter = v.GetComponent<AudioLowPassFilter>();
        if (lowPass < 22000f && filter == null) filter = v.gameObject.AddComponent<AudioLowPassFilter>();
        if (filter != null) { filter.enabled = lowPass < 22000f; filter.cutoffFrequency = lowPass; }
        v.priority = priority;
        v.transform.position = position;
        v.spatialBlend = spatialBlend;
        v.pitch = Mathf.Clamp(basePitch + Random.Range(-pitchJitter, pitchJitter), 0.4f, 1.6f);
        v.volume = volume * GameSettings.SfxVolume;
        v.clip = clip;
        v.Play();
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

    public static void PlayZombieVoice(string clip, Vector3 position, ZombieType type)
    {
        float distance = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, position) : 0f;
        if (distance > 45f) return;
        bool ambient = clip.StartsWith("zombie_groan", System.StringComparison.Ordinal);
        // Stagger the horde instead of letting dozens of identical groans mask attacks.
        if (ambient && Time.time < nextAmbientVoice) return;
        if (ambient) nextAmbientVoice = Time.time + 0.3f;
        float pitch = type == ZombieType.Warlord ? 0.62f : type == ZombieType.Brute ? 0.7f
            : type == ZombieType.Bloater ? 0.76f : type == ZombieType.Runner ? 1.04f : 0.86f;
        float volume = ambient ? 0.5f : clip == "brute_roar" ? 0.95f : 0.75f;
        float filter = Mathf.Lerp(9000f, 2200f, Mathf.InverseLerp(5f, 40f, distance));
        PlayAt(clip, position + Vector3.up * 1.2f, volume, 0.045f, pitch, filter, ambient ? 180 : 80, 1f);
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
