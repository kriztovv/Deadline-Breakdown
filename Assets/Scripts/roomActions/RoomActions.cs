using System;
using UnityEngine;

public abstract class RoomAction : MonoBehaviour
{
    [SerializeField] string title;          // shown in the notification
    [SerializeField, TextArea(2, 4)] string description; // shown under the title
    [SerializeField] float timeLimit = 30f; // 0 = no limit
    [SerializeField] float weight = 1f;     // random pick weight
    public string Title => title;
    public string Description => description;
    public float TimeLimit => timeLimit;
    public float Weight => weight;
    public bool IsActive { get; private set; }

    public event Action<RoomAction> Completed;
    public event Action<RoomAction> Failed;

    // Manager can skip actions that can't happen right now
    public virtual bool CanTrigger() => !IsActive;

    public void Begin()  { IsActive = true;  OnBegin(); }
    protected void Complete() { End(); Completed?.Invoke(this); }

    public void Fail()
    {
        if (!IsActive) return; // an action can only fail once per run
        End();

        try { OnFailed(); }
        finally { Failed?.Invoke(this); } // always tell the manager, even if OnFailed throws
    }

    /// <summary>Ends the action quietly, without a failure. Used when the game is over.</summary>
    public void Cancel()
    {
        if (!IsActive) return;
        End();
    }

    void End() { IsActive = false; OnEnd(); }

    protected abstract void OnBegin();       // e.g. play door knock, enable interaction
    protected virtual void OnEnd() { }       // cleanup

    /// <summary>
    /// Default consequence of ignoring an action: game over.
    /// Subclasses can override it, and call base.OnFailed() to keep this behaviour too.
    /// </summary>
    protected virtual void OnFailed()
    {
        // Game over: stop spawning new actions and cancel any others in progress
        if (RoomActionManager.Instance != null)
            RoomActionManager.Instance.StopActions();

        GameObject.Find("controller").GetComponent<doseController>().Fail();
    }
}