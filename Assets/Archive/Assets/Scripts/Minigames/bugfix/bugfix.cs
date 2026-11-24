using System.Collections.Generic;
using UnityEngine;

public class bugfix : minigameController
{
    [Header("Bug spawning")]
    [SerializeField] private GameObject bugPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] public int maxBugs = 5;
    [SerializeField] public float spawnInterval = 3f;        // 0 = never spawn automatically, only through SpawnBug()
    [SerializeField] private float minSpawnInterval = 0.8f;   // the fastest it ever gets
    [SerializeField] private float rampDuration = 120f;       // seconds until the minimum is reached

    [Header("Tutorial")]
    public bool tutorial;                                // set to true before StartGame() to play the tutorial first
    [SerializeField] private GameObject gameRoot;        // the "game" object, disabled during the tutorial
    [SerializeField] private GameObject tutorialRoot;    // the "tutorial" object, enabled during the tutorial
    [SerializeField] private int tutorialBugsRequired = 4;

    private bool _running;   // the real game is going
    private bool _inTutorial;
    private int _tutorialEliminated;
    private bool _barReady;
    
    private readonly List<GameObject> _bugs = new List<GameObject>();
    private float _startTime;
    private float _nextSpawn;
    private int _eliminated;
    public bool perfect = true;
    private bool _finished;

    // timer bar
    private Vector3 _barFullScale;
    private float _barLeftEdge;
    private float _barSpriteMinX;

    [Header("Timer")]
    [SerializeField] private float maxTime = 20f;           // the bar is full at this value, time can't go above it
    [SerializeField] private float startTime = 20f;         // how long spawning runs
    [SerializeField] private Transform timerBar;
    private float timeLeft;

    public void StartGame()
    {
        _finished = false;
        _running = false;

        if (tutorial)
        {
            _inTutorial = true;
            _tutorialEliminated = 0;
            SetActiveSafe(gameRoot, false);
            SetActiveSafe(tutorialRoot, true);
            // ANIMATION: tutorial intro
        }
        else BeginGame();
    }

    private void BeginGame()   // the real game: runs right away, or after the tutorial
    {
        _inTutorial = false;
        SetActiveSafe(tutorialRoot, false);
        SetActiveSafe(gameRoot, true);

        _startTime = Time.time;
        _nextSpawn = 0f;
        _eliminated = 0;
        timeLeft = Mathf.Min(startTime, maxTime);
        InitBar();
        UpdateBar();
        _running = true;
    }
    
    // Never disables an object this script lives under, that would switch the minigame off
    private void SetActiveSafe(GameObject go, bool active)
    {
        if (go == null) return;
        if (!active && transform.IsChildOf(go.transform))
        {
            Debug.LogWarning("bugfix: this script is inside " + go.name + ", so it can't be disabled. Move the script outside it.", this);
            return;
        }
        go.SetActive(active);
    }

    protected override void MgOnEnable()  => bugRightclick.Eliminated += OnBugEliminated;
    protected override void MgOnDisable() => bugRightclick.Eliminated -= OnBugEliminated;

    private void OnBugEliminated(bugRightclick bug)
    {
        if (_inTutorial)
        {
            _tutorialEliminated++;
            if (_tutorialEliminated >= tutorialBugsRequired) BeginGame();
            return;   // the tutorial bugs don't count for the real game
        }

        _eliminated++;
        Debug.Log($"Bugs eliminated: {_eliminated}");
    }

    public bool SpawnBug()
    {
        _bugs.RemoveAll(b => b == null);   // forget the bugs that were deleted
        if (_bugs.Count >= maxBugs || bugPrefab == null || spawnPoints.Length == 0) return false;

        Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

        // as a child of this object, so it ends up in THIS scene (Instantiate alone uses the active scene)
        _bugs.Add(Instantiate(bugPrefab, point.position, Quaternion.identity, transform));
        return true;
    }

    protected override void MgUpdate()
    {
        if (!_running || _finished) return;

        // countdown
        timeLeft -= Time.unscaledDeltaTime;   // real time, ignores timeScale
        UpdateBar();

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            UpdateBar();
            _finished = true;   // stops spawning and the countdown
            OnTimeUp();
            return;
        }

        // spawning
        if (spawnInterval > 0f && Time.time >= _nextSpawn)
        {
            float t = rampDuration > 0f ? Mathf.Clamp01((Time.time - _startTime) / rampDuration) : 1f;
            _nextSpawn = Time.time + Mathf.Lerp(spawnInterval, minSpawnInterval, t);   // the wait shrinks as time passes
            SpawnBug();
        }
    }

    private void OnTimeUp()
    {
        if (perfect) AssignmentManager.Instance.Improve("IT");
        ClassroomInput.Instance.CloseMinigame(true);
    }

    // optional: call this (e.g. from OnBugEliminated) to give bonus time
    public void AddTime(float seconds)
    {
        if (_finished) return;
        timeLeft = Mathf.Min(timeLeft + seconds, maxTime);   // capped at maxTime
    }

    // ---------- timer bar ----------

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
        if (timerBar == null) return;

        float fraction = maxTime > 0f ? Mathf.Clamp01(timeLeft / maxTime) : 0f;

        Vector3 scale = _barFullScale;
        scale.x = _barFullScale.x * fraction;
        timerBar.localScale = scale;

        Vector3 pos = timerBar.localPosition;
        pos.x = _barLeftEdge - _barSpriteMinX * scale.x;
        timerBar.localPosition = pos;
    }
}