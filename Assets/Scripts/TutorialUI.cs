using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup tutorialGroup;
    [SerializeField] private float showDuration = 15f;
    [SerializeField] private float fadeSpeed = 3f;

    private float timer;
    private bool visible = true;

    private void Start()
    {
        timer = showDuration;
        tutorialGroup.alpha = 0f;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            visible = !visible;
            timer = visible ? showDuration : 0f;
        }

        if (visible)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) visible = false;
        }

        float target = visible ? 1f : 0f;
        tutorialGroup.alpha = Mathf.MoveTowards(tutorialGroup.alpha, target, fadeSpeed * Time.deltaTime);
    }
}
