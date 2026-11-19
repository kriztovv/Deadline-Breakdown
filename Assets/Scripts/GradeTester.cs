using UnityEngine;

public class GradeTester : MonoBehaviour
{
    [SerializeField] private string subjectId = "Math";
    [SerializeField] private KeyCode worsenKey = KeyCode.J;
    [SerializeField] private KeyCode improveKey = KeyCode.K;

    private void OnEnable()  => MonitorInput.OnKeyDown += OnKey;
    private void OnDisable() => MonitorInput.OnKeyDown -= OnKey;

    private void OnKey(KeyCode key)
    {
        if (key == worsenKey) Worsen();
        if (key == improveKey) Improve();
    }

    [ContextMenu("Worsen")] public void Worsen()  => AssignmentManager.Instance.Worsen(subjectId);
    [ContextMenu("Improve")] public void Improve() => AssignmentManager.Instance.Improve(subjectId);
}
