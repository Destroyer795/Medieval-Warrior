#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public partial class SmokeTestDriver
{
    IEnumerator ReviewLastStand()
    {
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        var countdown = typeof(GameManager).GetField("<TimeLeft>k__BackingField", fields);
        float timeout = Time.realtimeSinceStartup + 60f;
        while ((GameManager.instance == null || GameManager.instance.State == GameManager.MatchState.Intro)
            && Time.realtimeSinceStartup < timeout) yield return null;

        var gm = GameManager.instance;
        var stand = gm != null ? gm.LastStand : null;
        var player = GameObject.FindWithTag("Player");
        Check(stand != null && player != null && gm.TimeLeft > 295f, "Last Stand starts with five minutes");
        if (stand == null || player == null) yield break;
        stand.GraveSelected = true;

        var health = player.GetComponent<PlayerHealth>();
        var body = player.GetComponent<Rigidbody>();
        var sun = RenderSettings.sun;
        var midnightRotation = sun.transform.rotation;
        var playerStart = body.position;
        float initialRadius = stand.Radius;
        int world = SceneManager.GetSceneByName("Background").handle;
        var stormRain = gm.GetComponentsInChildren<ParticleSystem>().Single(p => p.name == "Blood rain");
        bool nightSky = RenderSettings.skybox.shader.name == "Medieval Warrior/Last Stand/Sky";
        Check(nightSky && RenderSettings.fogMode == FogMode.ExponentialSquared && RenderSettings.fogDensity > .006f &&
            sun.intensity < .6f && !stormRain.isPlaying,
            "midnight applies its skybox, fog and moonlight");
        Capture("last_stand_01_midnight");
        var camera = Camera.main;
        Check(camera != null && camera.GetCommandBuffers(CameraEvent.BeforeForwardAlpha)
            .Any(buffer => buffer.name == "Last Stand volumetric mist"), "volumetric mist renders before the translucent fire and rain");
        if (camera != null)
            CaptureMiasma("last_stand_00_mountain_front", camera, stand.Center + new Vector3(87f, 43f, -152f), stand.Center);

        health.InvulnerableUntil = float.PositiveInfinity;
        gm.Pause();
        float pausedTimeLeft = gm.TimeLeft;
        float pausedRadius = stand.Radius;
        yield return Wait(.35f);
        Check(gm.TimeLeft == pausedTimeLeft && stand.Radius == pausedRadius, "pause freezes the clock and corruption");
        gm.Resume();
        typeof(GameManager).GetField("nextBoonTime", fields).SetValue(gm, 999f);

        countdown.SetValue(gm, 150.035f);
        timeout = Time.realtimeSinceStartup + 4f;
        while (gm.TimeLeft > 150f && Time.realtimeSinceStartup < timeout) yield return null;
        Check(stand.PhaseName == "BLOOD RAIN" && sun.intensity > .7f, "lightning lights the ruins at 2:30");
        Capture("last_stand_02_lightning");
        yield return Wait(3.2f);
        Check(stand.Radius < initialRadius * .5f && stormRain.isPlaying &&
            FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a => a.clip != null && a.clip.name == "last_stand_rain"),
            "midpoint tightens the front and starts blood rain and storm audio");
        if (camera != null)
            CaptureMiasma("last_stand_02_village_front", camera, stand.Center + new Vector3(30f, 24f, -62f), stand.Center);

        foreach (var enemy in EnemyController.All.ToArray()) if (enemy != null) Destroy(enemy.gameObject);
        body.isKinematic = true;
        body.position = player.transform.position = stand.Center + Vector3.right * (stand.Radius + 8f);
        float hp = health.currentHealth;
        yield return Wait(.55f);
        Check(health.currentHealth < hp && stand.Clearance < 0f, "exposure drains health through dodge immunity");
        body.position = player.transform.position = playerStart;
        hp = health.currentHealth;
        yield return Wait(.48f);
        Check(health.currentHealth == hp && stand.Clearance > 0f, "reaching safe ground ends exposure");

        yield return ReviewNamedGrave(gm, stand, health, countdown);
        body.position = player.transform.position = playerStart;
        countdown.SetValue(gm, 60.035f);
        typeof(GameManager).GetField("nextBoonTime", fields).SetValue(gm, 240f);
        yield return Wait(.65f);
        float surgeRadius = stand.Radius;
        Check(stand.IsSurging && surgeRadius < 24f &&
            FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a => a.clip != null && a.clip.name == "music_last_stand"),
            "the final minute closes the courtyard and starts its own survival score");
        Check(GameHUD.BoonsOpen && CombatTime.GameplayScale == 0f, "the minute-four boon freezes the final countdown");
        if (GameHUD.BoonsOpen) GameHUD.DebugPickBoon(0);
        yield return null;
        Capture("last_stand_03_front");
        Vector3 aboveCourt = stand.Center + Vector3.up * 8f;
        body.position = player.transform.position = aboveCourt;
        hp = health.currentHealth;
        yield return Wait(.5f);
        Check(health.currentHealth < hp && stand.Clearance < 0f, "the closing front also reaches above the courtyard");
        body.position = player.transform.position = playerStart;
        health.currentHealth = health.maxHealth;

        countdown.SetValue(gm, 2.3f);
        yield return null;
        var spawner = ZombieSpawner.Instance;
        var warlord = spawner.Spawn(ZombieType.Warlord, true);
        var bloater = spawner.Spawn(ZombieType.Bloater, false);
        Check(stand.Radius < 12f && warlord != null && bloater != null, "boss and horde still spawn on reachable ground at the final radius");
        yield return Wait(1.45f);
        var path = new NavMeshPath();
        if (warlord != null)
            Check(NavMesh.CalculatePath(warlord.transform.position, stand.Center, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete, "final spawns can actually reach the courtyard");
        if (bloater != null) bloater.TakeDamage(999f, Vector3.forward, 0f);
        int kills = gm.currentKills;
        int score = gm.Score;
        float stormDensity = RenderSettings.fogDensity;
        countdown.SetValue(gm, .02f);
        timeout = Time.realtimeSinceStartup + 2f;
        while (gm.State == GameManager.MatchState.Playing && Time.realtimeSinceStartup < timeout) yield return null;
        float dawnStart = Time.realtimeSinceStartup;
        yield return AtDawn(.65f);
        Check(gm.State == GameManager.MatchState.Dawn && gm.TimeLeft == 0f &&
            Mathf.Abs(CombatTime.GameplayScale - .3f) < .001f && EnemyController.All.Count > 0,
            "dawn runs in slow motion before resolving the match");
        Check(Mathf.Abs(RenderSettings.fogDensity - stormDensity) < .0001f && stormRain.isEmitting,
            "the horn starts under the existing storm and mist");
        Check(HearCue("wave_horn", spatial: true), "dawn plays the recorded trumpet at its original pitch from the hills");
        gm.Pause();
        gm.GameOver();
        Check(gm.State == GameManager.MatchState.Dawn && !gm.IsPaused, "the finale retains control of its sequence");
        yield return AtDawn(2.53f);
        var volley = gm.transform.Find("Royal arrow volley");
        Check(volley != null && volley.GetComponentsInChildren<TrailRenderer>()
            .Any(trail => trail.emitting && trail.sharedMaterial != null &&
                trail.sharedMaterial.shader.name == "Medieval Warrior/Last Stand/Royal Fire"),
            "the slow-motion volley renders live emissive trails");
        var hud = FindFirstObjectByType<GameHUD>();
        Check(hud != null && hud.GetComponentsInChildren<CanvasGroup>()
            .Any(group => group.name == "Combat HUD" && group.alpha == 0f), "the combat HUD fades out over the first rank");
        Capture("last_stand_04_royal_volley");
        yield return AtDawn(4.45f);
        Check(RenderSettings.fogDensity < stormDensity * .86f && RenderSettings.fogDensity > .004f &&
            stormRain.emission.rateOverTime.constant < 600f, "fog and rain are halfway through clearing during the volley");
        Capture("last_stand_05_mist_receding");
        yield return AtDawn(5.7f);
        Check(EnemyController.All.Count == 0 && gm.currentKills == kills && gm.Score == score &&
            health.currentHealth == health.maxHealth, "the royal volley clears every enemy without awarding player kills or allowing a late hit");
        yield return AtDawn(8.85f);
        Check(gm.State == GameManager.MatchState.Dawn && Quaternion.Angle(sun.transform.rotation, midnightRotation) > 30f &&
            sun.intensity > 1.5f && RenderSettings.fogDensity < .0016f && !stormRain.isEmitting,
            "the sun rises and the rain clears before the result");
        Capture("last_stand_06_dawn");
        yield return AtDawn(LastStandDirector.DawnDuration + .25f);
        Check(gm.State == GameManager.MatchState.Won && GameHUD.BoonsOpen == false, "Last Stand shows results after the dawn sequence");
        Check(HearCue("victory"), "the results play the recorded victory fanfare");

        gm.RestartGame();
        yield return Wait(5f);
        gm = GameManager.instance;
        Check(gm != null && gm.State == GameManager.MatchState.Playing && gm.LastStand.Radius > 120f
            && SceneManager.GetSceneByName("Background").handle == world, "replaying restores midnight and the starting front in the same world");
        if (gm == null || gm.State != GameManager.MatchState.Playing) yield break;
        gm.LastStand.GraveSelected = false;
        typeof(GameManager).GetField("nextBoonTime", fields).SetValue(gm, 999f);
        countdown.SetValue(gm, 119.8f);
        yield return Wait(.4f);
        Check(!gm.LastStand.GraveActive && gm.Boss != null && gm.Boss.type == ZombieType.Warlord,
            "an ordinary Last Stand keeps the Warlord at three minutes");
        health = GameObject.FindWithTag("Player").GetComponent<PlayerHealth>();
        countdown.SetValue(gm, .12f);
        health.TakeDamage(999f, null, 0f, true);
        yield return Wait(2.45f);
        Check(gm.State == GameManager.MatchState.Lost && gm.transform.Find("Royal arrow volley") == null,
            "dying just before dawn ends in defeat");
        Check(HearCue("defeat"), "defeat plays its recorded trumpet cue");
        gm.QuitGame();
        yield return Wait(2f);
        Check(GameManager.instance == null && RenderSettings.skybox.shader.name == "Skybox/Cubemap" &&
            FindFirstObjectByType<LastStandMiasma>() == null, "returning to the menu removes the match weather");

        IEnumerator AtDawn(float seconds)
        {
            while (Time.realtimeSinceStartup < dawnStart + seconds) yield return null;
        }
    }

    IEnumerator ReviewNamedGrave(GameManager gm, LastStandDirector stand, PlayerHealth knight, FieldInfo countdown)
    {
        var nextBoon = typeof(GameManager).GetField("nextBoonTime", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var enemy in EnemyController.All.ToArray()) if (enemy != null) Destroy(enemy.gameObject);
        yield return null;
        var body = knight.GetComponent<Rigidbody>();
        var control = knight.GetComponent<PlayerControlSpeed>();
        var camera = Camera.main;
        var orbit = camera.GetComponent<CameraFollow>();
        float immunity = knight.InvulnerableUntil;
        bool originallyKinematic = body.isKinematic;
        knight.InvulnerableUntil = 0f;
        body.isKinematic = false;
        countdown.SetValue(gm, 120.15f);
        nextBoon.SetValue(gm, 180f);
        Check(!stand.GraveActive && !stand.RevealingGrave, "the grave stays buried until the three-minute mark");
        float deadline = Time.realtimeSinceStartup + 12f;
        while (!stand.RevealingGrave && Time.realtimeSinceStartup < deadline) yield return null;
        var grave = stand.GetComponentInChildren<LastStandGrave>();
        Check(grave != null && stand.GraveActive && !GameHUD.BoonsOpen && gm.Boss == null,
            "the selected run raises its grave automatically and reserves the Warlord and boon reveal");
        if (grave == null)
        {
            body.isKinematic = originallyKinematic;
            knight.InvulnerableUntil = immunity;
            if (GameHUD.BoonsOpen) GameHUD.DebugPickBoon(0);
            nextBoon.SetValue(gm, 999f);
            yield break;
        }
        nextBoon.SetValue(gm, 999f);
        float health = knight.currentHealth;
        knight.TakeDamage(10f, null, 0f, true);
        Check(!control.enabled && body.isKinematic && knight.currentHealth == health,
            "the camera owns the reveal and the knight is safe while input is locked");
        var slab = grave.transform.Find("Carved headstone");
        Check(slab != null && slab.localPosition.y < -1f, "the solid grave rises from beneath the terrain");
        gm.Pause();
        float age = grave.Age;
        Vector3 cameraAtPause = camera.transform.position;
        yield return Wait(.32f);
        Check(grave.Age == age && camera.transform.position == cameraAtPause, "pause holds the grave and camera on their frame");
        gm.Resume();
        yield return Until(2.35f);
        var letters = slab != null ? slab.Find("Engraved name")?.GetComponent<TMPro.TextMeshPro>() : null;
        Vector3 viewport = letters != null ? camera.WorldToViewportPoint(letters.transform.position) : Vector3.zero;
        Check(letters != null && letters.text == GameSettings.HeroName && !letters.richText &&
            Mathf.Abs(viewport.x-.5f) < .15f && Mathf.Abs(viewport.y-.5f) < .15f && viewport.z > 0f &&
            camera.fieldOfView < GameSettings.FieldOfView && Mathf.Abs(slab.localPosition.y) < .01f,
            "the camera zooms to the knight's literal name on the raised stone");
        Capture("last_stand_grave_01_inscription");
        yield return Until(4.95f);
        var duelist = grave.Duelist;
        var path = new NavMeshPath();
        Check(duelist != null && stand.RevealingGrave && !duelist.IsDefeated &&
            NavMesh.SamplePosition(knight.transform.position, out var foot, 3f, NavMesh.AllAreas) &&
            NavMesh.CalculatePath(duelist.transform.position, foot.position, NavMesh.AllAreas, path) &&
            path.status == NavMeshPathStatus.PathComplete, "the shadow emerges beside its grave on a complete path to the knight");
        Capture("last_stand_grave_02_emergence");
        yield return Until(7.05f);
        Check(!stand.RevealingGrave && stand.GraveActive && !orbit.FramingGrave && control.enabled &&
            !body.isKinematic && knight.InvulnerableUntil > Time.time && !float.IsInfinity(knight.InvulnerableUntil) &&
            Mathf.Abs(camera.fieldOfView - GameSettings.FieldOfView) < .1f &&
            Vector3.Distance(camera.transform.position, knight.transform.position) < orbit.maxZoom + 3f,
            "the camera and controls return to the knight before the duel");
        body.isKinematic = originallyKinematic;
        knight.InvulnerableUntil = immunity;
        if (duelist != null)
        {
            // Isolate the damage measurements from unrelated horde attacks.
            var spawns = typeof(GameManager).GetField("spawnTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            float previousSpawnTimer = (float)spawns.GetValue(gm);
            spawns.SetValue(gm, 999f);
            try { yield return ReviewGraveCombat(duelist, knight); }
            finally { spawns.SetValue(gm, previousSpawnTimer); }
        }
        Check(!stand.GraveActive && stand.GetComponentsInChildren<LastStandGrave>().Length == 1,
            "the slain double releases the encounter and the grave cannot trigger twice");

        IEnumerator Until(float targetAge)
        {
            float limit = Time.realtimeSinceStartup + 15f;
            while (grave != null && grave.Revealing && grave.Age < targetAge && Time.realtimeSinceStartup < limit) yield return null;
            // Wait for CameraFollow.LateUpdate to display the sampled director pose.
            yield return null;
        }
    }

    static bool HearCue(string name, bool spatial = false)
    {
        var manager = FindFirstObjectByType<AudioManager>();
        return manager != null && manager.GetComponentsInChildren<AudioSource>().Any(source =>
            source.isPlaying && source.clip != null && source.clip.name == name &&
            Mathf.Approximately(source.pitch, 1f) && (source.spatialBlend > 0f) == spatial);
    }

    void CaptureMiasma(string name, Camera camera, Vector3 position, Vector3 courtyard)
    {
        var transform = camera.transform;
        Vector3 previousPosition = transform.position;
        Quaternion previousRotation = transform.rotation;
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(canvas => canvas.enabled).ToArray();
        foreach (var canvas in canvases) canvas.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(courtyard - position));
        try { Capture(name); }
        finally
        {
            transform.SetPositionAndRotation(previousPosition, previousRotation);
            foreach (var canvas in canvases) canvas.enabled = true;
        }
    }
}
#endif
