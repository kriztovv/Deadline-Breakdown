using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class pressBombardment : minigameController
{
    private enum Phase { Reading, Answering, Feedback, Finished }

    private const int DefaultOption = 3;   // selected when no keys are held
    private static readonly string[] OptionNames = { "top", "middle", "bottom", "default" };

    [Header("Sounds")]
    [SerializeField] private AudioClip tickLoopSound;
    [SerializeField, Range(0f, 1f)] private float tickVolume = 0.5f;
    [SerializeField] private AudioClip correctSound;
    [SerializeField, Range(0f, 1f)] private float correctVolume = 0.8f;
    [SerializeField] private AudioClip wrongSound;
    [SerializeField, Range(0f, 1f)] private float wrongVolume = 0.8f;
    [SerializeField] private AudioClip moveSound;
    [SerializeField, Range(0f, 1f)] private float moveVolume = 0.4f;

    private AudioSource _tickSource;
    
    [Header("Questions")]
    [SerializeField] private string resourcesFolder = "BusinessQuestions";   // a folder inside any Resources folder
    [SerializeField] private businessQuestion[] questions;                   // filled from the folder when the game starts (or assign by hand)
    [SerializeField] private int questionCount = 10;                         // Z: how many questions are asked

    [Header("Scene objects")]
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private GameObject[] answerObjects;                     // top, middle, bottom, default
    [SerializeField] private Color hoverTint = new Color(1f, 0.92f, 0.5f, 1f);

    [Header("Feedback after confirming")]
    [SerializeField] private Color correctColor = new Color(0.3f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color wrongColor = new Color(0.9f, 0.25f, 0.25f, 1f);
    [SerializeField] private float feedbackTime = 1f;       // how long the flashing lasts before the next question
    [SerializeField] private float flashInterval = 0.15f;   // how fast it blinks

    [Header("Background")]
    [SerializeField] private GameObject backgroundObject;   // the object that shows the background (has a SpriteRenderer or a UI Image)
    [SerializeField] private Sprite neutralBackground;      // the default one, shown most of the time
    [SerializeField] private Sprite correctBackground;      // shown after a correct answer
    [SerializeField] private Sprite wrongBackground;        // shown after a wrong answer
    [SerializeField] private float backgroundHoldTime = 1.5f;   // how long the correct / wrong one stays before going back to neutral

    [Header("Timer")]
    [SerializeField] private Transform timerBar;
    private Vector3 _barFullScale;
    private float _barLeftEdge;
    private float _barSpriteMinX;
    
    [Header("Timing: X = time until the answers appear, Y = time to answer")]
    [SerializeField] public float startReadTime = 2f;      // X at the start
    [SerializeField] private float readTimeStep = 0.25f;     // how much X drops after each question
    [SerializeField] private float minReadTime = 0.5f;
    [SerializeField] public float startAnswerTime = 4f;    // Y at the start
    [SerializeField] private float answerTimeStep = 0.25f;   // how much Y drops after each question
    [SerializeField] private float minAnswerTime = 1.5f;
    [SerializeField] private bool timersIgnoreTimeScale;

    [Header("Input")]
    [SerializeField] private KeyCode confirmKey = KeyCode.Space;
    
    [SerializeField] private bool shuffleAnswers = true;
    private int[] _order = new int[0];   // _order[slot] = index of the answer shown in that slot

    // Can also be set from another script before the game starts
    public int QuestionCount { get => questionCount; set => questionCount = value; }

    private readonly List<businessQuestion> _pool = new List<businessQuestion>();   // questions not asked yet on this load
    private TMP_Text[] _answerTexts;
    private SpriteRenderer[] _answerSprites;
    private Color[] _baseColors;

    private bool _perfect = true;
    private businessQuestion _current;
    private Phase _phase = Phase.Finished;   // stays finished until the game has started
    private float _timer;
    private float _readTime, _answerTime;
    private int _asked, _correct, _incorrect;
    private int _hover = -1;                 // 0 = top, 1 = middle, 2 = bottom, 3 = default, -1 = none

    private SpriteRenderer _backgroundSprite;
    private UnityEngine.UI.Image _backgroundImage;
    private float _backgroundTimer;          // time left until the background goes back to neutral

    public void StartGame()
    {
        CacheAnswerObjects();
        CacheBackground();
        SetBackground(neutralBackground);
        InitBar();
        SetBarVisible(false);
        
        if (_tickSource == null && tickLoopSound != null)
        {
            _tickSource = gameObject.AddComponent<AudioSource>();
            _tickSource.clip = tickLoopSound;
            _tickSource.loop = true;
            _tickSource.playOnAwake = false;
            _tickSource.spatialBlend = 0f;   // 2D
            _tickSource.volume = tickVolume;
        }
        
        if (questions == null || questions.Length == 0) questions = Resources.LoadAll<businessQuestion>(resourcesFolder);

        _pool.Clear();
        foreach (businessQuestion q in questions) if (q != null) _pool.Add(q);

        if (_pool.Count == 0)
        {
            Debug.LogWarning($"business: no questions found in Resources/{resourcesFolder}");
            return;
        }

        _asked = _correct = _incorrect = 0;
        _readTime = startReadTime;
        _answerTime = startAnswerTime;
        NextQuestion();
    }

    private void CacheAnswerObjects()
    {
        int n = answerObjects.Length;
        _answerTexts = new TMP_Text[n];
        _answerSprites = new SpriteRenderer[n];
        _baseColors = new Color[n];

        for (int i = 0; i < n; i++)
        {
            _answerTexts[i] = answerObjects[i].GetComponentInChildren<TMP_Text>(true);
            _answerSprites[i] = answerObjects[i].GetComponentInChildren<SpriteRenderer>(true);

            // the colors are applied to the background sprite if there is one, otherwise to the text
            _baseColors[i] = _answerSprites[i] != null ? _answerSprites[i].color
                           : _answerTexts[i] != null ? _answerTexts[i].color
                           : Color.white;
        }
    }

    //Question flow
    private void NextQuestion()
    {
        if (_asked >= questionCount || _pool.Count == 0)
        {
            if (_asked < questionCount) Debug.LogWarning($"business: only {_asked} questions were available, {questionCount} were requested");
            Finish();
            return;
        }

        int index = UnityEngine.Random.Range(0, _pool.Count);
        _current = _pool[index];
        _pool.RemoveAt(index);   // it can't come up again on this load
        BuildOrder();
        _asked++;

        SetHover(-1);
        HideAnswers();
        SetBarVisible(false);
        questionText.text = _current.questionText;

        _phase = Phase.Reading;
        _timer = _readTime;
        // ANIMATION: the question text appears
    }
    
    private void BuildOrder()
    {
        int n = _current.answers.Length;
        _order = new int[n];
        for (int i = 0; i < n; i++) _order[i] = i;   // identity, so the "I don't know" slot (3) never moves
        if (!shuffleAnswers) return;

        for (int i = 2; i > 0; i--)                  // Fisher-Yates over slots 0-2 only
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (_order[i], _order[j]) = (_order[j], _order[i]);
        }
    }

    private void ShowAnswers()
    {
        for (int i = 0; i < answerObjects.Length; i++)
        {
            bool has = i < _current.answers.Length;
            answerObjects[i].SetActive(has);
            if (has && _answerTexts[i] != null) _answerTexts[i].text = _current.answers[_order[i]].text;
        }

        _phase = Phase.Answering;
        _timer = _answerTime;
        SetBarVisible(true);
        SetTick(true);
        UpdateBar();
        SetHover(CurrentChoice());
        // ANIMATION: the answers appear (slide in, fade in)
    }

    private void HideAnswers()
    {
        foreach (GameObject answer in answerObjects) answer.SetActive(false);
    }

    protected override void MgUpdate()
    {
        UpdateBackground();
        
        if (_phase == Phase.Finished) return;

        _timer -= timersIgnoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

        switch (_phase)
        {
            case Phase.Reading:
                if (_timer <= 0f) ShowAnswers();
                break;

            case Phase.Answering:
                SetHover(CurrentChoice());
                UpdateBar();                         // _timer / _answerTime, only while answering
                if (_timer <= 0f) Confirm(true);     // out of time: whatever is hovered is locked in
                _tickSource.pitch = Mathf.Lerp(1.4f, 1f, _timer / _answerTime);
                break;

            case Phase.Feedback:
                if (_timer <= 0f) EndFeedback();
                else ApplyFeedback(Mathf.FloorToInt((feedbackTime - _timer) / flashInterval) % 2 == 0);
                break;
        } 
    }
    
    private void SetBarVisible(bool visible)
    {
        if (timerBar != null && timerBar.gameObject.activeSelf != visible)
            timerBar.gameObject.SetActive(visible);
    }

    protected override void OnKeyPressed(KeyCode key)
    {
        if (key == confirmKey && _phase == Phase.Answering) Confirm(false);
    }

    //Input: D hovers the middle answer, W+D the top one, S+D the bottom one, nothing held = the default one
    private int CurrentChoice()
    {
        Vector2 m = MovementInput;   // needs both movement toggles on in the controller

        if (m.x <= 0.5f) return DefaultOption;   // D isn't held
        if (m.y > 0.5f) return 0;                // W + D
        if (m.y < -0.5f) return 2;               // S + D
        return 1;                                // D
    }

    //Confirming and feedback
    private void Confirm(bool timedOut)
    {
        int index = CurrentChoice();
        bool correct = index < _order.Length && _current.answers[_order[index]].isCorrect;
        
        if (correct) Play(correctSound, correctVolume);
        else Play(wrongSound, wrongVolume);

        if (!correct)
        {
            AssignmentManager.Instance.Worsen("Marketing");
            _perfect = false;
        }
        ShowBackground(correct);
        Debug.Log($"Question {_asked}/{questionCount}: {(correct ? "CORRECT" : "INCORRECT")} (chose {OptionNames[index]}{(timedOut ? ", time ran out" : "")})");

        // The next question is a little harder
        _readTime = Mathf.Max(minReadTime, _readTime - readTimeStep);
        _answerTime = Mathf.Max(minAnswerTime, _answerTime - answerTimeStep);

        SetHover(-1);   // the hover tint gives way to the feedback colors
        SetBarVisible(false);
        SetTick(false);
        _phase = Phase.Feedback;
        _timer = feedbackTime;
        ApplyFeedback(true);
        // ANIMATION: sound for a correct or a wrong answer
    }

    // Correct answers go green, wrong ones red, blinking between that and their normal color
    private void ApplyFeedback(bool on)
    {
        for (int i = 0; i < answerObjects.Length; i++)
        {
            if (!answerObjects[i].activeSelf) continue;

            bool isCorrect = i < _order.Length && _current.answers[_order[i]].isCorrect;
            SetAnswerColor(i, on ? (isCorrect ? correctColor : wrongColor) : _baseColors[i]);
        }
    }

    private void EndFeedback()
    {
        for (int i = 0; i < answerObjects.Length; i++) SetAnswerColor(i, _baseColors[i]);
        NextQuestion();
    }

    private void Finish()
    {
        _phase = Phase.Finished;
        _backgroundTimer = 0f;
        SetBackground(neutralBackground);
        SetHover(-1);
        HideAnswers();
        SetBarVisible(false);
        SetTick(false);
        questionText.text = "";

        Debug.Log($"Business finished");
        
        if (_perfect) AssignmentManager.Instance.Improve("Marketing");
        
        ClassroomInput.Instance.CloseMinigame(true);
        // ANIMATION: results screen
    }

    //Background
    private void CacheBackground()
    {
        if (backgroundObject == null) return;
        _backgroundSprite = backgroundObject.GetComponentInChildren<SpriteRenderer>(true);
        _backgroundImage = backgroundObject.GetComponentInChildren<UnityEngine.UI.Image>(true);
    }

    // the correct / wrong background stays for backgroundHoldTime seconds, then it goes back to neutral
    private void ShowBackground(bool correct)
    {
        Sprite s = correct ? correctBackground : wrongBackground;
        if (s == null) return;

        SetBackground(s);
        _backgroundTimer = backgroundHoldTime;   // answering again before it ran out restarts the time
    }

    private void UpdateBackground()
    {
        if (_backgroundTimer <= 0f) return;

        _backgroundTimer -= timersIgnoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
        if (_backgroundTimer <= 0f) SetBackground(neutralBackground);
    }

    private void SetBackground(Sprite s)
    {
        if (s == null) return;
        if (_backgroundSprite != null) _backgroundSprite.sprite = s;
        if (_backgroundImage != null) _backgroundImage.sprite = s;
    }

    //Colors
    private void SetHover(int index)
    {
        if (index == _hover) return;

        // only when moving between two options while answering, not when the answers appear or the hover is cleared
        if (_phase == Phase.Answering && _hover >= 0 && index >= 0) Play(moveSound, moveVolume);

        SetHighlight(_hover, false);
        _hover = index;
        SetHighlight(_hover, true);
    }
    
    private void Play(AudioClip clip, float volume)
    {
        if (clip != null) SFXManager.instance.PlaySFX(clip, transform, volume);
    }

    private void SetTick(bool on)
    {
        if (_tickSource == null) return;
        if (on && !_tickSource.isPlaying) _tickSource.Play();
        else if (!on && _tickSource.isPlaying) _tickSource.Stop();
    }

    private void SetHighlight(int i, bool on)
    {
        if (i < 0 || i >= answerObjects.Length) return;

        SetAnswerColor(i, on ? _baseColors[i] * hoverTint : _baseColors[i]);
        // ANIMATION: hover effect (scale up, glow, sound)
    }

    private void SetAnswerColor(int i, Color c)
    {
        if (_answerSprites[i] != null) _answerSprites[i].color = c;
        else if (_answerTexts[i] != null) _answerTexts[i].color = c;
    }
    
    // ---------- timer bar ----------

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

        float fraction = _answerTime > 0f ? Mathf.Clamp01(_timer / _answerTime) : 0f;

        Vector3 scale = _barFullScale;
        scale.x = _barFullScale.x * fraction;
        timerBar.localScale = scale;

        Vector3 pos = timerBar.localPosition;
        pos.x = _barLeftEdge - _barSpriteMinX * scale.x;
        timerBar.localPosition = pos;
    }
}