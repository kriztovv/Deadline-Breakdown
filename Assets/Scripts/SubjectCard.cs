using TMPro;
using UnityEngine;

public class SubjectCard : MonoBehaviour
{
    [Header("Subject")]
    [SerializeField] private string subjectId = "Math";
    [SerializeField] private string sceneName;

    [Header("Parts")]
    [SerializeField] private Transform visuals;
    [SerializeField] private TMP_Text newAssignmentText;
    [SerializeField] private GameObject assignmentName;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text gradeText;

    [Header("Effects")]
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float hoverLift = 0.05f;
    [SerializeField] private float hoverSpeed = 12f;
    [SerializeField] private float blinkInterval = 0.5f;
    
    [Header("Grade change popup")]
    [SerializeField] private TMP_Text deltaText;          // a TMP child next to the grade letter, disabled by default
    [SerializeField] private Color gainColor = new Color(0.2f, 0.75f, 0.3f);
    [SerializeField] private Color lossColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private float gradeAnimTime = 1.6f;
    private bool holdGrade;

    public string SubjectId => subjectId;
    public string SceneName => sceneName;
    public bool CanOpen => AssignmentManager.Instance != null && AssignmentManager.Instance.IsActive(subjectId);

    private Vector3 baseScale, basePos;
    private bool hovered, shown;
    
    //more time perk
    private Color timerBaseColor;
    [SerializeField] private Color bonusTimerColor = new Color(0.3f, 0.9f, 0.4f);   // green
    
    //disable class perk
    [Header("Disabled look")]
    [SerializeField, Range(0.1f, 1f)] private float disabledBrightness = 0.45f;

    private SpriteRenderer[] sprites;
    private Color[] spriteColors;
    private TMP_Text[] texts;
    private Color[] textColors;
    private bool greyed;
    
    public float PlayGradeChange(int delta, string oldLetter, string newLetter)
    {
        StartCoroutine(GradeRoutine(delta, oldLetter, newLetter));
        return gradeAnimTime;
    }
    
    private System.Collections.IEnumerator GradeRoutine(int delta, string oldLetter, string newLetter)
    {
        holdGrade = true;
        gradeText.text = oldLetter;

        deltaText.gameObject.SetActive(true);
        deltaText.text = delta > 0 ? $"+{delta}" : delta.ToString();   // a minus sign is already in the number
        Color c = delta > 0 ? gainColor : lossColor;
        Vector3 start = deltaText.transform.localPosition;
        bool swapped = false;

        for (float t = 0f; t < gradeAnimTime; t += Time.unscaledDeltaTime)
        {
            float k = t / gradeAnimTime;
            deltaText.transform.localPosition = start + Vector3.up * (0.4f * k);
            c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            deltaText.color = c;

            if (!swapped && k > 0.35f)
            {
                swapped = true;
                gradeText.text = newLetter;
                gradeText.transform.localScale = Vector3.one * 1.4f;   // little pop
            }
            gradeText.transform.localScale = Vector3.Lerp(gradeText.transform.localScale, Vector3.one, 10f * Time.unscaledDeltaTime);
            yield return null;
        }

        deltaText.gameObject.SetActive(false);
        deltaText.transform.localPosition = start;
        gradeText.transform.localScale = Vector3.one;
        holdGrade = false;
    }

    private void Awake()
    {
        baseScale = visuals.localScale;
        basePos = visuals.localPosition;
        timerBaseColor = timerText.color;      // add this line
        
        sprites = visuals.GetComponentsInChildren<SpriteRenderer>(true);
        spriteColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) spriteColors[i] = sprites[i].color;

        var list = new System.Collections.Generic.List<TMP_Text>(visuals.GetComponentsInChildren<TMP_Text>(true));
        if (gradeText != null && !list.Contains(gradeText)) list.Add(gradeText);
        list.Remove(deltaText);     // these two are animated or coloured by other code
        list.Remove(timerText);
        texts = list.ToArray();
        textColors = new Color[texts.Length];
        for (int i = 0; i < texts.Length; i++) textColors[i] = texts[i].color;
        
        ApplyState(false);
    }

    private void OnEnable() => hovered = false;

    private void Update()
    {
        var mgr = AssignmentManager.Instance;
        
        if (mgr != null && gradeText != null && !holdGrade)
        {
            string g = mgr.GradeLetter(subjectId);
            if (gradeText.text != g) gradeText.text = g;
        }
        
        bool active = mgr != null && mgr.IsActive(subjectId);
        SetGreyed(mgr != null && mgr.IsDisabled(subjectId) && !active);
        
        if (active != shown) ApplyState(active);

        if (active)
        {
            int t = Mathf.CeilToInt(mgr.TimeLeft(subjectId));
            timerText.text = $"{t / 60}:{t % 60:00}";
            timerText.color = mgr.IsBonus(subjectId) ? bonusTimerColor : timerBaseColor;   // add this line
            newAssignmentText.enabled = Mathf.Repeat(Time.unscaledTime, blinkInterval * 2f) < blinkInterval;
        }

        float k = hoverSpeed * Time.unscaledDeltaTime;
        visuals.localScale = Vector3.Lerp(visuals.localScale, baseScale * (hovered ? hoverScale : 1f), k);
        visuals.localPosition = Vector3.Lerp(visuals.localPosition, basePos + Vector3.up * (hovered ? hoverLift : 0f), k);
    }

    private void ApplyState(bool active)
    {
        shown = active;
        newAssignmentText.gameObject.SetActive(active);
        assignmentName.SetActive(active);
        timerText.gameObject.SetActive(active);
    }

    public void SetHovered(bool value) => hovered = value;
    
    private void SetGreyed(bool on)
    {
        if (on == greyed) return;
        greyed = on;

        for (int i = 0; i < sprites.Length; i++)
            sprites[i].color = on ? Dim(spriteColors[i]) : spriteColors[i];
        for (int i = 0; i < texts.Length; i++)
            texts[i].color = on ? Dim(textColors[i]) : textColors[i];
    }

    private Color Dim(Color c) => new Color(c.r * disabledBrightness, c.g * disabledBrightness, c.b * disabledBrightness, c.a);
}