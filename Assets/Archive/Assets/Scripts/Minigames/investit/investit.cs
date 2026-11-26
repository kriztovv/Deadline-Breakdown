using System.Collections;
using TMPro;
using UnityEngine;

public class investit : minigameController
{
    public int collectedMoney;
    [SerializeField] public int requiredMoney;
    [SerializeField] private TMP_Text moneyCounterText;

    [Header("Timer")]
    [SerializeField] private float gameTime = 20f;   // seconds until the minigame ends
    [SerializeField] private Transform timerBar;     // scaled horizontally, runs out over gameTime, its left edge stays in place

    [Header("Tutorial")]
    public bool tutorial;                                // true when the game starts = the tutorial is shown first
    [SerializeField] private GameObject tutorialRoot;    // turned on during the tutorial
    [SerializeField] private float tutorialTime = 5f;    // X: seconds the timer and the spawning stay paused (real time)

    private minigameSpawner _spawner;
    private float _elapsed;
    private bool _running;   // the timer runs and the money counts
    private bool _ended;
    private Coroutine _endRoutine;
    private Vector3 _barFullScale;
    private float _barLeftEdge;
    private float _barSpriteMinX;

    public void StartGame()
    {
        _spawner = GameObject.Find("game").transform.Find("investitSpawner").GetComponent<minigameSpawner>();
        InitBar();

        if (tutorial) StartCoroutine(TutorialRoutine());
        else BeginGame();
    }

    private IEnumerator TutorialRoutine()
    {
        SetTutorialRoot(true);
        _spawner.enabled = false;   // nothing spawns while the tutorial is shown, and the timer hasn't started
        // ANIMATION: tutorial intro

        yield return new WaitForSecondsRealtime(tutorialTime);

        SetTutorialRoot(false);
        BeginGame();
    }

    private void BeginGame()
    {
        _running = true;
        _endRoutine = StartCoroutine(End(gameTime));
        _spawner.enabled = true;
    }

    // Never disables an object this script lives under, that would stop the coroutines
    private void SetTutorialRoot(bool active)
    {
        if (tutorialRoot == null) return;
        if (!active && transform.IsChildOf(tutorialRoot.transform))
        {
            Debug.LogWarning("investit: this script is inside the tutorial root, so it can't be disabled. Move the script outside it.", this);
            return;
        }
        tutorialRoot.SetActive(active);
    }

    protected override void MgUpdate()
    {
        if (!_running || _ended) return;

        _elapsed += Time.deltaTime;   // same clock as the WaitForSeconds in End
        UpdateBar();

        // reached early: no need to wait for the timer
        if (requiredMoney > 0 && collectedMoney >= requiredMoney) Finish();
    }

    private void InitBar()
    {
        if (timerBar == null) return;

        _barFullScale = timerBar.localScale;

        // the bar shrinks towards its left edge, whatever the sprite's pivot is
        var sr = timerBar.GetComponent<SpriteRenderer>();
        _barSpriteMinX = (sr != null && sr.sprite != null) ? sr.sprite.bounds.min.x : 0f;
        _barLeftEdge = timerBar.localPosition.x + _barSpriteMinX * _barFullScale.x;
    }

    private void UpdateBar()
    {
        if (timerBar == null) return;

        float fraction = gameTime > 0f ? Mathf.Clamp01(1f - _elapsed / gameTime) : 0f;

        Vector3 scale = _barFullScale;
        scale.x = _barFullScale.x * fraction;
        timerBar.localScale = scale;

        Vector3 pos = timerBar.localPosition;
        pos.x = _barLeftEdge - _barSpriteMinX * scale.x;
        timerBar.localPosition = pos;
    }

    public void UpdateCounter()
    {
        moneyCounterText.text = collectedMoney.ToString() + "/" + requiredMoney.ToString();
    }

    private IEnumerator End(float timer)
    {
        yield return new WaitForSeconds(timer - 3);
        _spawner.enabled = false;
        yield return new WaitForSeconds(2);
        Finish();
    }

    // Grades and closes the minigame, once (from the timer or from reaching the required amount)
    private void Finish()
    {
        if (_ended) return;
        _ended = true;
        _running = false;
        if (_endRoutine != null) StopCoroutine(_endRoutine);
        _spawner.enabled = false;

        // ANIMATION: success feedback, if an early finish should wait a moment before closing
        if (collectedMoney >= requiredMoney)
        {
            AssignmentManager.Instance.Improve("Finance");
        }
        else
        {
            AssignmentManager.Instance.Worsen("Finance");
        }
        ClassroomInput.Instance.CloseMinigame(true);
    }
}