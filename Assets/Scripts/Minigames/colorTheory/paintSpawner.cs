using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// The paint minigame. Spawns pairs of paint blobs (one color = one blob on the left, one on the right)
/// and runs the rules: connect every pair before the timer runs out. Completing all pairs gives time back,
/// clears the screen and starts a new round with more pairs. When the timer hits zero the board is wiped
/// and nothing is spawned anymore.
///
/// It does not read any input itself: the minigame controller (art) passes the cursor position in through
/// PointerDown / PointerMove / PointerUp.
///
/// Left side:  colors in order 0..n-1.
/// Right side: the same colors, shuffled.
///
/// Layout: each side is a blend between a straight vertical line (curve = 0) and an arc (curve = 1).
/// The values are randomized every round, then the whole layout is scaled down if needed so that it
/// stays inside the play area (see "Bounds"; the area is drawn as a gizmo when this object is selected).
public class paintSpawner : MonoBehaviour
{
    [Header("Sounds")]
    [SerializeField] private AudioClip connectSound;
    [SerializeField, Range(0f, 1f)] private float connectVolume = 0.7f;
    [SerializeField] private AudioClip roundCompleteSound;
    [SerializeField, Range(0f, 1f)] private float roundCompleteVolume = 0.9f;
    [SerializeField] private AudioClip brushLoopSound;
    [SerializeField, Range(0f, 1f)] private float brushVolume = 0.5f;
    [SerializeField] private float brushFadeTime = 0.1f;       // seconds to fade in or out
    [SerializeField] private float brushFullSpeed = 6f;        // cursor speed (world units per second) that gives full volume

    private AudioSource _brushSource;
    private float _brushTarget;
    private Vector2 _lastPointer;
    private bool _hasLastPointer;
    
    [Header("Prefabs")]
    [SerializeField] private GameObject paintPrefab;      // the invisible brush (has the paint component)
    [SerializeField] private GameObject blobPrefab;       // the blob you click; use a WHITE sprite, it gets tinted

    [Header("Layout")]
    [SerializeField] private Transform spawnRoot;         // should sit in the CENTER of the play area
    [SerializeField] private float spawnSegment = 0.5f;
    [SerializeField] private float distance = 5f;
    [SerializeField] private float xRandomness = 0f;      // each blob is shifted left or right by up to this much (local units)

    [Header("Layout randomness")]
    [SerializeField, Range(0f, 1f)] private float minCurve = 0f;     // 0 = two straight lines
    [SerializeField, Range(0f, 1f)] private float maxCurve = 1f;     // 1 = two arcs
    [SerializeField] private float distanceRandomness = 0f;          // distance varies by +/- this
    [SerializeField, Range(0f, 1f)] private float segmentRandomness = 0f;   // spawnSegment varies by +/- this

    [Header("Bounds")]
    [SerializeField] private Vector2 areaSize = new Vector2(16f, 9f);   // play area, centered on the spawn root (local units)
    [SerializeField] private float edgePadding = 0.8f;    // keeps blob centers this far from the border (about half a blob)
    [SerializeField] private float minBlobSpacing = 1f;   // layouts with blobs closer than this are rerolled
    [SerializeField] private int layoutAttempts = 30;

    [Header("Rounds")]
    [SerializeField] public int startPairs = 2;              // pairs in the first round
    [SerializeField] private int roundsBeforeIncrease = 0;    // rounds played with startPairs before it grows
    [SerializeField] private int roundsPerIncrease = 1;       // after that, +1 pair every this many rounds
    [SerializeField] private int maxPairs = 10;                // hard cap
    [SerializeField] private int pointsPerPair = 1;
    [SerializeField] public int roundsToWin = 3;             // completing this many rounds completes the assignment
    [SerializeField] private float clearDelay = 0.35f;        // pause after the last pair so you see the finished picture (0 = instant)

    [Header("Timer")]
    [SerializeField] private float maxTime = 20f;             // the bar is full at this value, time can't go above it

    public bool tutorial;
    private bool _timerOff;
    private float _savedMaxTime;
    
    [SerializeField] private float startTime = 20f;
    [SerializeField] private float timeBonus = 5f;            // seconds gained for completing a round
    [SerializeField] private Transform timerBar;              // scaled horizontally, its left edge stays in place
    [SerializeField] private bool timersIgnoreTimeScale;

    [Header("Gameplay")]
    [SerializeField] private float blobRadius = 0.7f;         // how close the cursor must be to a blob (world units)
    [SerializeField] private bool clearFailedStrokeOnRelease = false;   // false: stays until you start again

    [Header("State (read only)")]
    [SerializeField] private int score;
    [SerializeField] private int roundsCompleted;
    [SerializeField] private float timeLeft;

    [Header("Events")]
    public UnityEvent onRoundCompleted;
    public UnityEvent onGameOver;

    public int Score => score;
    public int RoundsCompleted => roundsCompleted;
    public float TimeLeft => timeLeft;
    public bool IsGameOver => _gameOver;

    private class Pair
    {
        public Color color;
        public paint brush;
        public bool done;
        public Blob[] ends = new Blob[2];
        public Blob Other(Blob b) { return ends[0] == b ? ends[1] : ends[0]; }
    }

    private class Blob
    {
        public Transform t;
        public Pair pair;
    }

    private readonly List<Pair> _pairs = new List<Pair>();
    private readonly List<Blob> _blobs = new List<Blob>();
    private readonly List<GameObject> _spawned = new List<GameObject>();

    private Pair _active;
    private Blob _target;
    private int _completed;

    private bool _gameOver = true;
    private bool _transitioning;
    private float _transitionTimer;

    // timer bar
    private Vector3 _barFullScale;
    private float _barLeftEdge;
    private float _barSpriteMinX;

    public void StartGame()
    {
        if (spawnRoot == null) spawnRoot = transform;

        if (_brushSource == null && brushLoopSound != null)
        {
            _brushSource = gameObject.AddComponent<AudioSource>();
            _brushSource.clip = brushLoopSound;
            _brushSource.loop = true;
            _brushSource.playOnAwake = false;
            _brushSource.spatialBlend = 0f;   // 2D
            _brushSource.volume = 0f;
        }

        InitBar();
        ResetGame();
    }

    private void Update()
    {
        UpdateBrushSound();
        if (_gameOver) return;

        if (_transitioning)
        {
            // the timer is paused while the finished picture is shown
            _transitionTimer -= DeltaTime;
            if (_transitionTimer <= 0f)
            {
                _transitioning = false;
                ClearBoard();
                StartRound();
            }
            return;
        }

        if (!_timerOff)   // the tutorial's first round has no timer
        {
            timeLeft -= DeltaTime;
            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                EndGame();
                return;
            }
        }
        UpdateBar();
    }

    // ---------- called by the minigame controller (art), positions are in world space ----------

    /// Left button pressed: starts a drag if the cursor is on an unfinished blob.
    public void PointerDown(Vector2 p)
    {
        if (_active != null || _gameOver || _transitioning) return;

        Blob hit = FindBlobAt(p);
        if (hit != null) StartDrag(hit);
    }

    /// Call every frame: moves the brush and completes the color when it reaches the matching blob.
    public void PointerMove(Vector2 p)
    {
        if (_active == null) return;

        float speed = _hasLastPointer
            ? (p - _lastPointer).magnitude / Mathf.Max(Time.unscaledDeltaTime, 0.0001f)
            : 0f;
        _lastPointer = p;
        _hasLastPointer = true;
        _brushTarget = Mathf.Clamp01(speed / brushFullSpeed) * brushVolume;

        MoveBrush(_active.brush, p);
        if (IsOver(_target, p)) Complete();
    }

    /// Left button released: completes the color if released on the matching blob, otherwise ends the drag.
    public void PointerUp(Vector2 p)
    {
        if (_active == null) return;

        MoveBrush(_active.brush, p);
        if (IsOver(_target, p)) Complete();
        else EndDrag();
    }

    /// For when the controller is disabled mid-drag.
    public void CancelDrag()
    {
        if (_active != null) EndDrag();
    }

    // ---------- game flow ----------

    /// Wipes everything and starts a brand new game (score 0, full timer, first round).
    public void ResetGame()
    {
        ClearBoard();
        score = 0;
        roundsCompleted = 0;
        timeLeft = Mathf.Min(startTime, maxTime);
        _gameOver = false;
        _transitioning = false;
        
        if (tutorial)
        {
            _timerOff = true;
            _savedMaxTime = maxTime;
            maxTime = 0f;
        }
        
        UpdateBar();
        StartRound();
    }

    private void StartRound()
    {
        Build(PairsForRound(roundsCompleted));
    }

    // pairs per round: startPairs for the first roundsBeforeIncrease rounds, then it grows
    // (with the defaults: 1, 1, 2, 3, 4, ...)
    private int PairsForRound(int completedRounds)
    {
        int pairs = startPairs;
        if (completedRounds >= roundsBeforeIncrease)
            pairs += 1 + (completedRounds - roundsBeforeIncrease) / Mathf.Max(1, roundsPerIncrease);
        return Mathf.Clamp(pairs, 1, Mathf.Max(1, maxPairs));
    }

    private void FinishRound()
    {
        if (_timerOff)
        {
            _timerOff = false;
            maxTime = _savedMaxTime;
        }
        roundsCompleted++;
        Play(roundCompleteSound, roundCompleteVolume);
        timeLeft = Mathf.Min(maxTime, timeLeft + timeBonus);
        UpdateBar();
        onRoundCompleted?.Invoke();

        if (roundsCompleted >= roundsToWin)
        {
            Win();
            return;
        }

        if (clearDelay <= 0f)
        {
            ClearBoard();
            StartRound();
        }
        else
        {
            _transitioning = true;
            _transitionTimer = clearDelay;
        }
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip != null) SFXManager.instance.PlaySFX(clip, transform, volume);
    }

    private void UpdateBrushSound()
    {
        if (_brushSource == null) return;

        bool dragging = _active != null && !_gameOver;
        float target = dragging ? _brushTarget : 0f;

        if (target > 0f && !_brushSource.isPlaying)
        {
            _brushSource.time = Random.value * brushLoopSound.length;   // a different start each time, so strokes don't sound identical
            _brushSource.Play();
        }

        float step = brushFadeTime > 0f ? brushVolume / brushFadeTime * Time.unscaledDeltaTime : brushVolume;
        _brushSource.volume = Mathf.MoveTowards(_brushSource.volume, target, step);

        if (target <= 0f && _brushSource.volume <= 0f && _brushSource.isPlaying) _brushSource.Stop();
    }
    
    private float DeltaTime => timersIgnoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

    // the timer ran out: the assignment failed
    private void EndGame()
    {
        _gameOver = true;
        _transitioning = false;
        Finish(false);
    }

    // enough rounds: the assignment is complete (after a short pause so the last stroke can be seen)
    private void Win()
    {
        _gameOver = true;     // stops the timer and the input
        _transitioning = false;
        StartCoroutine(FinishAfterDelay());
    }

    private IEnumerator FinishAfterDelay()
    {
        if (clearDelay > 0f)
        {
            if (timersIgnoreTimeScale) yield return new WaitForSecondsRealtime(clearDelay);
            else yield return new WaitForSeconds(clearDelay);
        }
        Finish(true);
    }

    private void Finish(bool success)
    {
        ClearBoard();     // wipe the screen and spawn nothing more
        UpdateBar();

        Debug.Log($"Paint finished. Score: {score}, rounds completed: {roundsCompleted}, {(success ? "assignment complete" : "time ran out")}");
        
        if (success) AssignmentManager.Instance.Improve("Art");
        else AssignmentManager.Instance.Worsen("Art");
        
        ClassroomInput.Instance.CloseMinigame(success);
        onGameOver?.Invoke();
    }

    // removes every blob, brush and stroke
    private void ClearBoard()
    {
        foreach (Pair p in _pairs)
            if (p.brush != null) p.brush.ClearStrokes();
        foreach (GameObject go in _spawned)
            if (go != null) Destroy(go);

        _pairs.Clear();
        _blobs.Clear();
        _spawned.Clear();
        paint.ResetSortingOrder();
        _active = null;
        _target = null;
    }

    // ---------- timer bar ----------

    private void InitBar()
    {
        if (timerBar == null) return;
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

    // ---------- setup ----------

    private void Build(int n)
    {
        _completed = 0;
        _active = null;

        // Evenly spread hues (so no two colors look alike), random start, random order.
        var hues = new List<float>();
        float hueStart = Random.value;
        for (int i = 0; i < n; i++) hues.Add((hueStart + i / (float)n) % 1f);
        Shuffle(hues);

        for (int i = 0; i < n; i++)
        {
            var pair = new Pair
            {
                color = Color.HSVToRGB(hues[i], Random.Range(0.75f, 1f), Random.Range(0.85f, 1f))
            };

            GameObject brushObj = Instantiate(paintPrefab, spawnRoot);
            brushObj.transform.localPosition = Vector3.zero;
            pair.brush = brushObj.GetComponent<paint>();
            pair.brush.SetColor(pair.color);
            _spawned.Add(brushObj);

            _pairs.Add(pair);
        }

        // Right side order: shuffled, but never the same as the left (that would be too easy).
        var order = new List<int>();
        for (int i = 0; i < n; i++) order.Add(i);
        if (n > 1)
        {
            do { Shuffle(order); } while (IsIdentity(order));
        }

        GenerateLayout(n, out Vector2[] leftPositions, out Vector2[] rightPositions);

        for (int slot = 0; slot < n; slot++)
        {
            SpawnBlob(_pairs[slot], leftPositions[slot], 0);          // left, index 0 of the pair
            SpawnBlob(_pairs[order[slot]], rightPositions[slot], 1);  // right, index 1 of the pair
        }
    }

    private void SpawnBlob(Pair pair, Vector2 position, int end)
    {
        GameObject go = Instantiate(blobPrefab, spawnRoot);
        go.transform.localPosition = new Vector3(position.x, position.y, 0f);

        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.color = pair.color;

        var blob = new Blob { t = go.transform, pair = pair };
        pair.ends[end] = blob;
        _blobs.Add(blob);
        _spawned.Add(go);
    }

    // ---------- layout ----------

    // Rolls random layout values, builds both sides, fits them into the play area and rejects layouts
    // where blobs are too close together. If no attempt is good enough, the best one is used.
    private void GenerateLayout(int n, out Vector2[] left, out Vector2[] right)
    {
        Vector2[] bestLeft = null, bestRight = null;
        float bestSpacing = -1f;

        for (int attempt = 0; attempt < Mathf.Max(1, layoutAttempts); attempt++)
        {
            float curve = Random.Range(minCurve, maxCurve);
            float segment = Mathf.Clamp(spawnSegment + Random.Range(-segmentRandomness, segmentRandomness), 0.1f, 1f);
            float radius = Mathf.Max(0.1f, distance + Random.Range(-distanceRandomness, distanceRandomness));

            Vector2[] l = BuildSide(n, 180f, curve, segment, radius);
            Vector2[] r = BuildSide(n, 0f, curve, segment, radius);
            FitToArea(l, r);

            float spacing = MinSpacing(l, r);
            if (spacing > bestSpacing)
            {
                bestSpacing = spacing;
                bestLeft = l;
                bestRight = r;
            }
            if (spacing >= minBlobSpacing) break;
        }

        left = bestLeft;
        right = bestRight;
    }

    // One side of the layout. arcCenter is 180 (left) or 0 (right).
    // curve 1 = points on the arc, curve 0 = points on a vertical line through the arc's outermost point.
    private Vector2[] BuildSide(int n, float arcCenter, float curve, float segment, float radius)
    {
        float span = 180f * segment;
        float dir = Mathf.Cos(arcCenter * Mathf.Deg2Rad);                      // -1 left, +1 right
        float halfHeight = radius * Mathf.Sin(span * 0.5f * Mathf.Deg2Rad);    // same height as the arc has

        var result = new Vector2[n];
        for (int slot = 0; slot < n; slot++)
        {
            float u = 0.5f - (slot + 0.5f) / n;                  // -0.5 .. 0.5 along the side
            float angle = (arcCenter + span * u) * Mathf.Deg2Rad;

            Vector2 arc = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Vector2 line = new Vector2(dir * radius, dir * 2f * u * halfHeight);   // evenly spaced vertically

            Vector2 p = Vector2.Lerp(line, arc, curve);
            p.x += Random.Range(-xRandomness, xRandomness);
            result[slot] = p;
        }
        return result;
    }

    // Scales the layout towards the center of the area (never up) until every blob center is inside it.
    // Each axis is scaled on its own, so a layout that is too tall doesn't also get squeezed horizontally.
    private void FitToArea(Vector2[] a, Vector2[] b)
    {
        float halfW = Mathf.Max(0.01f, areaSize.x * 0.5f - edgePadding);
        float halfH = Mathf.Max(0.01f, areaSize.y * 0.5f - edgePadding);

        float maxX = 0f, maxY = 0f;
        foreach (Vector2 p in a) { maxX = Mathf.Max(maxX, Mathf.Abs(p.x)); maxY = Mathf.Max(maxY, Mathf.Abs(p.y)); }
        foreach (Vector2 p in b) { maxX = Mathf.Max(maxX, Mathf.Abs(p.x)); maxY = Mathf.Max(maxY, Mathf.Abs(p.y)); }

        float sx = maxX > halfW ? halfW / maxX : 1f;
        float sy = maxY > halfH ? halfH / maxY : 1f;
        if (sx >= 1f && sy >= 1f) return;

        for (int i = 0; i < a.Length; i++) a[i] = new Vector2(a[i].x * sx, a[i].y * sy);
        for (int i = 0; i < b.Length; i++) b[i] = new Vector2(b[i].x * sx, b[i].y * sy);
    }

    private static float MinSpacing(Vector2[] a, Vector2[] b)
    {
        var all = new List<Vector2>(a.Length + b.Length);
        all.AddRange(a);
        all.AddRange(b);

        float min = float.MaxValue;
        for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
                min = Mathf.Min(min, Vector2.Distance(all[i], all[j]));
        return min;
    }

    // Shows the play area (and the padded area the blob centers stay in) when this object is selected.
    private void OnDrawGizmosSelected()
    {
        Transform root = spawnRoot != null ? spawnRoot : transform;
        Gizmos.matrix = root.localToWorldMatrix;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize.x, areaSize.y, 0f));

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(
            Mathf.Max(0f, areaSize.x - edgePadding * 2f),
            Mathf.Max(0f, areaSize.y - edgePadding * 2f), 0f));
    }

    // ---------- gameplay ----------

    private void StartDrag(Blob start)
    {
        _hasLastPointer = false;
        _brushTarget = 0f;
        
        _active = start.pair;
        _target = _active.Other(start);

        // the brush "teleports" to the blob you pressed, then follows the cursor
        MoveBrush(_active.brush, start.t.position);
        _active.brush.BeginDrag();     // resets the previous stroke of this color
    }

    private void EndDrag()
    {
        _active.brush.EndDrag(!clearFailedStrokeOnRelease);
        _active = null;
        _target = null;
    }

    private void Complete()
    {
        MoveBrush(_active.brush, _target.t.position);   // snap the end of the stroke onto the blob
        _active.brush.EndDrag(true);
        _active.brush.Lock();
        _active.done = true;
        Play(connectSound, connectVolume);
        _active = null;
        _target = null;

        score += pointsPerPair;
        _completed++;
        if (_completed >= _pairs.Count) FinishRound();
    }

    private Blob FindBlobAt(Vector2 p)
    {
        foreach (Blob b in _blobs)
            if (!b.pair.done && IsOver(b, p)) return b;
        return null;
    }

    private bool IsOver(Blob b, Vector2 p)
    {
        Vector2 c = b.t.position;
        return (c - p).sqrMagnitude <= blobRadius * blobRadius;
    }

    private static void MoveBrush(paint brush, Vector2 p)
    {
        Transform t = brush.transform;
        t.position = new Vector3(p.x, p.y, t.position.z);
    }

    // ---------- helpers ----------

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static bool IsIdentity(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] != i) return false;
        return true;
    }
}