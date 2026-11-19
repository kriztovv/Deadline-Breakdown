using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Rigidbody2D))]
public class bugChaseLucky : MonoBehaviour
{
    private enum State { Approach, Charge, Lunge, Retreat }

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer sprite;          // found automatically if left empty
    [SerializeField] private bool spriteFacesRight = true;   // which way the sprite looks in the art as drawn
    
    [Header("Movement")]
    [SerializeField] private float approachSpeed = 1.5f;
    [SerializeField] private float retreatSpeed = 3f;
    [SerializeField] private float steering = 8f;
    [SerializeField] private bool horizontalOnly;
    [SerializeField] private float recoverTime = 1.5f;     // idle time after being thrown or launched

    [Header("Attack")]
    [SerializeField] private float chargeDistance = 2.5f;
    [SerializeField] private float chargeTime = 0.8f;
    [SerializeField] private float lungeSpeed = 12f;
    [SerializeField] private float lungeTime = 0.4f;
    [SerializeField] private float lungeUpward = 0.5f;     // 0 = straight at Lucky, higher = the jump leans more upwards
    [SerializeField] private float retreatTime = 5f;
    [SerializeField] private GameObject errorBox;
    [SerializeField] private bugfix bugFixScript;
    
    [Header("Sounds")]
    [SerializeField] private AudioClip walkLoopSound;
    [SerializeField, Range(0f, 1f)] private float walkVolume = 0.5f;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 1f;

    private AudioSource _walkSource;
    private bool _silenced;
    
    private Rigidbody2D _rb;
    private Transform _lucky;
    private State _state;
    private float _stateEnd;
    private Vector2 _lungeDirection;
    private float _pausedUntil;
    private bool _wasHeld;
    private Animator animator;
    private BoxCollider2D _collider;

    private void Awake() {
        _rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
        
        _walkSource = gameObject.AddComponent<AudioSource>();
        _walkSource.clip = walkLoopSound;
        _walkSource.loop = true;
        _walkSource.playOnAwake = false;
        _walkSource.spatialBlend = 0f;
        _walkSource.volume = walkVolume;
    }
    
    private void Face(float dirX)
    {
        if (sprite == null || Mathf.Abs(dirX) < 0.05f) return;   // deadzone, so it doesn't flicker when Lucky is right above
        bool wantsRight = dirX > 0f;
        sprite.flipX = wantsRight != spriteFacesRight;
    }

    private void Start()
    {
        FindLucky();
        SetState(State.Approach);
        _collider = GetComponent<BoxCollider2D>();
    }

    private void FindLucky()
    {
        GameObject go = GameObject.Find("Lucky");
        _lucky = go != null ? go.transform : null;
    }

    // Call this after the bug gets launched, so it stays idle for 'recoverTime'
    public void Stun()
    {
        Interrupt();
        _pausedUntil = Time.time + recoverTime;
    }

    // A grab or a launch cancels a charge-up or a jump in progress
    private void Interrupt()
    {
        if (_state == State.Charge || _state == State.Lunge) SetState(State.Approach);
    }

    private void FixedUpdate()
    {
        // Held = minigame0 has put a TargetJoint2D on it while dragging
        bool held = TryGetComponent<TargetJoint2D>(out _);
        if (held && !_wasHeld) Interrupt();                              // grabbed
        if (!held && _wasHeld) _pausedUntil = Time.time + recoverTime;   // let go (thrown): wait before moving again
        _wasHeld = held;

        bool idle = held || Time.time < _pausedUntil;
        SetWalkSound(!idle && !_silenced && (_state == State.Approach || _state == State.Retreat));
        if (idle) return; 

        if (_lucky == null) { FindLucky(); if (_lucky == null) return; }

        Vector2 toLucky = (Vector2)_lucky.position - _rb.position;
        Vector2 dir = Direction(toLucky);

        switch (_state)
        {
            case State.Approach:
                Face(toLucky.x);
                Steer(dir * approachSpeed);
                if (toLucky.magnitude <= chargeDistance){
                    SetState(State.Charge);
                }
                break;

            case State.Charge:
                Face(toLucky.x);          // keeps tracking Lucky while winding up
                Steer(Vector2.zero);
                if (Time.time >= _stateEnd)
                {
                    _lungeDirection = (toLucky.normalized + Vector2.up * lungeUpward).normalized;
                    SetState(State.Lunge);
                }
                break;

            case State.Lunge:
                Face(_lungeDirection.x);  // the way it jumps
                if (Time.time >= _stateEnd) SetState(State.Approach);
                break;

            case State.Retreat:
                Face(-toLucky.x);         // runs away, so it faces away from Lucky
                Steer(-dir * retreatSpeed);
                if (Time.time >= _stateEnd) SetState(State.Approach);
                break;
        }
    }

    private void SetState(State next)
    {
        _state = next;

        switch (next)
        {
            case State.Approach:
                // ANIMATION: walking / crawling loop
                animator.SetBool("walking", true);
                break;

            case State.Charge:
                _stateEnd = Time.time + chargeTime;
                // ANIMATION: charge-up (crouch, shake, glowing eyes), turn to face Lucky
                animator.SetBool("walking", false);
                animator.SetTrigger("jump");
                break;

            case State.Lunge:
                _stateEnd = Time.time + lungeTime;
                _rb.linearVelocity = _lungeDirection * lungeSpeed;   // 'velocity' before Unity 6
                if (gameObject.transform.localPosition.y <= errorBox.transform.localPosition.y)
                {
                    _collider.enabled = false;
                    StartCoroutine(EnableCollider());
                }
                if (jumpSound != null) SFXManager.instance.PlaySFX(jumpSound, transform, jumpVolume);
                else
                {
                    Debug.Log("Local: "  + gameObject.transform.localPosition.y + ", Box: " + errorBox.transform.localPosition.y);
                }
                // ANIMATION: jump (stretch, motion trail, whoosh sound)
                break;

            case State.Retreat:
                _stateEnd = Time.time + retreatTime;
                // ANIMATION: hit reaction, then a scared run-away loop
                animator.SetBool("walking", true);
                break;
        }
    }

    private void SetWalkSound(bool on)
    {
        if (_walkSource == null || walkLoopSound == null) return;
        if (on && !_walkSource.isPlaying) _walkSource.Play();
        else if (!on && _walkSource.isPlaying) _walkSource.Stop();
    }

    // called when the bug dies, so the walking loop doesn't continue under the death sound
    public void StopSounds()
    {
        _silenced = true;
        SetWalkSound(false);
    }

    private void OnDisable() => SetWalkSound(false);
    
    private IEnumerator EnableCollider()
    {
        yield return new WaitForSeconds(0.2f);
        _collider.enabled = true;
    }
    
    private void OnCollisionEnter2D(Collision2D c) => TryHit(c.collider.transform);
    private void OnTriggerEnter2D(Collider2D c) => TryHit(c.transform);

    private void TryHit(Transform other)
    {
        if (_state != State.Lunge || _lucky == null) return;
        if (!other.IsChildOf(_lucky)) return;

        // ANIMATION: impact (flash, particles, Lucky's reaction)
        if (_lucky.TryGetComponent(out luckyRightclick lucky)) lucky.Hurt();
        else _lucky.GetComponent<Animator>().SetTrigger("hurt");
        bugFixScript.perfect = false;
        AssignmentManager.Instance.Worsen("IT");
        SetState(State.Retreat);
    }

    private void Steer(Vector2 desiredVelocity)
    {
        Vector2 error = desiredVelocity - _rb.linearVelocity;
        if (horizontalOnly) error.y = 0f;
        _rb.AddForce(error * (_rb.mass * steering));
    }

    private Vector2 Direction(Vector2 v)
    {
        if (horizontalOnly) v.y = 0f;
        return v.normalized;
    }
}