using UnityEngine;

public class minigamePlayerControlled : MonoBehaviour
{
    [SerializeField] private minigameController controller;   // optional: found automatically in this scene if left empty
    [SerializeField] private float speed = 3f;                // units per second
    [SerializeField] private float acceleration = 30f;        // how quickly it reaches full speed (and stops)
    [SerializeField] private bool flipToFaceDirection = true;

    private Animator _animator;

    private Rigidbody2D _rb;
    private bool _wasMoving;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        if (controller == null) controller = FindController();
        if (controller == null) Debug.LogWarning("minigamePlayerControlled: no minigameController found in this scene", this);
    }

    // Looks through this object's own scene, so it works for prefabs spawned at runtime too
    private minigameController FindController()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<minigameController>(true);
            if (found != null) return found;
        }
        return null;
    }

    // Without a Rigidbody2D, the object is moved directly
    private void Update()
    {
        if (controller == null || _rb != null) return;

        Vector2 input = controller.MovementInput;
        transform.position += (Vector3)(input * speed * controller.MovementDeltaTime);
        UpdateVisuals(input);
    }

    // With a Rigidbody2D, the velocity is steered, so collisions and gravity keep working
    private void FixedUpdate()
    {
        if (controller == null || _rb == null) return;
        if (!controller.HorizontalMovement && !controller.VerticalMovement) return;

        // Physics steps run on scaled time. To ignore the time scale, work in real-time velocity
        // and convert at the end (the floor keeps it finite in extreme slow motion)
        float scale = controller.MovementIgnoresTimeScale ? Mathf.Max(Time.timeScale, 0.05f) : 1f;

        Vector2 input = controller.MovementInput;
        Vector2 velocity = _rb.linearVelocity * scale;   // 'velocity' before Unity 6
        float step = acceleration * controller.MovementDeltaTime;

        // Only the enabled axes are touched, so gravity stays in charge of the other one
        if (controller.HorizontalMovement) velocity.x = Mathf.MoveTowards(velocity.x, input.x * speed, step);
        if (controller.VerticalMovement)   velocity.y = Mathf.MoveTowards(velocity.y, input.y * speed, step);

        if (controller.HorizontalMovement && Mathf.Abs(input.x) < 0.01f )
        {
            _animator.SetBool("isRunning", false);
        }
        else
        {
            _animator.SetBool("isRunning", true);
        }


        _rb.linearVelocity = velocity / scale;
        UpdateVisuals(input);
    }

    private void UpdateVisuals(Vector2 input)
    {
        bool moving = input.sqrMagnitude > 0.01f;
        if (moving != _wasMoving)
        {
            _wasMoving = moving;
            // ANIMATION: switch between the idle loop and the walk cycle
        }

        if (flipToFaceDirection && Mathf.Abs(input.x) > 0.01f)
        {
            // ANIMATION: turn to face the direction of travel (or set an animator parameter instead)
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * Mathf.Sign(input.x);
            transform.localScale = s;
        }
    }
}