using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mobliterator : minigameController
{
    [Header("Molecules")]
    [SerializeField] private GameObject[] moleculePrefabs;   // each spawn picks one at random
    [SerializeField] private int moleculeCount = 40;         // X: how many molecules are spawned
    [SerializeField] private float spawnRadius = 2f;         // they are scattered inside this circle around this object

    [Header("Shooting")]
    [SerializeField] private Transform shootPoint;           // where each new shot molecule appears (right side of the screen)
    [SerializeField] private Transform gravityCenter;        // the point the shot molecule falls toward after its first contact
    [SerializeField] private Vector2 shootDirection = Vector2.left;
    [SerializeField] private bool aimAtCursor = false;       // fire toward the cursor instead of shootDirection
    [SerializeField] private float shotSpeed = 8f;
    [SerializeField] private float respawnDelay = 0.5f;      // pause before the next molecule to shoot appears

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private molecule _shot;        // the molecule that is waiting or flying
    private Coroutine _respawn;

    public void StartGame()
    {
        if (moleculePrefabs == null || moleculePrefabs.Length == 0)
        {
            Debug.LogWarning("mobliterator: no molecule prefabs assigned", this);
            return;
        }

        // a second call starts a fresh round instead of piling up molecules
        if (_respawn != null) { StopCoroutine(_respawn); _respawn = null; }
        if (_shot != null)
        {
            _shot.ShotFinished -= OnShotFinished;
            Destroy(_shot.gameObject);
            _shot = null;
        }
        foreach (GameObject old in _spawned) if (old != null) Destroy(old);
        _spawned.Clear();

        for (int i = 0; i < moleculeCount; i++)
        {
            GameObject prefab = moleculePrefabs[Random.Range(0, moleculePrefabs.Length)];
            if (prefab == null) continue;

            Vector3 pos = transform.position + (Vector3)(Random.insideUnitCircle * spawnRadius);

            // as a child of this object, so it ends up in THIS scene (Instantiate alone uses the active scene)
            _spawned.Add(Instantiate(prefab, pos, prefab.transform.rotation, transform));
        }

        SpawnShot();
    }

    private void SpawnShot()
    {
        _respawn = null;

        GameObject prefab = moleculePrefabs[Random.Range(0, moleculePrefabs.Length)];
        if (prefab == null || shootPoint == null)
        {
            Debug.LogWarning("mobliterator: missing prefab or shoot point, no molecule to shoot", this);
            return;
        }

        GameObject go = Instantiate(prefab, shootPoint.position, prefab.transform.rotation, transform);
        if (!go.TryGetComponent(out _shot))
        {
            Debug.LogWarning("mobliterator: the prefab has no molecule script", this);
            Destroy(go);
            return;
        }

        _shot.ShotFinished += OnShotFinished;
        _shot.PrepareAsShot(gravityCenter);
        // ANIMATION: the new molecule appears at the shoot point (scale or fade in)
    }

    private void OnShotFinished(molecule m)
    {
        m.ShotFinished -= OnShotFinished;
        if (_shot == m) _shot = null;

        // a settled shot stays in the mass, so the next round clears it too (a popped one is already being destroyed)
        _spawned.Add(m.gameObject);

        if (_respawn != null) StopCoroutine(_respawn);
        _respawn = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnShot();
    }

    // Fire
    protected override void OnLeftDown()
    {
        if (_shot == null) return;

        Vector2 dir = shootDirection;
        if (aimAtCursor)
        {
            Vector2 toCursor = CursorPosition() - (Vector2)shootPoint.position;
            if (toCursor.sqrMagnitude > 0.0001f) dir = toCursor;
        }

        _shot.Launch(dir, shotSpeed);   // ignored if it was already fired
    }

    // Steer
    protected override void MgFixedUpdate()
    {
        if (_shot != null) _shot.Steer(MovementInput.y);   // W/S, zero unless Use Vertical Movement is on
    }

    private void OnDrawGizmosSelected()   // shows the spawn area and the shoot point in the Scene view
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        if (shootPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(shootPoint.position, 0.3f);
            Gizmos.DrawRay(shootPoint.position, (Vector3)shootDirection.normalized * 2f);
        }
    }
}