using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;
        if (!rb.TryGetComponent(out Pill pill)) return;

        // let go of it first if the player is holding it, so the pickup script doesn't keep a dead reference
        PickupController pickup = FindFirstObjectByType<PickupController>();
        if (pickup != null && pickup.Held == rb) pickup.ForgetHeld();

        Destroy(rb.gameObject);
    }
}
