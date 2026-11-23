using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class AssignmentManager : MonoBehaviour
{
    public static AssignmentManager Instance { get; private set; }

    [SerializeField] private List<string> subjectIds = new List<string>
        { "Geography", "Marketing", "PE", "History", "IT", "Finance", "Art", "Chemistry" };
    private float assignmentDuration = 180f;    // the 3:00 on the cards
    private float newAssignmentInterval = 60f;  // a new subject every minute
    
    [Header("Grades")]
    [SerializeField] private string[] gradeLetters = { "A", "B", "C", "D", "F" }; // best to worst
    [SerializeField] private int startGrade = 0;
    public UnityEvent onFailed;                       // optional extra hook
    
    [SerializeField] private float failDelay = 2f;   // length of the banner animation
    public static event System.Action<string> SubjectFailed;   // fires the moment a grade hits F
    private bool failing;

    private readonly Dictionary<string, int> grades = new Dictionary<string, int>();
    public event System.Action<string, int> GradeChanged;
    
    private readonly Dictionary<string, float> timers = new Dictionary<string, float>();
    private readonly List<string> tempKeys = new List<string>();
    private float spawnTimer;
    
    public int GradeIndex(string id) => grades.TryGetValue(id, out int g) ? g : startGrade;
    public string GradeLetter(string id) => gradeLetters[GradeIndex(id)];

    private readonly Dictionary<string, int> queued = new Dictionary<string, int>();
    
    // upgrade where it adds more time
    public string BonusSubject { get; private set; }
    private float bonusSeconds;

    public bool IsBonus(string id) => BonusSubject != null && id == BonusSubject;

// the grade the player will have once the queued changes apply
    public int PendingIndex(string id) => queued.TryGetValue(id, out int q) ? q : GradeIndex(id);

    public void Worsen(string id, int steps = 1) => SetGradeIndex(id, PendingIndex(id) + steps);
    public void Improve(string id, int steps = 1) => SetGradeIndex(id, PendingIndex(id) - steps);

    
    private string pendingFail;

    public string LetterFor(int index) => gradeLetters[Mathf.Clamp(index, 0, gradeLetters.Length - 1)];

    
    [SerializeField] private GameObject workPrompt;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private minigameInputController minigameInputController;
    
    
    //perk where it disables a class
    [Header("Class rotation perk")]
    [SerializeField] private float rotateInterval = 60f;   // seconds before the disabled class changes

    public string DisabledSubject { get; private set; }
    private float rotateTimer;
    
    public bool IsDisabled(string id) => DisabledSubject != null && id == DisabledSubject;
    
    //end

    public static bool Dead { get; private set; }
    public bool Running { get; private set; }

    private void OnEnable()  { DeathSequence.Respawned += OnRespawned; DeathSequence.Died += OnDied; }
    private void OnDisable() { DeathSequence.Respawned -= OnRespawned; DeathSequence.Died -= OnDied; }

    private void OnDied()
    {
        Dead = true;
        pendingFail = null;
        queued.Clear();
    } 
    
    public IReadOnlyList<string> SubjectIds => subjectIds;
    
    
    private void Awake()
    {
        Dead = false;
        Time.timeScale = 1f;
        MonitorInput.Clear();
        
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        foreach (string id in subjectIds) grades[id] = startGrade;
    }
    
    public void Begin()
    {
        if (Running) return;
        Running = true;
        spawnTimer = 5;
    }

    private void Update()
    {
        if (!Running) return;
        UpdateRotation();
        
        tempKeys.Clear();
        tempKeys.AddRange(timers.Keys);
        foreach (string id in tempKeys)
        {
            timers[id] -= Time.deltaTime;
            if (timers[id] <= 0f)
            {
                timers.Remove(id);
                AssignmentManager.Instance.Worsen(id);
            }
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            ActivateRandom();
            spawnTimer = Random.Range(5, 20) + UpgradeManager.Get("assignmentGap", 0f);   // perk adds seconds
        }

        if (Input.anyKeyDown)
        {
            if (Input.GetKeyDown(KeyCode.Keypad0)) timers["Geography"] = 180f;
            if (Input.GetKeyDown(KeyCode.Keypad1)) timers["Marketing"] = 180f;
            if (Input.GetKeyDown(KeyCode.Keypad2)) timers["PE"] = 180f;
            if (Input.GetKeyDown(KeyCode.Keypad3)) timers["History"] = 180f;
            if (Input.GetKeyDown(KeyCode.Keypad4)) timers["IT"] = 180f;
            if (Input.GetKeyDown(KeyCode.Keypad5)) timers["Finance"] = 180f;
            if (Input.GetKeyDown(KeyCode.Keypad6)) timers["Art"] = 180f;
            if (Input.GetKeyDown(KeyCode.Keypad7)) timers["Chemistry"] = 180f;
        }
    }
    
    private void OnRespawned()
    {
        Dead = false;
        failing = false;
        queued.Clear();

        // grades back to the start value
        foreach (string id in subjectIds) grades[id] = startGrade;

        // clear running assignments and start fresh
        timers.Clear();
        ActivateRandom();
        spawnTimer = newAssignmentInterval;
    }
    
    public void SetGradeIndex(string id, int index)
    {
        index = Mathf.Clamp(index, 0, gradeLetters.Length - 1);

        if (ClassroomInput.Instance != null && ClassroomInput.Instance.InMinigame)
        {
            queued[id] = index;          // invisible until the player leaves the minigame
            return;
        }
        Commit(id, index, false);
    }
    
    // called by ClassroomInput after the grade animation has started
    public void ApplyQueued()
    {
        foreach (var kv in new List<KeyValuePair<string, int>>(queued))
            Commit(kv.Key, kv.Value, true);
        queued.Clear();
    }

    private void Commit(string id, int index, bool deferFail)
    {
        int worst = gradeLetters.Length - 1;
        if (index == GradeIndex(id)) return;

        grades[id] = index;
        GradeChanged?.Invoke(id, index);

        if (index == worst && !failing)
        {
            if (deferFail) pendingFail = id;               // started by ResolvePendingFail after the animation
            else StartCoroutine(FailRoutine(id));
        }
    }
    
    private System.Collections.IEnumerator FailRoutine(string id)
    {
        failing = true;
        SubjectFailed?.Invoke(id);                        // banner starts now
        yield return new WaitForSecondsRealtime(failDelay);

        var dose = FindFirstObjectByType<doseController>();
        if (dose != null) dose.Fail();                    // death + pixel sort start here
        
        minigameInputController.atMonitor = false;
        crosshair.SetActive(false);
        Debug.Log("you fried your shit");
        workPrompt.SetActive(false);
        onFailed?.Invoke();
        failing = false;
        
    }

    private void ActivateRandom()
    {
        // how many assignments may run at once: all subjects, minus whatever perks take off
        int max = Mathf.Max(1, Mathf.RoundToInt(UpgradeManager.Get("maxAssignments", subjectIds.Count)));
        if (timers.Count >= max) return;
        
        var candidates = new List<string>();
        foreach (string id in subjectIds)
            if (!timers.ContainsKey(id) && !IsDisabled(id)) candidates.Add(id);

        if (candidates.Count == 0) return;

        string pick = candidates[Random.Range(0, candidates.Count)];

        float duration = Random.Range(30, 120);
        if (IsBonus(pick)) duration += bonusSeconds;   // the perk's subject gets extra time

        timers[pick] = duration;
    }
    
    // called by the perk when it's picked
    public void PickBonusSubject(float seconds)
    {
        bonusSeconds = seconds;
        BonusSubject = subjectIds[Random.Range(0, subjectIds.Count)];

        if (timers.ContainsKey(BonusSubject))        // already running, so top it up now
            timers[BonusSubject] += seconds;
    }
    
    private void UpdateRotation()
    {
        // perk not owned yet: nothing is disabled
        if (UpgradeManager.Get("classRotation", 0f) < 1f) { DisabledSubject = null; rotateTimer = 0f; return; }

        if (DisabledSubject == null) { RotateDisabled(); return; }   // first pick right after the perk

        rotateTimer += Time.unscaledDeltaTime;
        if (rotateTimer >= rotateInterval) RotateDisabled();
    }

    private readonly List<string> _eligible = new List<string>();
    private void RotateDisabled()
    {
        _eligible.Clear();
        foreach (string id in subjectIds)
            if (!timers.ContainsKey(id) && id != DisabledSubject)   // idle, and not the one already disabled
                _eligible.Add(id);

        if (_eligible.Count == 0)
        {
            // every other class is busy, so keep the current one and try again in about a second
            rotateTimer = Mathf.Max(0f, rotateInterval - 1f);
            return;
        }

        rotateTimer = 0f;
        DisabledSubject = _eligible[Random.Range(0, _eligible.Count)];
    }
    
    public void ResolvePendingFail()
    {
        if (pendingFail == null || failing) return;
        string id = pendingFail;
        pendingFail = null;
        if (GradeIndex(id) == gradeLetters.Length - 1)   // still F, so it wasn't improved back in the same game
            StartCoroutine(FailRoutine(id));
    }

    public bool IsActive(string id) => timers.ContainsKey(id);
    public float TimeLeft(string id) => timers.TryGetValue(id, out float t) ? t : 0f;
    public void Complete(string id) => timers.Remove(id); // call when the mini-game is won
}
