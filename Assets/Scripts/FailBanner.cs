using System.Collections;
using UnityEngine;

public class FailBanner : MonoBehaviour
{
    [SerializeField] private GameObject banner;
    [SerializeField] private float delay = 0f;   // wait before showing, e.g. to sync with the pixel-sort start

    
    
    private void OnSubjectFailed(string id) => StartCoroutine(Show());
    private void OnEnable()
    {
        DeathSequence.Died += OnDied;
        AssignmentManager.SubjectFailed += OnSubjectFailed;
    }

    private void OnDisable()
    {
        DeathSequence.Died -= OnDied;
        AssignmentManager.SubjectFailed -= OnSubjectFailed;
    }

    private void OnDied() => StartCoroutine(Show());

    private IEnumerator Show()
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        banner.SetActive(true);   // the Animator restarts from its default state when enabled
    }
}
