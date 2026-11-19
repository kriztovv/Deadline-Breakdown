using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PickupController : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private Transform holdArea;
    [SerializeField] private Collider playerCollider;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip throwSound;

    [Header("Targeting")]
    [SerializeField] private float pickupRange = 5f;
    [SerializeField] private float grabRadius = 0.15f;      // thickness of the ray
    [SerializeField] private LayerMask pickupMask = ~0;     // set to your pickup layer if you like

    [Header("Crosshair")]
    [SerializeField] private RectTransform crosshair;
    [SerializeField] private Image crosshairImage;
    [SerializeField] private float normalSize = 8f;
    [SerializeField] private float hoverSize = 18f;
    [SerializeField] private float heldSize = 12f;
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.6f);
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private float crosshairLerp = 14f;

    [Header("Physics Parameters")]
    [SerializeField] private float followSpeed = 14f;
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float rotateSpeed = 10f;
    [SerializeField] private float throwForce = 8f;
    [SerializeField] private float dropSpeedLimit = 3f;
    [SerializeField] private float swoopTime = 0.25f;

    [Header("Held Pose")]
    [SerializeField] private Vector3 heldEulerOffset = Vector3.zero;
    [SerializeField] private bool faceCamera = true;
    
    [Header("Rotation While Held")]
    [SerializeField] private float rotateSensitivity = 0.3f;
    [SerializeField] private bool resetRotationOnPickup = true;

    private Quaternion userRotation = Quaternion.identity; // rotation you add on top of the upright pose
    public bool IsRotating { get; private set; }

    public Rigidbody heldRB;
    private Collider[] heldColliders;
    private float heldTimer;
    private Rigidbody hoveredRB;
    public bool holding;
    
    public Rigidbody Held => heldRB;

    private void Update()
    {
        if (Mouse.current == null) return;

        hoveredRB = heldRB == null ? FindTarget() : null;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (heldRB == null) TryPickup(hoveredRB);
            else Drop(false);
        }

        if (heldRB != null && Mouse.current.rightButton.wasPressedThisFrame)
            Drop(true);
        
        IsRotating = heldRB != null && Keyboard.current != null && Keyboard.current.rKey.isPressed;

        if (IsRotating)
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * rotateSensitivity;

            // yaw around world up, pitch around the camera's right axis
            userRotation = Quaternion.AngleAxis(-delta.x, Vector3.up)
                           * Quaternion.AngleAxis(delta.y, transform.right)
                           * userRotation;
        }

        UpdateCrosshair();
    }

    private void FixedUpdate()
    {
        if (heldRB != null) MoveHeld();
    }

    // SphereCast is a "fat" raycast, so small objects are much easier to hit
    private Rigidbody FindTarget()
    {
        if (Physics.SphereCast(transform.position, grabRadius, transform.forward,
                out RaycastHit hit, pickupRange, pickupMask, QueryTriggerInteraction.Ignore))
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb != null && !rb.isKinematic) return rb;
        }
        return null;
    }
    
    public void ForgetHeld()
    {
        heldRB = null;
    }

    private void UpdateCrosshair()
    {
        if (crosshair == null) return;

        float targetSize = heldRB != null ? heldSize : (hoveredRB != null ? hoverSize : normalSize);
        Color targetColor = hoveredRB != null || heldRB != null ? hoverColor : normalColor;

        float size = Mathf.Lerp(crosshair.sizeDelta.x, targetSize, crosshairLerp * Time.deltaTime);
        crosshair.sizeDelta = new Vector2(size, size);

        if (crosshairImage != null)
            crosshairImage.color = Color.Lerp(crosshairImage.color, targetColor, crosshairLerp * Time.deltaTime);
    }

    private void TryPickup(Rigidbody rb)
    {
        if (rb == null) return;
        if (resetRotationOnPickup) userRotation = Quaternion.identity;
        
        heldRB = rb;
        heldTimer = 0f;
        heldRB.useGravity = false;
        heldRB.linearDamping = 4f;
        heldRB.angularDamping = 8f;
        heldRB.interpolation = RigidbodyInterpolation.Interpolate;

        //SFXManager.instance.PlaySFX(pickupSound, transform, 0.5f);
        
        heldColliders = heldRB.GetComponentsInChildren<Collider>();
        if (playerCollider != null)
            foreach (var c in heldColliders)
                Physics.IgnoreCollision(c, playerCollider, true);
    }

    private void MoveHeld()
    {
        heldTimer += Time.fixedUnscaledDeltaTime;
        float ease = Mathf.SmoothStep(0f, 1f, heldTimer / swoopTime);

        Vector3 toTarget = holdArea.position - heldRB.position;
        Vector3 vel = Vector3.ClampMagnitude(toTarget * followSpeed, maxSpeed) * Mathf.Lerp(0.4f, 1f, ease);
        heldRB.linearVelocity = vel;

        Quaternion basePose;
        if (faceCamera)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.001f) flatForward = -transform.forward;
            basePose = Quaternion.LookRotation(flatForward, Vector3.up) * Quaternion.Euler(heldEulerOffset);
        }
        else
        {
            basePose = Quaternion.Euler(heldEulerOffset);
        }

        Quaternion target = userRotation * basePose;

        heldRB.MoveRotation(Quaternion.Slerp(heldRB.rotation, target, rotateSpeed * Time.fixedDeltaTime));
        heldRB.angularVelocity = Vector3.zero;
    }

    private void Drop(bool throwIt)
    {
        heldRB.useGravity = true;
        heldRB.linearDamping = 0f;
        heldRB.angularDamping = 0.05f;

        if (throwIt)
        {
            heldRB.linearVelocity = transform.forward * throwForce;
            heldRB.angularVelocity = Random.insideUnitSphere * 4f;
            SFXManager.instance.PlaySFX(throwSound, transform, 0.5f);
        }
        else
        {
            heldRB.linearVelocity = Vector3.ClampMagnitude(heldRB.linearVelocity, dropSpeedLimit);
        }

        if (playerCollider != null)
            foreach (var c in heldColliders)
                if (c != null) Physics.IgnoreCollision(c, playerCollider, false);

        heldRB = null;
    }
}