using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class geography : minigameController
{
    [Header("Spawning (follows timeScale)")]
    public GameObject[] projectilePrefabs;      // each spawn picks one at random
    public float spawnDelay = 1.5f;             // seconds between projectiles
    public float spawnDelayRandomness = 0.5f;   // each delay is spawnDelay plus or minus up to this
    public float projectileSpeed = 3f;          // units per second, not randomized
    [SerializeField] private float spawnDistance = 7f;   // how far from the middle they appear (outside the visible area)

    [Header("Tutorial (follows timeScale)")]
    public bool tutorial;                          // true when the game starts = the scripted waves instead of the normal game
    public float tutorialProjectileSpeed = 1.5f;   // slower than the normal ones
    public float tutorialStepDelay = 1f;           // the pause between the groups (and before a repeat)
    public float tutorialPairDelay = 0.4f;         // the short pause between the two of a pair (right/left, the diagonals)

    [Header("Shield (ignores timeScale)")]
    [SerializeField] private Transform shield;           // a bar whose sprite faces up at the top position
    [SerializeField] private float shieldRadius = 1.5f;  // distance of the shield from the middle
    [SerializeField] private float snapSharpness = 18f;  // higher = snappier

    [Header("Middle")]
    [SerializeField] private Collider2D centerCollider;  // the green dome, found on this object if empty

    [Header("Counters")]
    [SerializeField] private TMP_Text defendedText;
    [SerializeField] private TMP_Text passedText;

    [Header("Timer (ignores timeScale)")]
    [SerializeField] private float gameTime = 60f;
    [SerializeField] private Transform timerBar;         // scaled horizontally, runs out over gameTime, its left edge stays in place
    [SerializeField] private float endDelay = 1.5f;      // real seconds between the end and closing the minigame

    [Header("Grade")]
    [SerializeField] private string subjectId = "Geography";

    // The 5 directions, in degrees: 0 = right, 90 = top, 180 = left. Used for the spawns and the shield.
    private static readonly float[] Angles = { 0f, 45f, 90f, 135f, 180f };

    private readonly List<GameObject> _projectiles = new List<GameObject>();
    private float _shieldAngle = 90f;
    private float _spawnTimer;
    private float _elapsed;
    private bool _running;
    private bool _tutorialMode;          // this run is the tutorial (no timer, no random spawns, no grade changes)
    private Coroutine _tutorialRoutine;
    private int _defended, _passed;

    private bool _barReady;
    private Vector3 _barFullScale;
    private float _barLeftEdge;
    private float _barSpriteMinX;

    public int Defended => _defended;   // for the grade conditions
    public int Passed => _passed;

    protected override void MgAwake()
    {
        if (centerCollider == null) centerCollider = GetComponent<Collider2D>();
    }

    public void StartGame()
    {
        if (projectilePrefabs == null || projectilePrefabs.Length == 0)
        {
            Debug.LogWarning("geography: no projectile prefabs assigned", this);
            return;
        }

        if (_tutorialRoutine != null) { StopCoroutine(_tutorialRoutine); _tutorialRoutine = null; }

        ClearProjectiles();
        _defended = _passed = 0;
        _elapsed = 0f;
        _spawnTimer = NextDelay();
        _tutorialMode = tutorial;
        _running = true;

        if (projectileSpeed > 9) projectileSpeed = 9;
        if (spawnDelay < 0.2) spawnDelay = 0.2f;

        InitBar();
        UpdateBar();
        UpdateCounters();
        SetBarVisible(!_tutorialMode);

        if (_tutorialMode) _tutorialRoutine = StartCoroutine(TutorialRoutine());
    }

    private void SetBarVisible(bool visible)
    {
        if (timerBar != null && timerBar.gameObject.activeSelf != visible)
            timerBar.gameObject.SetActive(visible);
    }
    
    protected override void MgUpdate()
    {
        UpdateShield();   // always, so it already follows the keys before the game starts

        if (!_running || _tutorialMode) return;   // the tutorial has no timer and no random spawns

        // Timer: real time
        _elapsed += Time.unscaledDeltaTime;
        UpdateBar();
        if (_elapsed >= gameTime) { EndGame(); return; }

        // Spawning: world time
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            SpawnAt(Angles[Random.Range(0, Angles.Length)], projectileSpeed);
            _spawnTimer = NextDelay();
        }
    }

    // Tutorial: the same waves until the player lets nothing through, then the minigame ends
    private IEnumerator TutorialRoutine()
    {
        while (_running)
        {
            _defended = _passed = 0;
            UpdateCounters();

            SpawnAt(90f, tutorialProjectileSpeed);    // 1 from the top
            yield return new WaitForSeconds(tutorialStepDelay);

            SpawnAt(0f, tutorialProjectileSpeed);     // 1 from the right
            yield return new WaitForSeconds(tutorialPairDelay);
            SpawnAt(180f, tutorialProjectileSpeed);   // then 1 from the left
            yield return new WaitForSeconds(tutorialStepDelay);

            SpawnAt(45f, tutorialProjectileSpeed);    // 1 from each diagonal
            yield return new WaitForSeconds(tutorialPairDelay);
            SpawnAt(135f, tutorialProjectileSpeed);

            // wait until the last ones were blocked or got through
            while (_running)
            {
                _projectiles.RemoveAll(go => go == null);
                if (_projectiles.Count == 0) break;
                yield return null;
            }
            if (!_running) yield break;

            if (_passed == 0)
            {
                EndGame();   // nothing got through
                yield break;
            }

            // something got through: from the start again
            // ANIMATION: "try again" feedback
            yield return new WaitForSeconds(tutorialStepDelay);
        }
    }

    // Shield
    private float TargetAngle()
    {
        Vector2 m = MovementInput;   // needs Use Horizontal Movement and Use Vertical Movement on
        int x = m.x > 0.5f ? 1 : (m.x < -0.5f ? -1 : 0);
        bool up = m.y > 0.5f;

        if (x == 0) return 90f;            // nothing, W, or A+D: top
        if (x > 0) return up ? 45f : 0f;   // D: right, W+D: upper right
        return up ? 135f : 180f;           // A: left, W+A: upper left
    }

    private void UpdateShield()
    {
        if (shield == null) return;

        // eases toward the target: fast start, soft landing. The angle stays between 0 and 180, so right to left always sweeps over the top
        float t = 1f - Mathf.Exp(-snapSharpness * Time.unscaledDeltaTime);
        _shieldAngle = Mathf.Lerp(_shieldAngle, TargetAngle(), t);

        Vector2 pos = (Vector2)transform.position + Direction(_shieldAngle) * shieldRadius;
        shield.position = new Vector3(pos.x, pos.y, shield.position.z);
        shield.rotation = Quaternion.Euler(0f, 0f, _shieldAngle - 90f);   // faces away from the middle
    }

    // Projectiles
    private void SpawnAt(float angle, float speed)
    {
        GameObject prefab = projectilePrefabs[Random.Range(0, projectilePrefabs.Length)];
        if (prefab == null) return;

        Vector2 dir = Direction(angle);
        Vector2 pos = (Vector2)transform.position + dir * spawnDistance;

        // as a child of this object, so it ends up in THIS scene (Instantiate alone uses the active scene)
        GameObject go = Instantiate(prefab, new Vector3(pos.x, pos.y, transform.position.z), Quaternion.identity, transform);

        if (!go.TryGetComponent(out geographyProjectile p)) p = go.AddComponent<geographyProjectile>();
        p.Launch(this, -dir * speed);   // straight toward the middle

        _projectiles.Add(go);
        // ANIMATION: warning flash on the side it comes from
    }

    // Called by the projectiles when they touch something
    public void ProjectileTouched(geographyProjectile p, Collider2D other)
    {
        if (!_running || p.Resolved) return;

        if (shield != null && other.transform.IsChildOf(shield))
        {
            p.Resolved = true;
            _defended++;
            // ANIMATION: block effect on the shield
        }
        else if (other == centerCollider)
        {
            p.Resolved = true;
            _passed++;
            if (!_tutorialMode && _passed % 2 == 0)   // the tutorial doesn't touch the grade
            {
                AssignmentManager.Instance.Worsen(subjectId);
                Debug.Log("Grade worsened");
            }
            // ANIMATION: hit effect on the dome
        }
        else return;   // touched something else (e.g. another projectile)

        _projectiles.Remove(p.gameObject);
        Destroy(p.gameObject);
        UpdateCounters();
    }

    private void ClearProjectiles()
    {
        foreach (GameObject go in _projectiles) if (go != null) Destroy(go);
        _projectiles.Clear();
    }

    private float NextDelay()
    {
        return Mathf.Max(0.1f, spawnDelay + Random.Range(-spawnDelayRandomness, spawnDelayRandomness));
    }

    private static Vector2 Direction(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void UpdateCounters()
    {
        if (defendedText != null) defendedText.text = _defended.ToString();
        if (passedText != null) passedText.text = _passed.ToString();
    }

    // End
    private void EndGame()
    {
        if (!_running) return;
        _running = false;
        ClearProjectiles();   // nothing may hit after the end

        Debug.Log($"geography: defended {_defended}, passed {_passed}{(_tutorialMode ? " (tutorial)" : "")}");

        if (!_tutorialMode && _passed == 0)
        {
            AssignmentManager.Instance.Improve(subjectId);
        }

        StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        // ANIMATION: end feedback while the game waits to close
        yield return new WaitForSecondsRealtime(endDelay);
        ClassroomInput.Instance.CloseMinigame(true);
    }

    // Timer bar (same as the other minigames)
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

        float fraction = gameTime > 0f ? Mathf.Clamp01(1f - _elapsed / gameTime) : 0f;

        Vector3 scale = _barFullScale;
        scale.x = _barFullScale.x * fraction;
        timerBar.localScale = scale;

        Vector3 pos = timerBar.localPosition;
        pos.x = _barLeftEdge - _barSpriteMinX * scale.x;
        timerBar.localPosition = pos;
    }

    private void OnDrawGizmosSelected()   // shows the shield ring and the 5 spawn points in the Scene view
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, shieldRadius);

        Gizmos.color = Color.red;
        foreach (float a in Angles)
        {
            Vector3 p = transform.position + (Vector3)(Direction(a) * spawnDistance);
            Gizmos.DrawWireSphere(p, 0.25f);
            Gizmos.DrawLine(p, transform.position);
        }
    }
}