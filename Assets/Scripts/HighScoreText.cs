using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class HighScoreText : MonoBehaviour
{
    [SerializeField] private string newBestSuffix = "  NEW!";   // leave empty to turn it off

    private TMP_Text text;

    private void OnEnable()
    {
        if (text == null) text = GetComponent<TMP_Text>();

        var stats = GameStats.Instance;
        if (stats == null) { text.text = PlayerPrefs.GetInt("highscore_v1", 0).ToString(); return; }

        bool newBest = stats.CommitHighScore();               // save first, then show
        text.text = stats.HighScore + (newBest ? newBestSuffix : "");
    }
}
