using UnityEngine;

// Added to the projectile prefab automatically by geography if it's missing
[RequireComponent(typeof(Rigidbody2D))]
public class geographyProjectile : MonoBehaviour
{
    public bool Resolved { get; set; }   // already counted, so it can't count twice

    private geography _owner;
    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.linearDamping = 0f;   // constant speed
    }

    public void Launch(geography owner, Vector2 velocity)
    {
        _owner = owner;
        _rb.linearVelocity = velocity;   // physics velocity, so it follows timeScale
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_owner != null) _owner.ProjectileTouched(this, other);
    }

    private void OnCollisionEnter2D(Collision2D c)   // in case the projectile's collider isn't a trigger
    {
        if (_owner != null) _owner.ProjectileTouched(this, c.collider);
    }
}