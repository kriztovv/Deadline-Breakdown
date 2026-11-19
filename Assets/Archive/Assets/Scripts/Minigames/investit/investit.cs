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

    private minigameSpawner _spawner;
    private float _elapsed;
    private Vector3 _barFullScale;
    private float _barLeftEdge;
    private float _barSpriteMinX;
    
    protected override void MgStart()
    {
        _spawner = GameObject.Find("game").transform.Find("investitSpawner").GetComponent<minigameSpawner>();
        InitBar();
        StartCoroutine(End(gameTime));
    }

    protected override void MgUpdate()
    {
        _elapsed += Time.deltaTime;   // same clock as the WaitForSeconds in End
        UpdateBar();
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