using System.Collections;
using UnityEngine;

public class molecule : MonoBehaviour
{
    [Header("Color")]
    [SerializeField] private Color moleculeColor = Color.white;   // molecules with the same color chain together
    [SerializeField] private bool tintSprite = true;              // also applies the color to the SpriteRenderer

    [Header("Chain reaction")]
    [SerializeField] private float delay = 0.25f;                 // X: seconds to wait before it reacts
    [SerializeField] private CapsuleCollider2D neighbourTrigger;  // optional: the trigger capsule on this object is found automatically
    [SerializeField] private LayerMask neighbourLayers = ~0;

    private ContactFilter2D _filter;
    private readonly Collider2D[] _hits = new Collider2D[32];
    private bool _popping;
    
    [Header("Custom gravity")]
    [SerializeField] private bool gravityOn = false;
    [SerializeField] private Vector2 gravityPoint;           // world position
    [SerializeField] private Transform gravityTarget;        // optional, overrides gravityPoint if set
    [SerializeField] private float gravityStrength = 20f;    // acceleration, like 9.81 for normal gravity

    private Rigidbody2D _rb;

    [Header("Shot (the molecule the player fires)")]
    [SerializeField] private float steerSpeed = 3f;      // up/down speed while steering
    [SerializeField] private float settleTime = 2f;      // X: seconds after the first contact in which a same-colored contact pops it
    [SerializeField] private float maxFlightTime = 8f;   // a shot that hits nothing is removed after this

    public event System.Action<molecule> ShotFinished;   // popped, settle timer ran out, or flew away

    private enum ShotState { None, Waiting, Flying, Settling }
    private ShotState _shotState = ShotState.None;
    private bool _isShot;
    private Vector2 _shotVelocity;
    private float _steer;
    
    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (_rb != null) _rb.gravityScale = 0f;   // built-in gravity off, we apply our own
        else Debug.LogWarning("molecule: no Rigidbody2D found on this object", this);
        
        if (neighbourTrigger == null)
        {
            foreach (CapsuleCollider2D c in GetComponents<CapsuleCollider2D>())
            {
                if (c.isTrigger) { neighbourTrigger = c; break; }
            }
        }
        if (neighbourTrigger == null) Debug.LogWarning("molecule: no trigger CapsuleCollider2D found on this object", this);

        _filter = new ContactFilter2D();
        _filter.SetLayerMask(neighbourLayers);
        _filter.useTriggers = true;   // the neighbours' colliders may be triggers too

        if (tintSprite)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.color = moleculeColor;
        }
    }

    // Starts the chain reaction from this molecule
    public void Pop(bool wait = true)   // wait = false pops right away, without the delay
    {
        if (_popping) return;
        _popping = true;
        StartCoroutine(PopRoutine(wait));
    }

    private IEnumerator PopRoutine(bool wait)
    {
        // ANIMATION: the molecule starts to shake or glow while it waits
        if (wait) yield return new WaitForSeconds(delay);

        // The neighbours are the other molecules overlapping the trigger capsule
        if (neighbourTrigger != null)
        {
            int count = neighbourTrigger.Overlap(_filter, _hits);
            for (int i = 0; i < count; i++)
            {
                molecule other = _hits[i].GetComponentInParent<molecule>();
                if (other == null || other == this) continue;

                if (SameColor(other)) other.Pop();   // the ones that are already reacting ignore it
            }
        }

        // ANIMATION: pop effect (particles, sound)
        Destroy(gameObject);
    }

    private bool SameColor(molecule other)
    {
        Color a = moleculeColor, b = other.moleculeColor;
        return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;   // alpha is ignored
    }
    
    private void FixedUpdate()   // follows timeScale like the rest of the world
    {
        if (_shotState == ShotState.Flying)
        {
            // straight line in the shot direction, W/S adds up/down speed on top
            _rb.linearVelocity = _shotVelocity + Vector2.up * (_steer * steerSpeed);
            return;
        }

        if (!gravityOn || _rb == null) return;

        Vector2 target = gravityTarget != null ? (Vector2)gravityTarget.position : gravityPoint;
        Vector2 dir = target - _rb.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        _rb.AddForce(dir.normalized * gravityStrength * _rb.mass);
    }

    // mobliterator calls these
    public void PrepareAsShot(Transform gravityCenter)
    {
        _isShot = true;
        _shotState = ShotState.Waiting;
        gravityOn = false;
        gravityTarget = gravityCenter;           // it falls toward this after the first contact
        _rb.bodyType = RigidbodyType2D.Kinematic;   // stays put at the shoot point until fired
    }

    public void Launch(Vector2 direction, float speed)
    {
        if (_shotState != ShotState.Waiting) return;   // already fired
        _shotState = ShotState.Flying;
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _shotVelocity = direction.normalized * speed;
        StartCoroutine(FlightRoutine());
    }

    public void Steer(float input) { _steer = Mathf.Clamp(input, -1f, 1f); }

    private IEnumerator FlightRoutine()
    {
        yield return new WaitForSeconds(maxFlightTime);
        if (_shotState != ShotState.Flying) yield break;   // it hit something in time

        FinishShot();
        // ANIMATION: fade out
        Destroy(gameObject);
    }

    private void BeginSettle()
    {
        _shotState = ShotState.Settling;
        gravityOn = true;   // we stop setting the velocity, so it keeps whatever momentum it has and gravity takes over
        StartCoroutine(SettleRoutine());
    }

    private IEnumerator SettleRoutine()
    {
        yield return new WaitForSeconds(settleTime);
        if (_shotState == ShotState.Settling) FinishShot();   // no match: it stays in the mass as a normal molecule
    }

    private void FinishShot()
    {
        if (!_isShot) return;   // only fires once
        _isShot = false;
        _shotState = ShotState.None;
        ShotFinished?.Invoke(this);
    }

    private void ShotContact(Collision2D c)
    {
        molecule other = c.collider.GetComponentInParent<molecule>();
        if (other == null || other == this) return;   // walls etc. don't count

        if (_shotState == ShotState.Flying) BeginSettle();   // first contact
        if (_shotState == ShotState.Settling && SameColor(other)) Pop(false);   // no delay
    }
    
    // mobliterator will call these later
    public void SetGravity(bool on) { gravityOn = on; }
    public void SetGravity(bool on, Vector2 point) { gravityOn = on; gravityTarget = null; gravityPoint = point; }
    public void SetGravity(bool on, Transform target) { gravityOn = on; gravityTarget = target; }
}

#if UNITY_EDITOR
// The Inspector button. This block is left out of builds, so it doesn't need an Editor folder.
[UnityEditor.CustomEditor(typeof(molecule))]
public class moleculeEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        UnityEditor.EditorGUILayout.Space();
        using (new UnityEditor.EditorGUI.DisabledScope(!Application.isPlaying))   // popping uses a coroutine, so it needs Play mode
        {
            if (GUILayout.Button("Pop (test)")) ((molecule)target).Pop();
        }
    }
}
#endif