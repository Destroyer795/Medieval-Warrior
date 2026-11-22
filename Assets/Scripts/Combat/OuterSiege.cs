using System.Collections.Generic;
using UnityEngine;

// Owns the playable wall and the short ground route joining its door to the baked village.
public sealed class OuterSiege : MonoBehaviour
{
    public ForegateStructure Structure { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public float Integrity => MaxHealth > 0 ? Health / MaxHealth : 0;
    public bool Breached => Health <= 0;
    public bool Cinematic { get; private set; }
    public float HoldSeconds { get; private set; }
    public bool StoneReady => Time.time >= stoneReadyAt;
    internal bool PassageClear => Breached && (!Cinematic || breachTime >= .28f);

    GameManager match;
    PlayerControlSpeed knight;
    CameraFollow cameraRig;
    Terrain terrain;
    Vector3 handoff, stoneHome;
    Quaternion stoneRotation, cradleRotation;
    readonly List<EnemyController> victims = new List<EnemyController>();
    // Four abreast through the opening. A killed front rank makes room for the next.
    readonly List<EnemyController>[] ranks =
    {
        new List<EnemyController>(), new List<EnemyController>(),
        new List<EnemyController>(), new List<EnemyController>()
    };
    int deployment;
    float stoneReadyAt, stoneFall = -1, breachTime, nextImpact;
    bool restoreControl;

    public static OuterSiege Create(GameManager match, PlayerHealth health)
    {
        var outer = match.gameObject.AddComponent<OuterSiege>();
        outer.match = match;
        outer.knight = health.GetComponent<PlayerControlSpeed>();
        outer.cameraRig = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        outer.terrain = Terrain.activeTerrain;
        outer.Structure = ForegateStructure.Build();
        if (outer.Structure == null)
        {
            Destroy(outer);
            return null;
        }
        outer.Structure.transform.SetParent(match.transform, true);
        outer.MaxHealth = GameSettings.Difficulty switch
        {
            Difficulty.Squire => 480, Difficulty.Warlord => 310, _ => 400
        };
        outer.Health = outer.MaxHealth;
        outer.handoff = match.Gate.Road;
        for (int depth = 20; depth <= 36; depth += 2)
        {
            Vector3 probe = outer.Ground(outer.Structure.World(0, 0, depth));
            if (!match.Gate.TryReach(match.Gate.Approach, probe, 2f, out var entry)) continue;
            outer.handoff = entry;
            break;
        }
        outer.stoneHome = outer.Structure.ReadyStone.localPosition;
        outer.stoneRotation = outer.Structure.ReadyStone.localRotation;
        outer.cradleRotation = outer.Structure.Cradle.localRotation;
        Vector3 start = outer.Structure.World(9, 6.45f, -.10f);
        Vector3 heading = Vector3.ProjectOnPlane(outer.Structure.World(0, 0, -12) - start, Vector3.up);
        Quaternion facing = Quaternion.LookRotation(heading);
        health.transform.SetPositionAndRotation(start, facing);
        if (health.TryGetComponent<Rigidbody>(out var body))
        {
            body.position = start;
            body.rotation = facing;
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
        }
        outer.knight.Equip(Weapon.Bow);
        outer.cameraRig?.FaceGraveFromKnight(start + heading);
        Physics.SyncTransforms();
        return outer;
    }

    internal float GroundHeight(Vector3 at) => terrain != null
        ? terrain.SampleHeight(at) + terrain.transform.position.y + .055f : Structure.World(0, 0, 0).y;

    Vector3 Ground(Vector3 at)
    {
        at.y = GroundHeight(at);
        return at;
    }

    internal EnemyController Deploy(ZombieSpawner spawner, ZombieType type, bool boss)
    {
        // Instantiate on the connected island, then detach the agent before the first frame.
        var enemy = spawner.SpawnAt(type, boss, handoff, match.Gate.Approach, false);
        if (enemy == null) return null;
        int index = deployment++;
        int lane = index % ranks.Length;
        float slot = LaneOffset(lane);
        var start = Ground(Structure.World(slot * 1.4f, 0, -12 - index % 4 * 2.2f));
        if (!Breached) ranks[lane].Add(enemy);
        enemy.SetOuterOrder(this, start, lane);
        return enemy;
    }

    static float LaneOffset(int lane) => (lane - 1.5f) * .93f;

    internal bool AtFront(EnemyController enemy, int lane) => ranks[lane].Count > 0 && ranks[lane][0] == enemy;

    internal Vector3 MarchPoint(EnemyController enemy, int lane, int stage)
    {
        float slot = LaneOffset(lane);
        if (stage < 0)
        {
            var queue = ranks[lane];
            queue.RemoveAll(member => member == null || member.IsDead);
            return Ground(Structure.World(slot, 0, -1f - Mathf.Max(0, queue.IndexOf(enemy)) * 1.25f));
        }
        return stage switch
        {
            0 => Ground(Structure.World(slot * .76f, 0, 2.9f)),
            1 => Ground(Structure.World(slot * .6f, 0, 14)),
            _ => handoff
        };
    }

    bool Active => match != null && match.State == GameManager.MatchState.Playing &&
        !match.IsPaused && !match.ChoosingBoon && Time.timeScale > 0;

    public void TakeDamage(float amount)
    {
        if (!Active || Breached || amount <= 0) return;
        Health = Mathf.Max(0, Health - amount);
        if (Time.time >= nextImpact || Breached)
        {
            var contact = Structure.World(0, 1.4f, -.3f);
            AudioManager.GateImpact(contact, Breached);
            FX.Splinters(contact, Structure.Outward);
            nextImpact = Time.time + .4f;
        }
        if (!Breached) return;
        Cinematic = true;
        breachTime = 0;
        restoreControl = knight.enabled;
        knight.enabled = false;
        if (knight.TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
            body.linearVelocity = Vector3.up * body.linearVelocity.y;
        FrameBreach();
    }

    void FrameBreach()
    {
        Vector3 focus = Structure.World(0, 1.9f, -.3f);
        Vector3 eye = Structure.World(1.35f, 3.15f, 9.4f - .6f * breachTime);
        cameraRig?.FrameSiege(focus, eye);
    }

    internal void EndCinematic()
    {
        if (!Cinematic) return;
        Cinematic = false;
        Structure.SetOpen(1);
        cameraRig?.EndSiegeShot();
        if (restoreControl && knight != null) knight.enabled = true;
    }

    void Update()
    {
        if (!Active) return;
        if (!Breached) HoldSeconds += Time.deltaTime;
        if (Cinematic)
        {
            breachTime += Time.unscaledDeltaTime;
            Structure.SetOpen(Mathf.SmoothStep(0, 1, Mathf.Clamp01(breachTime / .65f)));
            FrameBreach();
            if (breachTime >= 1)
            {
                EndCinematic();
                GameHUD.Banner("THE OUTER GATE HAS FALLEN", "Down the stairs! Hold the Keep sanctuary.", 2.8f);
            }
        }
        UpdateStone();
        if (!Cinematic && GameInput.SiegeUseDown) Interact();
    }

    bool At(float x, float y, float inward, float reach) =>
        (knight.transform.position - Structure.World(x, y, inward)).sqrMagnitude < reach * reach;
    bool AtArmory => At(-11.3f, 6.54f, .47f, 2.4f) || At(11.3f, 6.54f, .47f, 2.4f);
    bool AtCradle => At(-.9f, 6.85f, .1f, 2.2f);

    public string InteractionPrompt
    {
        get
        {
            if (!Active || Cinematic) return "";
            string key = GameInput.UsingGamepad ? "B" : "R";
            if (AtCradle) return StoneReady ? $"{key}  Drop stone" : $"Stone ready in {Mathf.CeilToInt(stoneReadyAt - Time.time)}s";
            if (AtArmory) return knight.Arrows < knight.maxArrows ? $"{key}  Refill quiver" : "Quiver full";
            return "";
        }
    }

    public bool Interact()
    {
        if (!Active || Cinematic) return false;
        if (AtArmory)
        {
            if (knight.AddArrows(knight.maxArrows) <= 0) return false;
            AudioManager.PlayAt("pickup", knight.transform.position);
            return true;
        }
        if (!AtCradle || !StoneReady) return false;
        stoneFall = 0;
        stoneReadyAt = Time.time + 8;
        AudioManager.PlayAt("keep_impact", Structure.Cradle.position, .6f);
        return true;
    }

    void UpdateStone()
    {
        var stone = Structure.ReadyStone;
        if (stoneFall < 0)
        {
            if (StoneReady && !stone.gameObject.activeSelf)
            {
                stone.localPosition = stoneHome;
                stone.localRotation = stoneRotation;
                stone.gameObject.SetActive(true);
            }
            return;
        }
        stoneFall += Time.deltaTime;
        Structure.Cradle.localRotation = cradleRotation * Quaternion.AngleAxis(-64 *
            Mathf.Sin(Mathf.PI * Mathf.Min(1, stoneFall / .8f)), Vector3.forward);
        Vector3 lip = Structure.World(0, 7.3f, -1.3f);
        Vector3 impact = Ground(lip) + Vector3.up * .35f;
        float flight = Mathf.Clamp01((stoneFall - .23f) / .87f);
        if (stoneFall <= .23f)
            stone.position = Vector3.Lerp(stone.parent.TransformPoint(stoneHome), lip, stoneFall / .23f);
        else stone.position = Vector3.Lerp(lip, impact, flight * flight);
        stone.Rotate(120 * Time.deltaTime, 0, 65 * Time.deltaTime);
        if (flight < 1) return;
        stoneFall = -1;
        stone.gameObject.SetActive(false);
        FX.Dust(impact, 2.5f, 46);
        FX.Shake(.36f);
        AudioManager.PlayAt("keep_impact", impact);
        victims.Clear();
        victims.AddRange(EnemyController.All);
        foreach (var enemy in victims)
            if (enemy != null && !enemy.IsDead && (enemy.transform.position - impact).sqrMagnitude < 3.4f * 3.4f)
                enemy.TakeDamage(22, Vector3.down, 0);
    }
}
