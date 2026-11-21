using UnityEngine;
using UnityEngine.AI;

// Spawns zombies anywhere on the map around the knight: random NavMesh points in a ring,
// preferring spots the camera can't see and that have a real path to the player.
// GameManager decides *what* and *when*; this decides *where*.
public class ZombieSpawner : MonoBehaviour
{
    public GameObject zombiePrefab;      // The Blueprint we want to copy
    public Transform spawnPoint;         // Fallback spawn location
    public Transform[] extraSpawnPoints; // More fallbacks (road ends, gates)
    public int maxZombies = 3;           // Only used when no GameManager drives the spawner
    public float spawnInterval = 2.0f;
    public float minSpawnDistance = 18f;
    public float maxSpawnDistance = 46f;

    public static ZombieSpawner Instance { get; private set; }

    private float timer;

    void Awake()
    {
        Instance=this;
        var revenant=BattleLoading.ZombiePrefab;
        if(revenant==null)revenant=Resources.Load<GameObject>("Revenants/Revenant");
        if(revenant!=null)zombiePrefab=revenant;
    }

    void Update()
    {
        if (GameManager.instance != null) return; // game modes control spawning

        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0;
            if (EnemyController.All.Count < maxZombies) Spawn(ZombieType.Walker, false);
        }
    }

    public EnemyController Spawn(ZombieType type, bool boss)
    {
        using var timing=BattleLoadMetrics.Measure(BattleLoadMetrics.Enabled?"Zombie spawn "+type:null);
        if (zombiePrefab == null) return null;
        var player = GameObject.FindWithTag("Player");
        Vector3 center = player != null ? player.transform.position : transform.position;
        float min = boss ? 24f : minSpawnDistance;

        Vector3 position;
        var stand = GameManager.instance != null ? GameManager.instance.LastStand : null;
        if (stand != null && stand.Radius < 54f)
        {
            if (!TryFindCourtyardApproach(stand, center, boss, out position)) return null;
        }
        else if (!TryFindSpawnPoint(center, min, maxSpawnDistance, out position)) position = Fallback();

        Quaternion facing = Quaternion.LookRotation(Flat(center - position).sqrMagnitude > 0.01f ? Flat(center - position) : Vector3.forward);
        var go = Instantiate(zombiePrefab, position, facing);
        go.name = (boss ? "Boss " : "") + type;
        var enemy = go.GetComponent<EnemyController>();
        if (enemy != null) enemy.Init(type, boss, true);
        return enemy;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

    static bool TryFindCourtyardApproach(LastStandDirector stand, Vector3 player, bool boss, out Vector3 result)
    {
        result = default;
        if (!NavMesh.SamplePosition(player, out var target, 5f, NavMesh.AllAreas)) return false;

        var camera = Camera.main;
        var path = new NavMeshPath();
        float angle = Random.Range(0f, 360f);
        float minDistance = boss ? 12f : stand.IsSurging ? 7f : 10f;
        bool haveVisible = false;
        for (int i = 0; i < 24; i++)
        {
            // Search around the closing front, including approaches just inside it.
            Vector3 direction = Quaternion.Euler(0f, angle + i * 137.5f, 0f) * Vector3.forward;
            float distance = Random.Range(stand.Radius - 5f, stand.Radius + 7f);
            Vector3 guess = stand.Center + direction * distance + Vector3.up;
            if (!NavMesh.SamplePosition(guess, out var hit, 4f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(hit.position.y - stand.Center.y) > 4f) continue;
            if (Flat(hit.position - player).sqrMagnitude < minDistance * minDistance) continue;
            if (!NavMesh.CalculatePath(hit.position, target.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete) continue;

            if (!IsInView(camera, hit.position))
            {
                result = hit.position;
                return true;
            }
            if (!haveVisible)
            {
                result = hit.position;
                haveVisible = true;
            }
        }
        return haveVisible;
    }

    Vector3 Fallback()
    {
        Transform point = spawnPoint != null ? spawnPoint : transform;
        if (extraSpawnPoints != null && extraSpawnPoints.Length > 0)
        {
            int pick = Random.Range(0, extraSpawnPoints.Length + 1);
            if (pick > 0 && extraSpawnPoints[pick - 1] != null) point = extraSpawnPoints[pick - 1];
        }
        Vector3 p = point.position;
        if (NavMesh.SamplePosition(p, out NavMeshHit hit, 8f, NavMesh.AllAreas)) p = hit.position;
        return p;
    }

    public static bool TryFindSpawnPoint(Vector3 around, float minDist, float maxDist, out Vector3 result)
    {
        result = around;
        var cam = Camera.main;
        NavMeshHit target;
        if (!NavMesh.SamplePosition(around, out target, 5f, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        Vector3 visibleBackup = Vector3.zero;
        bool haveVisible = false;

        for (int i = 0; i < 30; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float r = Random.Range(minDist, maxDist);
            Vector3 guess = around + new Vector3(dir.x, 0, dir.y) * r;
            if (!NavMesh.SamplePosition(guess + Vector3.up * 10f, out NavMeshHit hit, 14f, NavMesh.AllAreas)) continue;
            if (Vector3.Distance(Flat(hit.position), Flat(around)) < minDist * 0.8f) continue;
            if (!NavMesh.CalculatePath(hit.position, target.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;

            if (!IsInView(cam, hit.position)) { result = hit.position; return true; }
            if (!haveVisible) { visibleBackup = hit.position; haveVisible = true; }
        }
        if (haveVisible) { result = visibleBackup; return true; }  // rising from the ground in view is fine too
        return false;
    }

    static bool IsInView(Camera camera, Vector3 position)
    {
        if (camera == null) return false;
        Vector3 vp = camera.WorldToViewportPoint(position + Vector3.up);
        return vp.z > 0f && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
    }
}
