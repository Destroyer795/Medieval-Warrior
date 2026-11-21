using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public sealed class LastStandDirector : MonoBehaviour
{
    public const float Duration = 300f;
    public const float StormSeconds = 150f;
    public const float SurgeSeconds = 60f;
    public const string VictoryLine = "YOU HELD UNTIL DAWN.";

    const float StartingRadius = 132f;
    const float CourtyardRadius = 11f;
    const float ExposureInterval = .4f;
    internal const float DawnDuration = 10.9f;
    const float DawnBannerAt = 8.15f;

    PlayerHealth player;
    LastStandWeather weather;
    float exposureTime, exposureDamage;
    bool stormStarted;

    public Vector3 Center { get; private set; }
    public float Radius { get; private set; }
    public float Ceiling { get; private set; }
    public float Clearance => player != null ? ClearanceAt(player.transform.position) : Radius;
    public bool IsSurging { get; private set; }
    public string PhaseName => IsSurging ? "FINAL ASSAULT" : stormStarted ? "BLOOD RAIN" : "MIDNIGHT";

    internal void Prepare(PlayerHealth knight)
    {
        player = knight;
        // The environment builder places the knight on open ground in the courtyard.
        Center = knight != null ? knight.transform.position : transform.position;
        if (NavMesh.SamplePosition(Center, out var ground, 3f, NavMesh.AllAreas)) Center = ground.position;
        SetBounds(Duration);
        weather = new LastStandWeather(transform, Camera.main);
        weather.Tick(Center, Radius, Ceiling, Center, 0f);
        AudioManager.PlayMusic("ambient_last_stand", .8f);
    }

    internal void Tick(float timeLeft, float deltaTime)
    {
        SetBounds(timeLeft);
        Vector3 position = player != null ? player.transform.position : Center;
        weather.Tick(Center, Radius, Ceiling, position, Duration - timeLeft);

        if (!stormStarted && timeLeft <= StormSeconds)
        {
            stormStarted = true;
            GameHUD.Banner("BLOOD RAIN", "The corruption is closing on the courtyard.", 3f);
            if (timeLeft > SurgeSeconds) AudioManager.PlayMusic("last_stand_rain", .85f);
        }
        if (!IsSurging && timeLeft <= SurgeSeconds)
        {
            IsSurging = true;
            GameHUD.Banner("THE FINAL MINUTE", "Hold the courtyard. Dawn is close.", 3.5f);
            AudioManager.SetBattleIntensity(0f, true);
            AudioManager.PlayMusic("music_last_stand", .95f);
        }

        if (player == null || player.IsDead) return;
        float clearance = ClearanceAt(position);
        if (clearance >= 0f)
        {
            exposureTime = exposureDamage = 0f;
            return;
        }

        exposureTime += deltaTime;
        exposureDamage += Mathf.Lerp(7f, 23f, Mathf.InverseLerp(0f, 9f, -clearance)) * deltaTime;
        if (exposureTime < ExposureInterval) return;
        player.TakeCorruption(exposureDamage);
        exposureTime = exposureDamage = 0f;
    }

    void SetBounds(float timeLeft)
    {
        float remaining = Mathf.Clamp01(timeLeft / Duration);
        Radius = Mathf.Lerp(CourtyardRadius, StartingRadius, Mathf.Pow(remaining, 1.5f));
        // Close off high roofs and battlements as the front reaches the keep.
        Ceiling = Center.y + Mathf.SmoothStep(3.2f, 28f, Mathf.InverseLerp(.25f, .78f, remaining));
    }

    float ClearanceAt(Vector3 position)
    {
        Vector2 offset = new Vector2(position.x - Center.x, position.z - Center.z);
        float clearance = Radius - offset.magnitude;
        return position.y <= Ceiling ? clearance : Mathf.Min(clearance, Ceiling - position.y);
    }

    internal IEnumerator PlayDawn()
    {
        Vector3 position = player != null ? player.transform.position : Center;
        if (player != null)
        {
            player.InvulnerableUntil = float.PositiveInfinity;
            if (player.TryGetComponent<PlayerControlSpeed>(out var control)) control.enabled = false;
            if (player.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
                body.linearVelocity = Vector3.Project(body.linearVelocity, Vector3.up);
        }

        CombatTime.SetScale(.3f);
        GameHUD.BeginDawn();
        AudioManager.StopMusic();
        AudioManager.PlayAt("wave_horn", Center + LastStandWeather.SunriseDirection * 105f, 1f, 0f,
            1f, 7400f, 18, .78f, 90f, 260f);
        var camera = Camera.main;
        var orbit = camera != null ? camera.GetComponent<CameraFollow>() : null;
        var volley = new LastStandVolley(transform, position, camera);
        bool bannerShown = false;
        float elapsed = 0f;
        while (elapsed < DawnDuration)
        {
            weather.Dawn(elapsed);
            if (orbit != null) orbit.DawnView = Mathf.SmoothStep(0f, 1f, elapsed / 3.6f);
            volley.Tick(elapsed);
            if (!bannerShown && elapsed >= DawnBannerAt)
            {
                bannerShown = true;
                GameHUD.Banner(VictoryLine, "The royal host has arrived.", DawnDuration - DawnBannerAt + .25f);
            }
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        weather.Dawn(DawnDuration);
        volley.Tick(DawnDuration);
    }

    void OnDestroy()
    {
        weather?.Dispose();
    }
}
