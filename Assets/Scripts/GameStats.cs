using System;
using System.Collections.Generic;
using UnityEngine;

public class GameStats : MonoBehaviour
{
    public static GameStats Instance { get; private set; }

    [Tooltip("Score uses max(1, value) for pills and completions, so a single 0 doesn't wipe out the whole score.")]
    [SerializeField] private bool countZeroAsOne = true;

    public event Action Changed;   // UI can subscribe and refresh

    private readonly Dictionary<string, int> plays = new Dictionary<string, int>();
    private readonly Dictionary<string, int> completions = new Dictionary<string, int>();
    private readonly Dictionary<string, Dictionary<string, float>> custom = new Dictionary<string, Dictionary<string, float>>();

    public float TimeSurvived { get; private set; }
    public int PillsTaken { get; private set; }

    private void Awake() => Instance = this;
    private void OnEnable() => DeathSequence.Respawned += ResetRun;
    private void OnDisable() => DeathSequence.Respawned -= ResetRun;

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

    // time survived * pills taken * minigames finished
    public float Score
    {
        get
        {
            float pills = PillsTaken, done = TotalCompletions;
            if (countZeroAsOne) { pills = Mathf.Max(1f, pills); done = Mathf.Max(1f, done); }
            return TimeSurvived * pills * done;
        }
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
