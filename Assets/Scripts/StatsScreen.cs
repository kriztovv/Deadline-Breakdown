using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class StatsScreen : MonoBehaviour
{
    [Header("Overall")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text miscText;
    [SerializeField] private float countUpTime = 1.5f;

    [Header("Subject selector")]
    [SerializeField] private TMP_Text subjectNameText;
    [SerializeField] private TMP_Text subjectStatsText;
    [SerializeField] private GameObject leftArrow;    // optional visual hints
    [SerializeField] private GameObject rightArrow;
    [SerializeField] private Key leftKey = Key.LeftArrow;
    [SerializeField] private Key rightKey = Key.RightArrow;

    private int index;

    private void OnEnable()
    {
        index = 0;
        ShowMisc();
        ShowSubject();
        StopAllCoroutines();
        StartCoroutine(CountUpScore());
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        int count = AssignmentManager.Instance.SubjectIds.Count;
        if (kb[rightKey].wasPressedThisFrame) { index = (index + 1) % count; ShowSubject(); }
        if (kb[leftKey].wasPressedThisFrame)  { index = (index - 1 + count) % count; ShowSubject(); }
    }

    private IEnumerator CountUpScore()
    {
        float target = GameStats.Instance.Score;
        for (float t = 0f; t < countUpTime; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / countUpTime, 3f);   // ease out
            scoreText.text = Mathf.RoundToInt(target * k).ToString("N0");
            yield return null;
        }
        scoreText.text = Mathf.RoundToInt(target).ToString("N0");
    }

    private void ShowMisc()
    {
        var s = GameStats.Instance;
        int t = Mathf.FloorToInt(s.TimeSurvived);
        miscText.text =
            $"Time survived   {t / 60}:{t % 60:00}\n" +
            $"Pills taken   {s.PillsTaken}\n" +
            $"Games played   {s.TotalPlays}\n" +
            $"Games finished   {s.TotalCompletions}";
    }

    private void ShowSubject()
    {
        string id = AssignmentManager.Instance.SubjectIds[index];
        var s = GameStats.Instance;

        subjectNameText.text = id.ToUpper();
        subjectStatsText.text =
            $"Grade   {AssignmentManager.Instance.GradeLetter(id)}\n" +
            $"Times played   {s.Plays(id)}\n" +
            $"Times finished   {s.Completions(id)}";
        // later: add a line per minigame stat, e.g. $"Coins   {s.GetStat(id, "coins")}"
    }
}
