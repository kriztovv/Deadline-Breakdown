using TMPro;
using UnityEngine;

public class PillRiskIndicator : MonoBehaviour
{
    [SerializeField] private PickupController pickup;
    [SerializeField] private doseController dose;
    [SerializeField] private TMP_Text riskText;            // a TMP text (UI or 3D) near the crosshair
    [SerializeField] private float limit = 100f;           // the overdose threshold

    [Header("Look")]
    [SerializeField] private string deathLabel = "CERTAIN DEATH";
    [SerializeField] private Color deathColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private string warnLabel = "MIGHT DIE";
    [SerializeField] private Color warnColor = new Color(1f, 0.85f, 0.2f);

    
    private float _nextLog;
    private void Update()
    {
        bool log = Time.unscaledTime >= _nextLog;
        if (log) _nextLog = Time.unscaledTime + 1f;

        float sense = UpgradeManager.Get("riskSense", 0f);
        if (sense < 1f) { if (log) Hide(); return; }

        Rigidbody held = pickup.Held;
        if (held == null || !held.TryGetComponent(out Pill heldPill)) { if (log)  Hide(); return; }

        Doser pill = heldPill.doser;
        if (pill == null) { if (log) Hide(); return; }

        float value = pill.value + Mathf.RoundToInt(UpgradeManager.Get("pillValue", 0f));
        float uncertainty = pill.uncertainty;
        foreach (Doser d in dose.activeDosers)
        {
            if (d == null) continue;
            value += d.value;
            uncertainty += d.uncertainty;
        }

        if (log) Debug.Log($"[Risk] value = {value}, uncertainty = {uncertainty}, limit = {limit}");

        if (value >= limit) Show(deathLabel, deathColor);
        else if (value + uncertainty >= limit) Show(warnLabel, warnColor);
        else Hide();
    }

    private Doser DoserOf(Rigidbody held)
    {
        return held.TryGetComponent(out Pill pill) ? pill.doser : null;
    }

    private void Show(string label, Color color) { riskText.text = label; riskText.color = color; }
    private void Hide() => riskText.text = "";
}
