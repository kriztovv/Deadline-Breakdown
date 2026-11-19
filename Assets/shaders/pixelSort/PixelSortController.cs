using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Drives the pixel-sort death effect. Call Trigger() once the energy effect has settled:
/// the current frame is captured, then sorted column by column over time (dark pixels sink,
/// like a melt) and shown frozen over the game. Call Stop() on respawn.
/// All "Curve" fields use X = normalized time (0..1 over Duration), Y = 0..1.
/// </summary>
public class PixelSortController : MonoBehaviour
{
    public enum SortKey { Luminance = 0, Brightness = 1, Saturation = 2, Hue = 3, Red = 4, Green = 5, Blue = 6 }

    enum State { Idle, WaitingForCapture, Running }

    public static PixelSortController Instance { get; private set; }

    [Header("Timing")]
    [SerializeField] float duration = 3f;
    [Tooltip("Keep showing the sorted image after Duration ends (until Stop() is called).")]
    [SerializeField] bool holdAtEnd = true;
    [SerializeField] bool unscaledTime = true;   // keeps running when Time.timeScale = 0

    [Header("Quality / cost")]
    [Tooltip("Resolution of the sort relative to the camera. 0.5 = chunkier streaks, a quarter of the cost.")]
    [Range(0.25f, 1f)] [SerializeField] float resolutionScale = 1f;
    [Tooltip("ARGBHalf copies pixels exactly. RGB111110Float halves the memory traffic.")]
    [SerializeField] RenderTextureFormat format = RenderTextureFormat.ARGBHalf;
    [Tooltip("Safety cap on sort steps per frame (each step is one fullscreen pass).")]
    [SerializeField] int maxPassesPerFrame = 32;

    [Header("Speed")]
    [Tooltip("How far streaks travel, in screen heights per second, at curve = 1. One pass moves a pixel one texel.")]
    [SerializeField] float meltSpeed = 0.6f;
    [SerializeField] AnimationCurve speedCurve = Make((0f, 1f), (1f, 0.25f));

    [Header("What gets sorted")]
    [SerializeField] SortKey sortKey = SortKey.Luminance;
    [Tooltip("Off: dark pixels sink down (streaks hang below dark features). On: bright pixels sink.\n" +
             "If it looks upside-down on your platform, flip this.")]
    [SerializeField] bool invertDirection = false;
    [Tooltip("Only pixels whose key is between these two curves are sorted. Widen over time to spread the effect. Y = key value 0..1.")]
    [SerializeField] AnimationCurve rangeMinCurve = Make((0f, 0f), (1f, 0f));
    [SerializeField] AnimationCurve rangeMaxCurve = Make((0f, 0.35f), (0.5f, 0.7f), (1f, 0.7f));
    [Tooltip("0 = every column melts at the same speed. Higher = ragged streak lengths.")]
    [Range(0f, 1f)] [SerializeField] float columnVariation = 0.5f;

    [Header("Display")]
    [Range(0f, 1f)] [SerializeField] float maxOpacity = 1f;
    [SerializeField] AnimationCurve opacityCurve = Make((0f, 1f), (1f, 1f));

    [Header("Gradient map")]
    [Tooltip("Recolours the sorted image by brightness: left end = darkest pixels, right end = brightest. " +
             "Use a normal (non-HDR) gradient.")]
    [SerializeField] Gradient gradient = DefaultGradient();
    [Tooltip("Strength of the gradient map at curve = 1. 0 = original colours.")]
    [Range(0f, 1f)] [SerializeField] float maxGradientStrength = 1f;
    [Tooltip("X = normalized time, Y = 0..1. How strongly the gradient map is applied over time.")]
    [SerializeField] AnimationCurve gradientStrengthCurve = Make((0f, 0f), (0.5f, 1f), (1f, 1f));

    // ---- state shared with PixelSortFeature ----
    static readonly int SortSizeId  = Shader.PropertyToID("_SortSize");
    static readonly int RangeId     = Shader.PropertyToID("_SortRange");
    static readonly int KeyModeId   = Shader.PropertyToID("_KeyMode");
    static readonly int DirId       = Shader.PropertyToID("_Dir");
    static readonly int ColVarId    = Shader.PropertyToID("_ColumnVariation");
    static readonly int SeedId      = Shader.PropertyToID("_Seed");
    static readonly int OpacityId   = Shader.PropertyToID("_Opacity");
    static readonly int GradientTexId      = Shader.PropertyToID("_GradientTex");
    static readonly int GradientStrengthId = Shader.PropertyToID("_GradientStrength");

    State state = State.Idle;
    float time;
    float passAccum;
    int pendingPasses;
    int parityCounter;
    bool latestIsA = true;

    const int GradientSize = 256;
    Texture2D gradientTex;
    bool gradientDirty = true;

    RenderTexture[] textures = new RenderTexture[2];
    RTHandle[] handles = new RTHandle[2];
    int texWidth, texHeight;

    public bool IsActive => state != State.Idle;
    public bool NeedsCapture => state == State.WaitingForCapture;
    public RTHandle HandleA => handles[0];
    public RTHandle HandleB => handles[1];
    public bool LatestIsA { get => latestIsA; set => latestIsA = value; }

    float NormalizedTime => duration <= 0.0001f ? 1f : Mathf.Clamp01(time / duration);

    void OnEnable()  => Instance = this;

    // Editing the gradient in the Inspector (also while playing) re-bakes it on the next frame.
    void OnValidate() => gradientDirty = true;

    void OnDisable()
    {
        if (Instance == this) Instance = null;
        Stop();
    }

    /// <summary>Capture the current frame and start melting it.</summary>
    [ContextMenu("Trigger")]
    public void Trigger()
    {
        pendingPasses = 0;
        passAccum = 0f;
        time = 0f;
        parityCounter = 0;
        state = State.WaitingForCapture;   // the render pass takes the snapshot on the next frame
    }

    /// <summary>Wait (real time) before capturing, e.g. for the energy effect to ease out.</summary>
    public void TriggerAfterDelay(float seconds) => StartCoroutine(TriggerDelayed(seconds));

    IEnumerator TriggerDelayed(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Trigger();
    }

    /// <summary>Remove the effect and free the textures (call on respawn).</summary>
    [ContextMenu("Stop")]
    public void Stop()
    {
        state = State.Idle;
        pendingPasses = 0;
        ReleaseTargets();
        ReleaseGradient();
    }

    void Update()
    {
        if (state != State.Running) return;

        float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        time += dt;

        if (time >= duration)
        {
            if (!holdAtEnd) { Stop(); return; }
            time = duration;   // keep showing the final sorted image, no more sorting
            return;
        }

        // Passes per second so that streaks travel "meltSpeed" screen heights per second.
        float t = NormalizedTime;
        float height = texHeight > 0 ? texHeight : Screen.height * resolutionScale;
        passAccum += meltSpeed * height * Ev(speedCurve, t) * dt;

        int whole = (int)passAccum;
        passAccum -= whole;
        pendingPasses = Mathf.Min(pendingPasses + whole, maxPassesPerFrame);
    }

    // ---------------- called by the render pass ----------------

    /// <summary>(Re)allocates the two sort textures for the given camera size.</summary>
    public void EnsureTargets(int cameraWidth, int cameraHeight)
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(cameraWidth * resolutionScale));
        int h = Mathf.Max(8, Mathf.RoundToInt(cameraHeight * resolutionScale));
        if (textures[0] != null && texWidth == w && texHeight == h) return;

        bool hadContent = textures[0] != null && state == State.Running;
        ReleaseTargets();

        for (int i = 0; i < 2; i++)
        {
            var rt = new RenderTexture(w, h, 0, format, RenderTextureReadWrite.Linear)
            {
                name = "PixelSort_" + (i == 0 ? "A" : "B"),
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            rt.Create();
            textures[i] = rt;
            handles[i] = RTHandles.Alloc(rt);
        }
        texWidth = w;
        texHeight = h;

        // Window was resized mid-effect: the old contents are gone, so take a new snapshot.
        if (hadContent)
        {
            state = State.WaitingForCapture;
            time = 0f;
            pendingPasses = 0;
        }
    }

    public void MarkCaptured()
    {
        latestIsA = true;      // the snapshot was written into texture A
        passAccum = 0f;
        pendingPasses = 0;
        parityCounter = 0;
        time = 0f;
        state = State.Running;
    }

    public int ConsumePasses()
    {
        int n = pendingPasses;
        pendingPasses = 0;
        return n;
    }

    /// <summary>Shader pass index for the next sort step (1 = even pairs, 2 = odd pairs, alternating).</summary>
    public int NextSortShaderPass() => 1 + (parityCounter++ & 1);

    public void ApplyToMaterial(Material m)
    {
        float t = NormalizedTime;
        float lo = Ev(rangeMinCurve, t);
        float hi = Mathf.Max(lo, Ev(rangeMaxCurve, t));

        m.SetVector(SortSizeId, new Vector4(texWidth, texHeight, 1f / Mathf.Max(1, texWidth), 1f / Mathf.Max(1, texHeight)));
        m.SetVector(RangeId, new Vector4(lo, hi, 0f, 0f));
        m.SetFloat(KeyModeId, (float)sortKey);
        m.SetFloat(DirId, invertDirection ? -1f : 1f);
        m.SetFloat(ColVarId, columnVariation);
        m.SetFloat(SeedId, Time.frameCount % 9973);
        m.SetFloat(OpacityId, Ev(opacityCurve, t) * maxOpacity);

        EnsureGradientTexture();
        m.SetTexture(GradientTexId, gradientTex);
        m.SetFloat(GradientStrengthId, Ev(gradientStrengthCurve, t) * maxGradientStrength);
    }

    // ---------------- helpers ----------------

    void ReleaseTargets()
    {
        for (int i = 0; i < 2; i++)
        {
            if (handles[i] != null) { handles[i].Release(); handles[i] = null; }
            if (textures[i] != null)
            {
                textures[i].Release();
                Destroy(textures[i]);
                textures[i] = null;
            }
        }
        texWidth = texHeight = 0;
    }

    // Bakes the Gradient into a 256x1 texture. It is stored as an sRGB texture, so in a Linear
    // colour-space project the GPU converts it to linear on sampling, exactly like a normal colour.
    void EnsureGradientTexture()
    {
        if (gradientTex == null)
        {
            gradientTex = new Texture2D(GradientSize, 1, TextureFormat.RGBA32, false, false)
            {
                name = "PixelSort_Gradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            gradientDirty = true;
        }

        if (!gradientDirty) return;

        if (gradient == null) gradient = DefaultGradient();
        var px = new Color32[GradientSize];
        for (int i = 0; i < GradientSize; i++)
            px[i] = gradient.Evaluate(i / (float)(GradientSize - 1));
        gradientTex.SetPixels32(px);
        gradientTex.Apply(false);
        gradientDirty = false;
    }

    void ReleaseGradient()
    {
        if (gradientTex != null)
        {
            Destroy(gradientTex);
            gradientTex = null;
        }
        gradientDirty = true;
    }

    // Black -> deep red -> pale cream. Replace it in the Inspector.
    static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.02f, 0.0f, 0.0f), 0f),
                new GradientColorKey(new Color(0.55f, 0.05f, 0.05f), 0.5f),
                new GradientColorKey(new Color(1.0f, 0.88f, 0.75f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return g;
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