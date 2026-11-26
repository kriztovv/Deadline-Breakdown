using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the zoom-whip transition on the Hidden/SceneTransition material and calls your code
/// at the moment the screen is most covered, so you can swap scenes there.
///
///   SceneTransition.Instance.Play(() => MySwapRoutine());
///
/// The transition holds at its peak until MySwapRoutine() has finished, then plays out.
/// All curves use X = normalized time (0..1 over Duration), Y = 0..1.
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Header("Material used by the Full Screen Pass Renderer Feature")]
    [SerializeField] Material material;

    [Header("Timing")]
    [SerializeField] float duration = 1.1f;
    [Tooltip("Where in the timeline (0..1) the swap happens. Put the peak of the curves here. " +
             "Lower = quick build-up and a long release, like the reference.")]
    [Range(0.05f, 0.95f)] [SerializeField] float midpoint = 0.38f;
    [Tooltip("Keeps playing when Time.timeScale is slowed (your pills slow time).")]
    [SerializeField] bool unscaledTime = true;
    [Tooltip("Safety net: if the swap code takes longer than this, the transition continues anyway.")]
    [SerializeField] float maxHoldSeconds = 4f;

    [Header("Look")]
    [Tooltip("Screen position (0..1) the zoom and the streaks converge on.")]
    [SerializeField] Vector2 center = new Vector2(0.5f, 0.5f);
    [Tooltip("Direction of the sideways whip.")]
    [SerializeField] Vector2 whipDirection = new Vector2(-1f, 0f);
    [Tooltip("Mirror the whip direction on half of the transitions.")]
    [SerializeField] bool flipWhipRandomly = true;
    [Tooltip("Blur quality. Each sample is a screen read (three with chromatic aberration).")]
    [Range(4, 48)] [SerializeField] int samples = 24;

    [Header("Maximum amounts (curve value of 1 = this)")]
    [Tooltip("Extra zoom. 0.35 = 35% push-in.")]
    [SerializeField] float maxZoom = 0.35f;
    [Tooltip("Radial blur: fraction of the distance to the centre that is smeared.")]
    [SerializeField] float maxZoomBlur = 0.6f;
    [Tooltip("Sideways smear length in screen widths.")]
    [SerializeField] float maxWhip = 0.12f;
    [Tooltip("Red/blue colour split.")]
    [SerializeField] float maxChroma = 0.05f;
    [Range(0f, 1f)] [SerializeField] float maxFlash = 0f;
    [SerializeField] [ColorUsage(false, true)] Color flashColor = Color.white;

    [Header("Curves (X = normalized time, Y = 0..1)")]
    [SerializeField] AnimationCurve zoomCurve     = Make((0f, 0f), (0.38f, 1f), (1f, 0f));
    [SerializeField] AnimationCurve zoomBlurCurve = Make((0f, 0f), (0.38f, 1f), (1f, 0f));
    [SerializeField] AnimationCurve whipCurve     = Make((0f, 0f), (0.24f, 1f), (0.45f, 0.2f), (1f, 0f));
    [SerializeField] AnimationCurve chromaCurve   = Make((0f, 0f), (0.3f, 0.5f), (0.42f, 1f), (1f, 0f));
    [SerializeField] AnimationCurve flashCurve    = Make((0f, 0f), (0.34f, 0f), (0.38f, 1f), (0.46f, 0f), (1f, 0f));

    static readonly int ZoomId       = Shader.PropertyToID("_Zoom");
    static readonly int ZoomBlurId   = Shader.PropertyToID("_ZoomBlur");
    static readonly int WhipId       = Shader.PropertyToID("_Whip");
    static readonly int WhipDirId    = Shader.PropertyToID("_WhipDir");
    static readonly int ChromaId     = Shader.PropertyToID("_Chroma");
    static readonly int CenterId     = Shader.PropertyToID("_Center");
    static readonly int FlashId      = Shader.PropertyToID("_Flash");
    static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    static readonly int SamplesId    = Shader.PropertyToID("_Samples");

    Vector2 whipDir = Vector2.left;

    public bool IsPlaying { get; private set; }

    void OnEnable()
    {
        Instance = this;
        ResetMaterial();
    }

    void OnDisable()
    {
        if (Instance == this) Instance = null;
        IsPlaying = false;
        ResetMaterial();   // the material is a project asset: always put it back
    }

    [ContextMenu("Play (test)")]
    void PlayTest() => Play();

    /// <summary>
    /// Plays the transition. onMidpoint is a coroutine method that runs at the peak, while the
    /// screen is covered; the transition waits for it to finish before playing out.
    /// Returns a coroutine you can yield on, or null if a transition is already playing.
    /// </summary>
    public Coroutine Play(System.Func<IEnumerator> onMidpoint = null)
    {
        if (IsPlaying) return null;
        return StartCoroutine(Run(onMidpoint));
    }

    IEnumerator Run(System.Func<IEnumerator> onMidpoint)
    {
        IsPlaying = true;

        whipDir = whipDirection.sqrMagnitude > 0.0001f ? whipDirection.normalized : Vector2.left;
        if (flipWhipRandomly && Random.value < 0.5f) whipDir = -whipDir;

        // Phase 1: build up to the peak.
        float t = 0f;
        while (t < midpoint)
        {
            t += Step() / Mathf.Max(0.01f, duration);
            Apply(Mathf.Min(t, midpoint));
            yield return null;
        }
        Apply(midpoint);

        // Phase 2: THE PEAK. The screen is covered here: swap scenes now.
        if (onMidpoint != null)
        {
            bool done = false;
            StartCoroutine(RunAndFlag(onMidpoint(), () => done = true));

            float waited = 0f;
            while (!done && waited < maxHoldSeconds)
            {
                waited += Step();
                yield return null;
            }
            if (!done)
                Debug.LogWarning("SceneTransition: the midpoint routine took longer than maxHoldSeconds; continuing.");
        }

        // Phase 3: play out.
        t = midpoint;
        while (t < 1f)
        {
            t += Step() / Mathf.Max(0.01f, duration);
            Apply(Mathf.Min(t, 1f));
            yield return null;
        }

        ResetMaterial();
        IsPlaying = false;
    }

    IEnumerator RunAndFlag(IEnumerator inner, System.Action onDone)
    {
        yield return StartCoroutine(inner);
        onDone();
    }

    // Frame time, capped so a long hitch (scene activation) can't skip part of the ramp-out.
    float Step()
    {
        float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        return Mathf.Min(dt, 0.05f);
    }

    void Apply(float t)
    {
        if (material == null) return;

        material.SetFloat(ZoomId, 1f + Ev(zoomCurve, t) * maxZoom);
        material.SetFloat(ZoomBlurId, Ev(zoomBlurCurve, t) * maxZoomBlur);
        material.SetFloat(WhipId, Ev(whipCurve, t) * maxWhip);
        material.SetVector(WhipDirId, new Vector4(whipDir.x, whipDir.y, 0f, 0f));
        material.SetFloat(ChromaId, Ev(chromaCurve, t) * maxChroma);
        material.SetVector(CenterId, new Vector4(center.x, center.y, 0f, 0f));
        material.SetFloat(FlashId, Ev(flashCurve, t) * maxFlash);
        material.SetColor(FlashColorId, flashColor);
        material.SetFloat(SamplesId, samples);
    }

    void ResetMaterial()
    {
        if (material == null) return;
        material.SetFloat(ZoomId, 1f);
        material.SetFloat(ZoomBlurId, 0f);
        material.SetFloat(WhipId, 0f);
        material.SetFloat(ChromaId, 0f);
        material.SetFloat(FlashId, 0f);
    }

    static float Ev(AnimationCurve c, float t) => Mathf.Clamp01(c.Evaluate(t));

    // Builds a smooth curve from (x, y) points; peaks/valleys get flat tangents so it never overshoots.
    static AnimationCurve Make(params (float x, float y)[] pts)
    {
        var c = new AnimationCurve();
        foreach (var p in pts) c.AddKey(p.x, p.y);

        for (int i = 0; i < c.length; i++)
        {
            bool endpoint = i == 0 || i == c.length - 1;
            bool extremum = !endpoint &&
                ((c[i].value >= c[i - 1].value && c[i].value >= c[i + 1].value) ||
                 (c[i].value <= c[i - 1].value && c[i].value <= c[i + 1].value));

            if (endpoint || extremum)
            {
                Keyframe k = c[i];
                k.inTangent = 0f;
                k.outTangent = 0f;
                c.MoveKey(i, k);
            }
            else
            {
                c.SmoothTangents(i, 0f);
            }
        }
        return c;
    }
}
