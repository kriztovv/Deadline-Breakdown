using System.Collections.Generic;
using UnityEngine;

public class minigameSpawner : MonoBehaviour
{
    [Header("What to spawn")]
    [SerializeField] private GameObject[] prefabs;

    [Header("Where (this object's Y is the spawn height)")]
    [SerializeField] private Vector2 xRange = new Vector2(-4f, 4f);   // left and right limit, relative to this object's X

    [Header("When")]
    [SerializeField] public float startInterval = 2f;   // seconds between spawns at the start (0 = never spawn automatically, only through Spawn())
    [SerializeField] private float minInterval = 0.5f;   // the fastest it ever gets
    [SerializeField] private float rampDuration = 90f;   // seconds until the minimum is reached
    [SerializeField] private int maxObjects = 10;        // cap on spawned objects alive at the same time

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private float _startTime;
    private float _nextSpawn;

    // Enabling the component (re)starts the speed-up, so disable and enable it to restart a round
    private void OnEnable()
    {
        _startTime = Time.time;
        _nextSpawn = Time.time + startInterval;
    }

    private void Update()
    {
        if (startInterval <= 0f || Time.time < _nextSpawn) return;

        float t = rampDuration > 0f ? Mathf.Clamp01((Time.time - _startTime) / rampDuration) : 1f;
        _nextSpawn = Time.time + Mathf.Lerp(startInterval, minInterval, t);   // the wait shrinks as time passes
        Spawn();
    }

    // Spawns one random prefab at a random X along the range. Returns false if the cap is reached.
    public bool Spawn()
    {
        _spawned.RemoveAll(o => o == null);   // forget the ones that were destroyed
        if (_spawned.Count >= maxObjects || prefabs.Length == 0) return false;

        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        if (prefab == null) return false;

        Vector3 pos = transform.position;
        pos.x += Random.Range(xRange.x, xRange.y);

        // as a child of this object, so it ends up in THIS scene (Instantiate alone uses the active scene)
        GameObject newObj = Instantiate(prefab, pos, prefab.transform.rotation, transform);
        _spawned.Add(newObj);
        GameObject.Destroy(newObj, 3);
        return true;
    }

    // Draws the spawn line in the Scene view so you can place it
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 p = transform.position;
        Vector3 left = p + Vector3.right * xRange.x;
        Vector3 right = p + Vector3.right * xRange.y;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireSphere(left, 0.1f);
        Gizmos.DrawWireSphere(right, 0.1f);
    }
}