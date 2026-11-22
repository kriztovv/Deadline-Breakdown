using System.Collections.Generic;
using UnityEngine;

public class GradeRaiser : MonoBehaviour
{
    [SerializeField] private float baseInterval = 90f;   // seconds between boosts
    [SerializeField] private float minInterval = 20f;    // floor if you stack interval perks

    private float _timer;
    private readonly List<string> _candidates = new List<string>();

    private void Update()
    {
        // perk not owned yet
        if (UpgradeManager.Get("gradeRaise", 0f) < 1f) { _timer = 0f; return; }

        var am = AssignmentManager.Instance;
        if (am == null || !am.Running || AssignmentManager.Dead) return;

        float interval = Mathf.Max(minInterval, UpgradeManager.Get("gradeRaiseInterval", baseInterval));

        _timer += Time.deltaTime;   // SCALED
        if (_timer < interval) return;
        _timer = 0f;

        RaiseRandomGrade(am);
    }

    private void RaiseRandomGrade(AssignmentManager am)
    {
        _candidates.Clear();
        foreach (string id in am.SubjectIds)
            if (am.PendingIndex(id) > 0) _candidates.Add(id);   // anything below an A

        if (_candidates.Count == 0) return;                      // everything is already an A

        string pick = _candidates[Random.Range(0, _candidates.Count)];
        am.Improve(pick);   // goes through your queue, so it animates on minigame exit
    }
}