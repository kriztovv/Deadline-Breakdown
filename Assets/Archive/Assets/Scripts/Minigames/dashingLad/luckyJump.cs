using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class luckyJump : MonoBehaviour
{
    private enum State { Idle, Walk, Run, Jump, Fall, Land }

    private const float MinTimeScale = 0.05f;   // keeps the compensation finite in extreme slow motion (physics doesn't run at 0)

    [Header("Sounds")]
    [SerializeField] private AudioClip jumpSound;
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 1f;
    
    [Header("Input")]
    [SerializeField] private KeyCode[] jumpKeys = { KeyCode.Space, KeyCode.W, KeyCode.UpArrow };
    [SerializeField] private bool jumpWithLeftClick = true;
    [SerializeField] private float jumpBufferTime = 0.12f;   // a press this shortly before landing still jumps
    [SerializeField] private float coyoteTime = 0.08f;       // a press this shortly after running off a roof still jumps

    [Header("Jump")]
    [Tooltip("On: gravity and jump ignore Time.timeScale (same feel in slow motion). Off: they slow down with the time scale.")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private float jumpSpeed = 12f;
    [SerializeField] private float gravity = 35f;            // units per second squared. Jump height = jumpSpeed² / (2 × gravity)
    [SerializeField] private float maxFallSpeed = 25f;
    [Tooltip("Upward speed a jump is cut to when the key is released early. Lower = shorter taps.")]
    [SerializeField] private float jumpCutSpeed = 4f;

    [Header("Ground")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundCheckDistance = 0.08f;

    [Header("Animation states")]
    [SerializeField] private dashingLad game;                // optional: found automatically in this scene
    [SerializeField] private float runSpeed = 4f;            // scroll speed from which walking becomes running
    [SerializeField] private float landTime = 0.15f;         // how long the landing state lasts

    public bool IsGrounded => _grounded;

    private Rigidbody2D _rb;
    private Animator _animator;
    private ContactFilter2D _groundFilter;
    private readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

    private State _state = State.Idle;
    private bool _grounded;
    private float _jumpBuffer;
    private float _coyote;
    private float _landTimer;

    private bool _jumpHeld;

    private bool IsJumpHeld()
    {
        foreach (KeyCode key in jumpKeys)
            if (Input.GetKey(key)) return true;
        return jumpWithLeftClick && Input.GetMouseButton(0);
    }
    
    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();

        _groundFilter = new ContactFilter2D();
        _groundFilter.SetLayerMask(groundLayers);
        _groundFilter.useTriggers = false;

        if (game == null) game = FindGame();
    }

    private dashingLad FindGame()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<dashingLad>(true);
            if (found != null) return found;
        }
        return null;
    }

    private void OnEnable()
    {
        MonitorInput.OnKeyDown += OnKey;
        MonitorInput.OnInteraction += OnInteraction;
    }

    private void OnDisable()
    {
        MonitorInput.OnKeyDown -= OnKey;
        MonitorInput.OnInteraction -= OnInteraction;
    }

    //Input
    private void OnKey(KeyCode key)
    {
        if (System.Array.IndexOf(jumpKeys, key) >= 0) QueueJump();
    }

    private void OnInteraction(MonitorInteraction interaction)
    {
        if (jumpWithLeftClick && interaction == MonitorInteraction.PrimaryDown) QueueJump();
    }

    private void QueueJump()
    {
        if (game != null && game.IsFinished) return;   // the minigame is over
        _jumpBuffer = jumpBufferTime;
    }

    //Physics, in real time
    private void FixedUpdate()
    {
        // Physics runs on scaled time. Everything below is worked out in real-time units,
        // and the velocity is divided by the scale when it is handed to the rigidbody
        float scale = useUnscaledTime ? Mathf.Max(Time.timeScale, MinTimeScale) : 1f;
        float vy = _rb.linearVelocity.y * scale;   // real units per second ('velocity' before Unity 6)

        _grounded = vy <= 1f && CheckGround();     // not while still rising from a jump
        _animator.SetBool("inAir", !_grounded);
        if (_grounded) _coyote = coyoteTime;

        if (_jumpBuffer > 0f && _coyote > 0f)
        {
            vy = jumpSpeed;
            _jumpBuffer = 0f;
            _coyote = 0f;
            _grounded = false;
            if (jumpSound != null) SFXManager.instance.PlaySFX(jumpSound, transform, jumpVolume);
        }
        else
        {
            // Released early while still rising: cut the jump short
            if (!_jumpHeld && vy > jumpCutSpeed) vy = jumpCutSpeed;

            vy -= gravity * Time.fixedDeltaTime / scale;
            vy = Mathf.Max(vy, -maxFallSpeed);
        }

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, vy / scale);
    }

    // Is there walkable ground right below his feet?
    private bool CheckGround()
    {
        int n = _rb.Cast(Vector2.down, _groundFilter, _hits, groundCheckDistance);
        for (int i = 0; i < n; i++)
        {
            if (_hits[i].normal.y > 0.5f) return true;   // a surface facing upwards, not a wall
        }
        return false;
    }

    //Animation states, in real time as well
    private void Update()
    {
        _jumpHeld = IsJumpHeld();

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _jumpBuffer -= dt;
        _coyote -= dt;
        _landTimer -= dt;

        UpdateState();
    }

    private void UpdateState()
    {
        State wanted;

        if (!_grounded)
        {
            wanted = _rb.linearVelocity.y > 0f ? State.Jump : State.Fall;   // rising or falling
        }
        else if (_state == State.Jump || _state == State.Fall)
        {
            wanted = State.Land;   // just touched the ground
            _landTimer = landTime;
        }
        else if (_state == State.Land && _landTimer > 0f)
        {
            wanted = State.Land;
        }
        else
        {
            // On the ground the scrolling speed decides: standing still, walking or running
            float speed = game != null ? game.ScrollSpeed : 0f;
            wanted = speed < 0.05f ? State.Idle : speed < runSpeed ? State.Walk : State.Run;
        }

        SetState(wanted);
    }

    private void SetState(State next)
    {
        if (next == _state) return;
        _state = next;

        switch (next)
        {
            case State.Idle:
                // ANIMATION / SPRITE: standing still (the scrolling has stopped)
                break;

            case State.Walk:
                // ANIMATION / SPRITE: walk cycle (slow scrolling)
                // You can scale the animation speed with game.ScrollSpeed here
                break;

            case State.Run:
                // ANIMATION / SPRITE: run cycle (fast scrolling)
                break;

            case State.Jump:
                // ANIMATION / SPRITE: jump, going up (stretch, sound)
                break;

            case State.Fall:
                // ANIMATION / SPRITE: falling, going down (after the peak, or after running off a roof)
                break;

            case State.Land:
                // ANIMATION / SPRITE: landing (squash, dust puff, thud sound), lasts 'Land Time' and then goes back to walk or run
                break;
        }
    }
}