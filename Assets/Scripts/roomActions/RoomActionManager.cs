using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Randomly picks and starts RoomActions, tracks their timeouts,
/// and raises events the UI can listen to.
/// Put this on a single GameObject in the scene.
/// </summary>
public class RoomActionManager : MonoBehaviour
{
    public static RoomActionManager Instance { get; private set; }

    [Header("Timing")]
    [SerializeField] float minDelay = 10f;          // min seconds between triggers
    [SerializeField] float maxDelay = 30f;          // max seconds between triggers
    [SerializeField] float perActionCooldown = 20f; // same action can't repeat sooner than this

    [Header("Limits")]
    [SerializeField] int maxConcurrent = 2;

    [Header("Actions (can also self-register at runtime)")]
    [SerializeField] List<RoomAction> actions = new List<RoomAction>();

    [Header("Notification look")]
    [SerializeField] NotificationItem notificationPrefab;   // the RoomActionNotification prefab

    // UI and other systems listen to these
    public event Action<RoomAction> ActionStarted;
    public event Action<RoomAction, bool> ActionEnded; // bool = success

    readonly Dictionary<RoomAction, float> activeTimers = new Dictionary<RoomAction, float>();
    readonly Dictionary<RoomAction, float> lastEndTime = new Dictionary<RoomAction, float>();
    readonly Dictionary<RoomAction, NotificationItem> notifications = new Dictionary<RoomAction, NotificationItem>();
    readonly List<RoomAction> candidates = new List<RoomAction>();   // reused, avoids allocations
    readonly List<RoomAction> timeoutBuffer = new List<RoomAction>(); // reused

    float nextTriggerTime;

    public IReadOnlyCollection<RoomAction> ActiveActions => activeTimers.Keys;

    public bool running;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Hook up anything assigned in the Inspector
        foreach (var a in actions)
            Subscribe(a);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var a in actions)
            Unsubscribe(a);
    }

  public void StartActions()
{
    running = true;
    ScheduleNext();
}

    /// <summary>
    /// Stops spawning and cancels everything still in progress (call on game over).
    /// Call StartActions() again to resume.
    /// </summary>
    public void StopActions()
    {
        running = false;

        // Own snapshot: StopActions runs from inside TickTimeouts (via Fail), which is
        // enumerating timeoutBuffer, so that list must not be touched here.
        var snapshot = new List<RoomAction>(activeTimers.Keys);

        foreach (var a in snapshot)
        {
            // Skip the one that is already ending (the action that just failed);
            // FinishAction cleans that one up.
            if (a == null || !a.IsActive) continue;

            a.Cancel();
            activeTimers.Remove(a);
            DismissNotification(a);
        }
    }

    void Update()
    {
        if (!running) return;

        TickTimeouts();

        if (Time.time >= nextTriggerTime)
        {
            TryTriggerRandom();
            ScheduleNext();
        }
    }

    // ---------- Registration (call from RoomAction.OnEnable / OnDisable) ----------

    public void Register(RoomAction action)
    {
        if (action == null || actions.Contains(action)) return;
        actions.Add(action);
        Subscribe(action);
    }

    public void Unregister(RoomAction action)
    {
        if (action == null) return;
        if (activeTimers.ContainsKey(action))
            activeTimers.Remove(action);
        Unsubscribe(action);
        actions.Remove(action);
        DismissNotification(action); // don't leave a notification stuck for a removed action
    }

    void Subscribe(RoomAction a)
    {
        if (a == null) return;
        a.Completed += OnActionCompleted;
        a.Failed += OnActionFailed;
    }

    void Unsubscribe(RoomAction a)
    {
        if (a == null) return;
        a.Completed -= OnActionCompleted;
        a.Failed -= OnActionFailed;
    }

    // ---------- Picking ----------

    void TryTriggerRandom()
    {
        if (activeTimers.Count >= maxConcurrent) return;

        candidates.Clear();
        float totalWeight = 0f;

        foreach (var a in actions)
        {
            if (a == null || !a.isActiveAndEnabled) continue;
            if (!a.CanTrigger()) continue;
            if (IsOnCooldown(a)) continue;

            candidates.Add(a);
            totalWeight += Mathf.Max(0f, a.Weight);
        }

        if (candidates.Count == 0 || totalWeight <= 0f) return;

        // Weighted random pick
        float roll = Random.value * totalWeight;
        foreach (var a in candidates)
        {
            roll -= Mathf.Max(0f, a.Weight);
            if (roll <= 0f)
            {
                StartAction(a);
                return;
            }
        }

        StartAction(candidates[candidates.Count - 1]); // float rounding fallback
    }

    bool IsOnCooldown(RoomAction a)
    {
        return lastEndTime.TryGetValue(a, out float t) && Time.time - t < perActionCooldown;
    }

    /// <summary>Starts a specific action (also handy for debugging / scripted events).</summary>
    public void StartAction(RoomAction action)
    {
        if (action == null || activeTimers.ContainsKey(action)) return;

        activeTimers[action] = action.TimeLimit; // 0 = no limit
        action.Begin();
        PushNotification(action);
        ActionStarted?.Invoke(action);
    }

    // ---------- Timeouts ----------

    void TickTimeouts()
    {
        if (activeTimers.Count == 0) return;

        timeoutBuffer.Clear();
        // Copy keys first: Fail() ends up modifying activeTimers
        foreach (var a in activeTimers.Keys)
            timeoutBuffer.Add(a);

        foreach (var a in timeoutBuffer)
        {
            if (a.TimeLimit <= 0f) continue; // no limit

            // May already be gone: an earlier Fail() in this loop can stop/cancel the others
            if (!activeTimers.TryGetValue(a, out float current)) continue;

            float remaining = current - Time.deltaTime;
            activeTimers[a] = remaining;

            if (remaining <= 0f)
                a.Fail(); // triggers OnActionFailed via the event
        }
    }

    /// <summary>Remaining seconds for the UI countdown. Returns 0 if not active or no limit.</summary>
    public float GetRemainingTime(RoomAction action)
    {
        return activeTimers.TryGetValue(action, out float t) ? Mathf.Max(0f, t) : 0f;
    }

    // ---------- Action results ----------

    void OnActionCompleted(RoomAction a) => FinishAction(a, true);
    void OnActionFailed(RoomAction a) => FinishAction(a, false);

    void FinishAction(RoomAction a, bool success)
    {
        if (!activeTimers.Remove(a)) return; // wasn't started by us

        lastEndTime[a] = Time.time;
        DismissNotification(a);
        ActionEnded?.Invoke(a, success);
    }

    // ---------- Notifications (all UI work lives in NotificationManager) ----------

    void PushNotification(RoomAction action)
    {
        if (NotificationManager.Instance == null) return;

        notifications[action] = NotificationManager.Instance.Push(notificationPrefab, action.Title, action.Description);
    }

    void DismissNotification(RoomAction action)
    {
        if (!notifications.Remove(action, out var item)) return;

        if (NotificationManager.Instance != null)
            NotificationManager.Instance.Dismiss(item);
    }

    void ScheduleNext()
    {
        nextTriggerTime = Time.time + Random.Range(minDelay, maxDelay);
    }
}