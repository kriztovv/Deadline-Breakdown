using UnityEngine;

public class OutlineBoost : MonoBehaviour
{
    [SerializeField] private float boostedWidth = 4f;
    [SerializeField] private Color boostedColor = Color.red;

    private Outline outline;
    private Color baseColor;
    private float baseWidth;
    private bool boosted;

    private void Awake()
    {
        outline = GetComponentInChildren<Outline>();
        if (outline == null) { enabled = false; return; }

        baseColor = outline.OutlineColor;    // your normal white
        baseWidth = outline.OutlineWidth;    // your normal 2
    }

    private void Update()
    {
        bool want = UpgradeManager.Get("pillGlow", 0f) >= 1f;
        if (want == boosted) return;         // only touch the outline when something changes
        boosted = want;

        outline.OutlineColor = want ? boostedColor : baseColor;
        outline.OutlineWidth = want ? boostedWidth : baseWidth;
    }
}
