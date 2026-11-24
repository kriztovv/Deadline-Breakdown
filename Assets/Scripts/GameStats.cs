using System;
using System.Collections.Generic;
using UnityEngine;

public class GameStats : MonoBehaviour
{
    public static GameStats Instance { get; private set; }

    public event Action Changed;   // UI can subscribe and refresh

    private readonly Dictionary<string, int> plays = new Dictionary<string, int>();
    private readonly Dictionary<string, int> completions = new Dictionary<string, int>();
    private readonly Dictionary<string, Dictionary<string, float>> custom = new Dictionary<string, Dictionary<string, float>>();

    public float TimeSurvived { get; private set; }
    public int PillsTaken { get; private set; }

    private void Awake() => Instance = this;
    private void OnEnable() => DeathSequence.Respawned += ResetRun;
    private void OnDisable() => DeathSequence.Respawned -= ResetRun;
    
    
    [Header("Score weights")]
    [SerializeField] private float pointsPerMinute = 100f;      // survival, linear
    [SerializeField] private float pointsPerCompletion = 250f;  // each finished minigame, linear
    [SerializeField] private float pillWeight = 60f;            // scaled by sqrt(pills), so it slows down
    [SerializeField] private float pointsPerGradeStep = 40f;    // optional bonus for good grades
    [SerializeField] private int worstGradeIndex = 4;           // F in your A, B, C, D, F list

    private void Update()
    {
        if (Running && !AssignmentManager.Dead) TimeSurvived += Time.unscaledDeltaTime;
    }

    // ---- writing ----
    public void AddPlay(string subject) { Bump(plays, subject); Changed?.Invoke(); }
    public void AddCompletion(string subject) { Bump(completions, subject); Changed?.Invoke(); }
    public void AddPill() { PillsTaken++; Changed?.Invoke(); }
    
    public bool Running { get; private set; }
    public void Begin() => Running = true;

    // minigame-specific stats, e.g. AddStat("Finance", "coins", 1)
    public void AddStat(string subject, string stat, float amount = 1f)
    {
        if (!custom.TryGetValue(subject, out var d)) custom[subject] = d = new Dictionary<string, float>();
        d.TryGetValue(stat, out float v);
        d[stat] = v + amount;
        Changed?.Invoke();
    }

    // ---- reading ----
    public int Plays(string subject) => plays.TryGetValue(subject, out int n) ? n : 0;
    public int Completions(string subject) => completions.TryGetValue(subject, out int n) ? n : 0;
    public float GetStat(string subject, string stat) =>
        custom.TryGetValue(subject, out var d) && d.TryGetValue(stat, out float v) ? v : 0f;

    public int TotalPlays { get { int t = 0; foreach (var p in plays) t += p.Value; return t; } }
    public int TotalCompletions { get { int t = 0; foreach (var p in completions) t += p.Value; return t; } }

// survival + completions + pills (diminishing) + grades
    public float Score
    {
        get
        {
            float survival    = TimeSurvived / 60f * pointsPerMinute;
            float completed   = TotalCompletions * pointsPerCompletion;
            float pillsPart   = Mathf.Sqrt(PillsTaken) * pillWeight;
            float gradesPart  = GradePoints() * pointsPerGradeStep;

            return survival + completed + pillsPart + gradesPart;
        }
    }
    
    // how many grade steps above F you hold across all subjects (A = 4, F = 0, per subject)
    private int GradePoints()
    {
        var am = AssignmentManager.Instance;
        if (am == null) return 0;

        int points = 0;
        foreach (string id in am.SubjectIds)
            points += Mathf.Max(0, worstGradeIndex - am.GradeIndex(id));
        return points;
    }

    public void ResetRun()
    {
        plays.Clear(); completions.Clear(); custom.Clear();
        TimeSurvived = 0f; PillsTaken = 0;
        Changed?.Invoke();
    }

    private static void Bump(Dictionary<string, int> d, string key)
    {
        d.TryGetValue(key, out int n);
        d[key] = n + 1;
    }
}
