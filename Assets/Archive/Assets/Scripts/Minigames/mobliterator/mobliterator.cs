using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class mobliterator : minigameController
{
    [Header("Molecules")]
    [SerializeField] private GameObject[] moleculePrefabs;   // each spawn picks one at random
    [SerializeField] public int moleculeCount = 40;         // X: how many molecules are spawned
    [SerializeField] private float spawnRadius = 2f;         // they are scattered inside this circle around this object

    [Header("Shooting")]
    [SerializeField] private Transform shootPoint;           // where each new molecule to shoot appears
    [SerializeField] private Transform gravityCenter;        // the point the shot molecule falls toward after its first contact
    [SerializeField] private bool shootLeft = true;          // the side the molecules are shot toward
    [SerializeField] private float shotSpeed = 8f;
    [SerializeField] private float respawnDelay = 0.5f;      // pause before the next molecule to shoot appears

    [Header("Aiming")]
    [SerializeField] private float aimSpeed = 60f;           // degrees per second while holding W/S
    [SerializeField] private float maxAimAngle = 60f;        // how far up or down it can aim
    [SerializeField] private Transform aimIndicator;         // optional arrow, its sprite must point right (+X)

    [Header("Goal")]
    [SerializeField] private TMP_Text counterText;           // the 3D TextMeshPro object, shows popped/required
    private string assignmentName = "Chemistry";             // as AssignmentManager knows it (investit uses "Finance")

    [Header("Timer")]
    [SerializeField] private float secondsPerMolecule = 3f;  // total time = required pops * this
    [SerializeField] private Transform timerBar;             // scaled horizontally, runs out over the total time, its left edge stays in place
    [SerializeField] private float endDelay = 1.5f;          // real seconds between the end and closing the minigame

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private molecule _shot;        // the molecule that is waiting or flying
    private Coroutine _respawn;
    private float _aimAngle;       // degrees above (+) or below (-) the horizontal

    private int _popped, _required;
    private float _gameTime, _elapsed;
    private bool _running, _ended;

    private bool _barReady;
    private Vector3 _barFullScale;
    private float _barLeftEdge;
    private float _barSpriteMinX;

    protected override void MgOnEnable()  { molecule.AnyPopped += OnMoleculePopped; }
    protected override void MgOnDisable() { molecule.AnyPopped -= OnMoleculePopped; }

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

        // goal and timer
        _popped = 0;
        _required = moleculeCount / 3;
        _gameTime = _required * secondsPerMolecule;
        _elapsed = 0f;
        _ended = false;
        _running = true;
        InitBar();
        UpdateBar();
        UpdateCounter();

        _aimAngle = 0f;
        SpawnShot();
    }

    private void SpawnShot()
    {
        _respawn = null;
        if (_ended) return;

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

        if (_ended) return;
        if (_respawn != null) StopCoroutine(_respawn);
        _respawn = StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnShot();
    }

    // Counting
    private void OnMoleculePopped(molecule m)
    {
        if (!_running || m.transform.parent != transform) return;   // only this minigame's molecules

        _popped++;
        UpdateCounter();
        StartCoroutine(CheckAllPopped());
    }

    // Nothing left in the mass (the molecule waiting or flying to be shot doesn't count)
    private IEnumerator CheckAllPopped()
    {
        yield return null;   // the molecule that just popped is destroyed at the end of this frame
        if (!_running) yield break;

        foreach (molecule m in GetComponentsInChildren<molecule>())
        {
            if (m.InMass) yield break;   // something is still in the mass, or about to pop
        }

        EndGame(true);
    }

    private void UpdateCounter()
    {
        if (counterText != null) counterText.text = _popped + "/" + _required;
    }

    // End
    private void EndGame(bool poppedAll)
    {
        if (_ended) return;
        _ended = true;
        _running = false;
        if (_respawn != null) { StopCoroutine(_respawn); _respawn = null; }

        Debug.Log($"mobliterator: popped {_popped}/{_required}, all popped: {poppedAll}");

        if (poppedAll) Grade(true);
        else if (_popped < _required) Grade(false);
        else if (_popped <= moleculeCount / 2) Grade(true);
        
        // else: the required number was reached but not everything popped, the grade stays

        StartCoroutine(CloseRoutine());
    }

    private void Grade(bool improve)
    {
        if (string.IsNullOrEmpty(assignmentName))
        {
            Debug.LogWarning("mobliterator: assignment name is empty, the grade was not changed", this);
            return;
        }

        if (improve) AssignmentManager.Instance.Improve(assignmentName);
        else AssignmentManager.Instance.Worsen(assignmentName);
    }

    private IEnumerator CloseRoutine()
    {
        // ANIMATION: end feedback (success or failure) while the game waits to close
        yield return new WaitForSecondsRealtime(endDelay);
        ClassroomInput.Instance.CloseMinigame(true);
    }

    // The aimed direction: W/S tilt it up or down from the horizontal
    private Vector2 AimDirection()
    {
        float rad = _aimAngle * Mathf.Deg2Rad;
        return new Vector2((shootLeft ? -1f : 1f) * Mathf.Cos(rad), Mathf.Sin(rad));
    }

    protected override void MgUpdate()
    {
        // Timer
        if (_running)
        {
            _elapsed += Time.deltaTime;   // the world clock, follows timeScale like the other timers
            UpdateBar();
            if (_elapsed >= _gameTime) EndGame(false);
        }

        // Aim
        bool aiming = _running && _shot != null && _shot.IsWaiting;

        if (aiming)
        {
            // MovementDeltaTime: the aiming follows the "movement ignores time scale" toggle
            _aimAngle += MovementInput.y * aimSpeed * MovementDeltaTime;
            _aimAngle = Mathf.Clamp(_aimAngle, -maxAimAngle, maxAimAngle);
        }

        if (aimIndicator != null)
        {
            aimIndicator.gameObject.SetActive(aiming);   // hidden once it is fired
            if (aiming)
            {
                Vector2 dir = AimDirection();
                aimIndicator.position = shootPoint.position;
                aimIndicator.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                // ANIMATION: the arrow or trajectory line can pulse or stretch here
            }
        }
    }

    // Fire
    protected override void OnLeftDown()
    {
        if (!_running || _shot == null) return;
        _shot.Launch(AimDirection(), shotSpeed);   // ignored if it was already fired
    }

    // Timer bar (same as investit)
    private void InitBar()
    {
        if (timerBar == null || _barReady) return;
        _barReady = true;

        _barFullScale = timerBar.localScale;

        // the bar shrinks towards its left edge, whatever the sprite's pivot is
        var sr = timerBar.GetComponent<SpriteRenderer>();
        _barSpriteMinX = (sr != null && sr.sprite != null) ? sr.sprite.bounds.min.x : 0f;
        _barLeftEdge = timerBar.localPosition.x + _barSpriteMinX * _barFullScale.x;
    }

    private void UpdateBar()
    {
        if (timerBar == null || !_barReady) return;

        float fraction = _gameTime > 0f ? Mathf.Clamp01(1f - _elapsed / _gameTime) : 0f;

        Vector3 scale = _barFullScale;
        scale.x = _barFullScale.x * fraction;
        timerBar.localScale = scale;

        Vector3 pos = timerBar.localPosition;
        pos.x = _barLeftEdge - _barSpriteMinX * scale.x;
        timerBar.localPosition = pos;
    }

    private void OnDrawGizmosSelected()   // shows the spawn area, the shoot point and the current aim in the Scene view
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        if (shootPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(shootPoint.position, 0.3f);
            Gizmos.DrawRay(shootPoint.position, (Vector3)AimDirection() * 2f);
        }
    }
}