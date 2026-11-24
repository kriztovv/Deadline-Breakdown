using UnityEngine;
using UnityEngine.InputSystem;

public class RadiatorAction : RoomAction
{
    [Header("References")]
    [SerializeField] Transform player;
    [Tooltip("Optional. Range is measured from here. Defaults to this object if empty.")]
    [SerializeField] Transform doorPoint;
    [SerializeField] GameObject prompt;                 // the actionText "Press E" UI object
    [SerializeField] ShaderPulseHighlight highlight;

    [Header("Settings")]
    [SerializeField] float interactRange = 2.5f;

    Transform Point => doorPoint != null ? doorPoint : transform;

    void Start()
    {
        if (prompt != null) prompt.SetActive(false);
    }

    protected override void OnBegin()
    {
        if (highlight != null) highlight.SetActive(true);
    }

    void Update()
    {
        if (!IsActive || player == null || Keyboard.current == null) return;

        if (AssignmentManager.Dead)
        {
            SetPrompt(false);
            return; // no interacting while dead
        }

        // Flat distance, ignoring height
        Vector3 a = player.position, b = Point.position;
        a.y = 0f; b.y = 0f;
        bool near = Vector3.Distance(a, b) < interactRange;

        SetPrompt(near);

        if (near && Keyboard.current.eKey.wasPressedThisFrame)
            Complete();
    }

    protected override void OnEnd()
    {
        SetPrompt(false);
        if (highlight != null) highlight.SetActive(false);
    }

    protected override void OnFailed()
    {
        // custom stuff first
        base.OnFailed();
    }

    void SetPrompt(bool on)
    {
        if (prompt != null && prompt.activeSelf != on)
            prompt.SetActive(on);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Point.position, interactRange);
    }
}