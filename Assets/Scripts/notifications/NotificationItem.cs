using TMPro;
using UnityEngine;

/// <summary>
/// Lives on the notification prefab. Shows one notification and animates itself:
/// slides in from the left, glides to whatever slot the manager gives it,
/// slides back out and destroys itself when dismissed.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class NotificationItem : MonoBehaviour
{
    [Header("Prefab references")]
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;

    [Header("Animation")]
    [SerializeField] float smoothTime = 0.15f;   // lower = snappier
    [SerializeField] float hiddenMargin = 50f;   // extra distance past the screen edge when hidden

    RectTransform rt;
    Vector2 velocity;
    float targetY;
    bool placed;    // has it received its first slot yet?
    bool leaving;

    /// <summary>Height used by the manager to stack items.</summary>
    public float Height => rt.rect.height;

    /// <summary>Unscaled time at which the manager dismisses it. 0 = stays until dismissed.</summary>
    public float ExpireTime { get; set; }

    float HiddenX => -(rt.rect.width + hiddenMargin);

    void Awake()
    {
        rt = (RectTransform)transform;

        // Anchor to the container's top-left so the manager can stack by simple offsets.
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);

        // Start off-screen to the left
        rt.anchoredPosition = new Vector2(HiddenX, 0f);
    }

    public void SetText(string title, string description)
    {
        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description;
    }

    /// <summary>Called by the manager. y is the offset from the top of the stack (0 or negative).</summary>
    public void SetSlot(float y)
    {
        targetY = y;

        if (!placed)
        {
            // First slot: jump straight to the right height so it slides in sideways only
            placed = true;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }
    }

    /// <summary>Slides out, then destroys itself.</summary>
    public void Hide() => leaving = true;

    void Update()
    {
        Vector2 target = new Vector2(leaving ? HiddenX : 0f, targetY);

        // Unscaled time: the game slows down with the dose meter, the UI shouldn't
        rt.anchoredPosition = Vector2.SmoothDamp(
            rt.anchoredPosition, target, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        if (leaving && Mathf.Abs(rt.anchoredPosition.x - HiddenX) < 1f)
            Destroy(gameObject);
    }
}
