using System.Collections.Generic;
using UnityEngine;

public class dashingLad : minigameController
{
    private enum Phase { Idle, Running, Stopping, Finished }

    private class Segment
    {
        public GameObject go;
        public Rigidbody2D rb;
        public float startX;   // where it started, to measure the distance it has scrolled
    }

    [Header("Sounds")]
    [SerializeField] private AudioClip fallSound;
    [SerializeField, Range(0f, 1f)] private float fallVolume = 1f;
    
    [Header("Segments")]
    [SerializeField] private GameObject[] segmentPrefabs;      // picked at random
    [SerializeField] private GameObject[] startingSegments;    // optional: objects already in the scene (the first rooftop) that scroll away too
    [SerializeField] public int segmentCount = 20;            // how many segments are spawned before the game ends
    [SerializeField] private float scrollDistance = 40f;       // how far a segment scrolls before it is deleted
    [SerializeField] private Vector2 heightRange = new Vector2(-1f, 1f);    // random height offset from this object's Y (min, max)
    [SerializeField] private Vector2 extraGapRange = new Vector2(0f, 3f);   // random gap added on top of the trigger's width (min, max)
    [SerializeField] private bool gapsGrowWithSpeed = true;    // the extra gap is 0 at the start speed and reaches its full range at the max speed
    [SerializeField] private Collider2D triggerCollider;       // optional: the trigger collider on this object is found automatically
    
    [Header("Scrolling speed")]
    [SerializeField] public float startSpeed = 1f;            // units per second at the start
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float acceleration = 0.15f;       // units per second, per second
    [SerializeField] private float stopDuration = 3f;          // how long the slow stop takes at the end

    public float ScrollSpeed => _speed;       // e.g. for parallax backgrounds or the run animation
    public bool IsFinished => _phase == Phase.Finished;

    private readonly List<Segment> _segments = new List<Segment>();
    private readonly HashSet<Collider2D> _inside = new HashSet<Collider2D>();   // colliders of the last segment that are inside the trigger
    private Segment _last;                    // the most recently spawned segment
    private Phase _phase = Phase.Idle;
    private float _speed;
    private float _stopRate;
    private int _spawned;
    private bool _end;
    private bool _failed;

    public void StartGame()
    {
        if (triggerCollider == null)
        {
            foreach (Collider2D c in GetComponents<Collider2D>())
            {
                if (c.isTrigger) { triggerCollider = c; break; }
            }
        }

        if (triggerCollider == null || segmentPrefabs == null || segmentPrefabs.Length == 0)
        {
            Debug.LogWarning("dashingLad: needs a trigger collider on this object and at least one segment prefab", this);
            return;
        }

        foreach (GameObject go in startingSegments) if (go != null) Register(go);

        _speed = startSpeed;
        _phase = Phase.Running;
        SpawnSegment();
    }

    public void End(bool fail = false)
    {
        if (_end) return;
        _end = true;
        _failed = fail;
        if (fail && fallSound != null) SFXManager.instance.PlaySFX(fallSound, transform, fallVolume);
        if (fail) AssignmentManager.Instance.Worsen("PE");
        else AssignmentManager.Instance.Improve("PE");
        EndGame();
    }
    
    //Spawning
    private void SpawnSegment()
    {
        GameObject prefab = segmentPrefabs[UnityEngine.Random.Range(0, segmentPrefabs.Length)];
        if (prefab == null) return;

        float gapScale = gapsGrowWithSpeed ? Mathf.InverseLerp(startSpeed, maxSpeed, _speed) : 1f;
        float gap = UnityEngine.Random.Range(extraGapRange.x, extraGapRange.y) * gapScale;
        float y = transform.position.y + UnityEngine.Random.Range(heightRange.x, heightRange.y);

        // Its left edge goes just right of the trigger (plus the random gap), so it scrolls in through the trigger
        float leftEdge = triggerCollider.bounds.max.x + gap;
        Vector3 pos = new Vector3(leftEdge, y, transform.position.z);

        // as a child of this object, so it ends up in THIS scene (Instantiate alone uses the active scene)
        GameObject go = Instantiate(prefab, pos, prefab.transform.rotation, transform);

        // The pivot can be anywhere in the prefab: shift it so its left EDGE is at the wanted spot
        if (TryGetBounds(go, out Bounds b)) go.transform.position += Vector3.right * (leftEdge - b.min.x);

        _inside.Clear();
        _last = Register(go);
        _spawned++;
    }

    private Segment Register(GameObject go)
    {
        if (!go.TryGetComponent(out Rigidbody2D rb)) rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;                   // moved by velocity, no gravity, can carry the player
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;   // smooth scrolling between physics steps

        var segment = new Segment { go = go, rb = rb, startX = go.transform.position.x };
        _segments.Add(segment);
        return segment;
    }

    private static bool TryGetBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    //The next segment spawns when the last one has completely left the trigger
    private bool IsLast(Collider2D other) =>
        _last != null && _last.go != null && other.transform.IsChildOf(_last.go.transform);

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsLast(other)) _inside.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsLast(other)) return;
        if (!_inside.Remove(other) || _inside.Count > 0) return;   // some of its colliders are still inside

        if (_phase != Phase.Running) return;
        if (_spawned >= segmentCount) EndGame();
        else SpawnSegment();
    }

    //Scrolling
    protected override void MgFixedUpdate()
    {
        if (_phase == Phase.Idle || _phase == Phase.Finished) return;

        float dt = Time.fixedDeltaTime;

        if (_phase == Phase.Running) _speed = Mathf.Min(maxSpeed, _speed + acceleration * dt);
        else _speed = Mathf.MoveTowards(_speed, 0f, _stopRate * dt);   // Stopping: slow down to a standstill

        for (int i = _segments.Count - 1; i >= 0; i--)
        {
            Segment s = _segments[i];
            if (s.go == null) { _segments.RemoveAt(i); continue; }

            // The newest segment is never deleted, or the chain would stall if it was removed before leaving the trigger
            if (s != _last && s.startX - s.go.transform.position.x >= scrollDistance)
            {
                Destroy(s.go);
                _segments.RemoveAt(i);
                continue;
            }

            s.rb.linearVelocity = new Vector2(-_speed, 0f);   // 'velocity' before Unity 6
        }

        if (_phase == Phase.Stopping && _speed <= 0f) FinishGame();
    }

    //Ending
    // Stops spawning and slows everything down to a standstill. Can be called from other scripts too.
    public void EndGame()
    {
        if (_phase != Phase.Running) return;

        _phase = Phase.Stopping;
        _stopRate = _speed / Mathf.Max(stopDuration, 0.01f);
        // ANIMATION: the player slows down / the scenery eases to a halt
        End();
    }

    private void FinishGame()
    {
        _phase = Phase.Finished;
        // ANIMATION: results / finish screen
        ClassroomInput.Instance.CloseMinigame(!_failed);
    }
}