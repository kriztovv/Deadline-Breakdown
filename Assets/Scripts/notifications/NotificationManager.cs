using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single place to show notifications. Any system can call
///     var n = NotificationManager.Instance.Push("Title", "Description");
///     NotificationManager.Instance.Dismiss(n);
/// Put this on one GameObject in the scene.
/// </summary>
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Default look. Used whenever a caller doesn't pass its own prefab.")]
    [SerializeField] NotificationItem itemPrefab;
    [Tooltip("Top-left corner of this RectTransform is where the stack starts; it grows downward.")]
    [SerializeField] RectTransform container;

    [Header("Layout")]
    [SerializeField] float spacing = 12f;

    readonly List<NotificationItem> items = new List<NotificationItem>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (container == null) container = (RectTransform)transform;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Auto-dismiss notifications that were pushed with a duration
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (item != null && item.ExpireTime > 0f && Time.unscaledTime >= item.ExpireTime)
                Dismiss(item);
        }
    }

    /// <summary>
    /// Shows a notification and returns a handle to dismiss or edit it later.
    /// duration = 0 keeps it until you call Dismiss.
    /// </summary>
    public NotificationItem Push(string title, string description, float duration = 0f)
        => Push(itemPrefab, title, description, duration);

    /// <summary>
    /// Same as above, but with its own look. The prefab must have a NotificationItem on it.
    /// A null prefab falls back to the default one. Every look shares the same stack.
    /// </summary>
    public NotificationItem Push(NotificationItem prefab, string title, string description, float duration = 0f)
    {
        if (prefab == null) prefab = itemPrefab;

        var item = Instantiate(prefab, container);
        item.SetText(title, description);
        item.ExpireTime = duration > 0f ? Time.unscaledTime + duration : 0f;

        items.Add(item);
        Relayout();
        return item;
    }

    /// <summary>Slides a notification out. Safe to call with null or an already dismissed one.</summary>
    public void Dismiss(NotificationItem item)
    {
        if (item == null || !items.Remove(item)) return;

        item.Hide();
        Relayout(); // the ones below glide up to fill the gap
    }

    /// <summary>Dismisses everything (e.g. on game over).</summary>
    public void ClearAll()
    {
        foreach (var item in items)
            if (item != null) item.Hide();

        items.Clear();
    }

    // Gives each live item its slot, top to bottom, in the order they were pushed
    void Relayout()
    {
        float y = 0f;
        foreach (var item in items)
        {
            if (item == null) continue;
            item.SetSlot(-y);
            y += item.Height + spacing;
        }
    }
}