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

    void Awake() => Instance = this;

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
        if (zombiePrefab == null) return null;
        var player = GameObject.FindWithTag("Player");
        Vector3 center = player != null ? player.transform.position : transform.position;
        float min = boss ? 24f : minSpawnDistance;

        Vector3 position;
        if (!TryFindSpawnPoint(center, min, maxSpawnDistance, out position)) position = Fallback();

        Quaternion facing = Quaternion.LookRotation(Flat(center - position).sqrMagnitude > 0.01f ? Flat(center - position) : Vector3.forward);
        var go = Instantiate(zombiePrefab, position, facing);
        go.name = (boss ? "Boss " : "") + type;
        var enemy = go.GetComponent<EnemyController>();
        if (enemy != null) enemy.Init(type, boss, true);
        return enemy;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

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

            bool visible = false;
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(hit.position + Vector3.up);
                visible = vp.z > 0 && vp.x > -0.05f && vp.x < 1.05f && vp.y > -0.05f && vp.y < 1.05f;
            }
            if (!visible) { result = hit.position; return true; }
            if (!haveVisible) { visibleBackup = hit.position; haveVisible = true; }
        }
        if (haveVisible) { result = visibleBackup; return true; }  // rising from the ground in view is fine too
        return false;
    }
}
