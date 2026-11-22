using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class PillSpawner : MonoBehaviour
{
    [Serializable]
    public class PillType
    {
        public string name = "Pill";
        public GameObject prefab;
        [Min(0f)] public float weight = 25f; // relative chance, higher = more common
    }

    [Header("Pill Types")]
    [SerializeField] private List<PillType> pillTypes = new List<PillType>();

    [Header("Spawning")]
    [SerializeField] private string spawnPointName = "pillBottleSpawn";
    [SerializeField] private Vector2 intervalRange = new Vector2(20, 60);
    [SerializeField] private int maxActivePills = 5;
    [SerializeField] private float occupiedRadius = 0.3f;

    private readonly List<Transform> spawnPoints = new List<Transform>();
    private readonly List<GameObject> activePills = new List<GameObject>();
    private float timer;

    private void Start()
    {
        foreach (Transform child in transform)
        {
            Transform point = child.Find(spawnPointName);
            if (point != null) spawnPoints.Add(point);
        }
        ResetTimer();
    }

    private void Update()
    {
        activePills.RemoveAll(p => p == null);
        
        int cap = Mathf.RoundToInt(UpgradeManager.Get("maxPills", maxActivePills));

        if (activePills.Count >= cap)
        {
            ResetTimer();
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        Transform point = GetFreeSpawnPoint();
        GameObject prefab = PickPrefab();
        if (point != null && prefab != null)
            activePills.Add(Instantiate(prefab, point.position, point.rotation));

        ResetTimer();
        
    }

    private GameObject PickPrefab()
    {
        float total = 0f;
        foreach (PillType t in pillTypes)
            if (t.prefab != null) total += t.weight;

        if (total <= 0f) return null;

        float roll = Random.value * total;
        foreach (PillType t in pillTypes)
        {
            if (t.prefab == null) continue;
            roll -= t.weight;
            if (roll <= 0f) return t.prefab;
        }
        
        return null;
    }

    private void ResetTimer()
    {
        float scale = Mathf.Max(0.1f, UpgradeManager.Get("pillInterval", 1f));   // 1 = normal, 0.8 = 20% faster
        timer = Random.Range(intervalRange.x, intervalRange.y) * scale;
        //Debug.Log($"[PillSpawner] next pill in {timer:F1}s (scale = {scale})");
    }

    private Transform GetFreeSpawnPoint()
    {
        var free = new List<Transform>();
        foreach (Transform point in spawnPoints)
        {
            bool taken = false;
            foreach (GameObject pill in activePills)
            {
                if (Vector3.Distance(pill.transform.position, point.position) < occupiedRadius)
                {
                    taken = true;
                    break;
                }
            }
            if (!taken) free.Add(point);
        }
        return free.Count > 0 ? free[Random.Range(0, free.Count)] : null;
    }
}