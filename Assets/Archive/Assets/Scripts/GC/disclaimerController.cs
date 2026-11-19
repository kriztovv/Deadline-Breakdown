using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class disclaimerController : MonoBehaviour
{
    [SerializeField] private List<CanvasGroup> canvasGroups;
    [SerializeField] private AudioClip sfx;

    private Queue<CanvasGroup> _canvasQueue;

    private void Start()
    {
        _canvasQueue = new Queue<CanvasGroup>();
        foreach (var canvasGroup in canvasGroups) _canvasQueue.Enqueue(canvasGroup);
        StartCoroutine(Show(_canvasQueue.Dequeue(),true));
    }

    private IEnumerator Show(CanvasGroup canvas, bool isVideo = false)
    {
        while (canvas.alpha < 1)
        {
            canvas.alpha = Mathf.Lerp(canvas.alpha, 2, Time.deltaTime * 2);
            yield return null;
        }
        canvas.alpha = 1;

        if (isVideo) yield return new WaitForSeconds(6.5f);
        else yield return new WaitForSeconds(3);

        while (canvas.alpha > 0)
        {
            canvas.alpha = Mathf.Lerp(canvas.alpha, -1, Time.deltaTime);
            yield return null;
        }
        canvas.alpha = 0;
        yield return new WaitForSeconds(0.5f);

        if (isVideo) SFXManager.instance.PlaySFX(sfx, transform, 0.2f);
        if (_canvasQueue.Count > 0) StartCoroutine(Show(_canvasQueue.Dequeue()));
        else SceneManager.LoadScene("RoomScene");
    }
}