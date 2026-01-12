using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    public GameObject zombiePrefab;   // The Blueprint we want to copy
    public Transform spawnPoint;      // Where to put them
    public int maxZombies = 3;        // Limit the crowd
    public float spawnInterval = 2.0f;// How often to check

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;

        // Every 2 seconds, check if we need more zombies
        if (timer >= spawnInterval)
        {
            CheckAndSpawn();
            timer = 0;
        }
    }

    void CheckAndSpawn()
    {
        // Count how many objects with the "Enemy" tag are currently in the scene
        int currentZombies = GameObject.FindGameObjectsWithTag("Enemy").Length;

        // If we have fewer than the max, make a new one!
        if (currentZombies < maxZombies)
        {
            SpawnZombie();
        }
    }

    void SpawnZombie()
    {
        // instantiate means "Create a Copy"
        // 1. What to copy (zombiePrefab)
        // 2. Where to put it (spawnPoint position)
        // 3. Which way to face (spawnPoint rotation)
        Instantiate(zombiePrefab, spawnPoint.position, spawnPoint.rotation);
    }
}