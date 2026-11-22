using UnityEngine;

public class OwnedUpgradeSlot : MonoBehaviour
{
    public SpriteRenderer icon;          // the child sprite that shows the upgrade's icon
    [SerializeField] private float popTime = 0.25f;

    private float t;
    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        if (t >= 1f) return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / popTime);
        float k = 1f + 0.2f * Mathf.Sin(t * Mathf.PI);          // slight overshoot
        transform.localScale = baseScale * Mathf.SmoothStep(0f, 1f, t) * k;
    }
}