using UnityEngine;

public class PlayerPush : MonoBehaviour
{
    [SerializeField] private float pushSpeed = 3f;     // how fast props get shoved
    [SerializeField] private float maxPushMass = 15f;  // heavier things won't move
    [SerializeField] private float massInfluence = 1f; // 0 = mass ignored

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody rb = hit.collider.attachedRigidbody;
        if (rb == null || rb.isKinematic) return;
        if (rb.mass > maxPushMass) return;

        // don't shove things you are standing on
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 dir = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
        if (dir.sqrMagnitude < 0.001f) return;
        dir.Normalize();

        // lighter objects get pushed faster
        float massFactor = Mathf.Lerp(1f, 1f / Mathf.Max(rb.mass, 0.1f), massInfluence);
        float speed = Mathf.Min(pushSpeed * massFactor, pushSpeed * 2f);

        rb.linearVelocity = new Vector3(dir.x * speed, rb.linearVelocity.y, dir.z * speed);
    }
}
