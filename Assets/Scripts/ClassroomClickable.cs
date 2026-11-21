using UnityEngine;
using UnityEngine.Events;

public class ClassroomClickable : MonoBehaviour
{
    public UnityEvent onClick;
    public bool Interactable { get; set; } = true;

    [SerializeField] private Transform visuals;      // child that scales on hover (not the one with the collider)
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float hoverSpeed = 12f;

    private Vector3 baseScale;
    private bool hovered;

    private void Awake() { if (visuals == null) visuals = transform; baseScale = visuals.localScale; }
    private void OnEnable() => hovered = false;

    private void Update()
    {
        Vector3 target = baseScale * (hovered ? hoverScale : 1f);
        visuals.localScale = Vector3.Lerp(visuals.localScale, target, hoverSpeed * Time.unscaledDeltaTime);
    }

    public void SetHovered(bool value) => hovered = value;
    public void Click() => onClick.Invoke();
}
