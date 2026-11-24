using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActivePillIcons : MonoBehaviour
{
    [SerializeField] private doseController dose;
    [SerializeField] private RectTransform container;   // needs a Horizontal Layout Group
    [SerializeField] private Sprite fallbackIcon;       // used if a Doser has no icon

    [Header("Look")]
    [SerializeField] private float iconSize = 48f;
    [SerializeField] private float swayAmount = 6f;     // side to side, in pixels
    [SerializeField] private float swaySpeed = 2f;
    [SerializeField] private float bobAmount = 3f;      // small up and down motion
    [SerializeField, Range(0f, 1f)] private float delayedAlpha = 0.35f;   // pill hasn't kicked in yet

    private class Entry { public RectTransform slot, icon; public Image image; public float phase; }

    private readonly Dictionary<Doser, Entry> entries = new Dictionary<Doser, Entry>();
    private readonly List<Doser> stale = new List<Doser>();

    private void Update()
    {
        // new pills get an icon
        foreach (Doser d in dose.activeDosers)
            if (d != null && !entries.ContainsKey(d)) entries[d] = Create(d);

        // finished pills lose theirs (the copy was removed from the list or destroyed)
        stale.Clear();
        foreach (var kv in entries)
            if (kv.Key == null || !dose.activeDosers.Contains(kv.Key)) stale.Add(kv.Key);
        foreach (Doser d in stale)
        {
            Destroy(entries[d].slot.gameObject);
            entries.Remove(d);
        }

        // sway
        float t = Time.unscaledTime * swaySpeed;
        foreach (var kv in entries)
        {
            Entry e = kv.Value;
            e.icon.anchoredPosition = new Vector2(
                Mathf.Sin(t + e.phase) * swayAmount,
                Mathf.Sin(t * 1.3f + e.phase) * bobAmount);

            Color c = e.image.color;
            c.a = kv.Key.delay > 0 ? delayedAlpha : 1f;
            e.image.color = c;
        }
    }

    private Entry Create(Doser d)
    {
        // the slot is positioned by the layout group; the icon inside it moves freely
        var slotGO = new GameObject("PillSlot", typeof(RectTransform));
        var slot = slotGO.GetComponent<RectTransform>();
        slot.SetParent(container, false);
        slot.sizeDelta = new Vector2(iconSize, iconSize);

        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var icon = iconGO.GetComponent<RectTransform>();
        icon.SetParent(slot, false);
        icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.sizeDelta = slot.sizeDelta;

        var img = iconGO.GetComponent<Image>();
        img.sprite = d.icon != null ? d.icon : fallbackIcon;
        img.preserveAspect = true;
        img.raycastTarget = false;

        return new Entry { slot = slot, icon = icon, image = img, phase = Random.value * 6.28f };
    }
}
