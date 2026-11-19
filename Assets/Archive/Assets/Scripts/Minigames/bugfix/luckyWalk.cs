using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class luckyWalk : MonoBehaviour
{
    private enum State { Idle, Walk }

    [SerializeField] private Transform center;                 // leave empty to use his starting position
    [SerializeField] private float walkSpeed = 1.2f;
    [SerializeField] private float steering = 10f;             // how quickly his speed follows the wanted speed
    [SerializeField] private float startWalkDistance = 0.3f;   // how far from the center before he walks back
    [SerializeField] private float stopDistance = 0.1f;        // how close counts as "there" (keep it below the start distance)

    private Rigidbody2D _rb;
    private Animator animator;
    private State _state;
    private float _homeX;

    private float CenterX => center != null ? center.position.x : _homeX;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        _homeX = transform.position.x;
    }

    private void FixedUpdate()
    {
        // While minigame0 drags him (it adds a TargetJoint2D), he stands still instead of fighting the cursor
        if (TryGetComponent<TargetJoint2D>(out _))
        {
            SetState(State.Idle);
            return;
        }

        float dx = CenterX - _rb.position.x;   // only the horizontal distance matters
        float dist = Mathf.Abs(dx);

        // Two distances (start and stop) so he doesn't flicker between walking and idle at the edge
        State wanted = _state == State.Walk
            ? (dist <= stopDistance ? State.Idle : State.Walk)
            : (dist > startWalkDistance ? State.Walk : State.Idle);
        SetState(wanted);

        // ANIMATION: face the direction of travel (flip the sprite, or set scale.x to the sign of dx)
        transform.localScale = new Vector3(Mathf.Sign(dx), 1f, 1f);
        // Only a horizontal force, so gravity keeps full control of the vertical movement
        float targetSpeed = _state == State.Walk ? Mathf.Sign(dx) * walkSpeed : 0f;
        float error = targetSpeed - _rb.linearVelocity.x;   // 'velocity' before Unity 6
        _rb.AddForce(new Vector2(error * _rb.mass * steering, 0f));
    }

    private void SetState(State next)
    {
        if (next == _state) return;
        _state = next;

        switch (next)
        {
            case State.Idle:
                // ANIMATION: idle loop (breathing, looking around)
                animator.SetBool("walking", false);
                break;

            case State.Walk:
                // ANIMATION: walk cycle
                animator.SetBool("walking", true);
                break;
        }
    }
}