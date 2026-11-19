using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scrolling line graph for an energy value (0-100 by default).
/// All drawing happens in the "UI/EnergyGraph" shader; this component owns the sample history,
/// smooths the "dull" / "danger" state, and feeds the material.
///
/// Usage: add to an empty UI object under a Canvas (it replaces the Image component),
/// then call PushSample(energy) once per second.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("UI/Energy Graph")]
public class EnergyGraph : MaskableGraphic
{
    /// <summary>Number of samples kept. Must match SAMPLES in EnergyGraph.shader.</summary>
    public const int SampleCount = 64;

    /// <summary>Vertical grid line spacing in samples. Must match GRID_SPAN in EnergyGraph.shader.</summary>
    const int GridSpanSamples = 6;

    [Header("Energy scale")]
    [SerializeField] float maxEnergy = 100f;
    [Tooltip("At and below this level the graph looks calm; under it, the graph goes dull.")]
    [SerializeField] float baselineEnergy = 20f;
    [Tooltip("Danger effects start fading in here and reach full strength at max energy.")]
    [SerializeField] float dangerStartEnergy = 80f;

    [Header("Timing")]
    [Tooltip("Seconds between PushSample calls. Used to scroll the graph smoothly between samples.")]
    [SerializeField] float sampleInterval = 1f;
    [Tooltip("How quickly the dull / danger look follows the latest energy value (seconds).")]
    [SerializeField] float stateSmoothTime = 0.5f;

    [Header("Rendering")]
    [Tooltip("Assign UI/EnergyGraph. If empty, the component tries Shader.Find at runtime, " +
             "which only works in builds if the shader is referenced somewhere.")]
    [SerializeField] Shader shader;

    // Property IDs
    static readonly int SamplesId = Shader.PropertyToID("_Samples");
    static readonly int ScrollId = Shader.PropertyToID("_Scroll");
    static readonly int GridPhaseId = Shader.PropertyToID("_GridPhase");
    static readonly int DullId = Shader.PropertyToID("_Dull");
    static readonly int DangerId = Shader.PropertyToID("_Danger");
    static readonly int BaselineId = Shader.PropertyToID("_Baseline");
    static readonly int DangerLineId = Shader.PropertyToID("_DangerLine");
    static readonly int RectPxId = Shader.PropertyToID("_RectPx");

    readonly float[] _samples = new float[SampleCount];   // normalised 0-1, oldest first
    Material _mat;
    bool _hasData;
    int _pushCount;
    float _sinceLastSample;
    float _latest;           // latest raw energy
    float _shown;            // smoothed energy that drives dull / danger
    float _shownVelocity;

    /// <summary>Latest energy value that was pushed.</summary>
    public float Energy => _latest;

    /// <summary>0 at or above baseline, rising to 1 at zero energy. Other systems can read this.</summary>
    public float Dull01
    {
        get
        {
            float t = Mathf.InverseLerp(0f, Mathf.Max(baselineEnergy, 0.0001f), _shown);
            return 1f - Mathf.SmoothStep(0f, 1f, t);
        }
    }

    /// <summary>0 below the danger start, rising to 1 at max energy. Other systems can read this.</summary>
    public float Danger01
    {
        get
        {
            float t = Mathf.InverseLerp(dangerStartEnergy, Mathf.Max(maxEnergy, dangerStartEnergy + 0.0001f), _shown);
            return Mathf.SmoothStep(0f, 1f, t);
        }
    }

    /// <summary>Add a newly calculated energy value. Call once per sampleInterval.</summary>
    public void PushSample(float energy)
    {
        float v = Mathf.Clamp01(energy / Mathf.Max(maxEnergy, 0.0001f));

        if (!_hasData)
        {
            // First value: start with a flat history so there is no fake ramp from zero.
            for (int i = 0; i < SampleCount; i++) _samples[i] = v;
            _hasData = true;
            _shown = energy;
            _shownVelocity = 0f;
        }
        else
        {
            System.Array.Copy(_samples, 1, _samples, 0, SampleCount - 1);
            _samples[SampleCount - 1] = v;
        }

        _latest = energy;
        _pushCount++;
        _sinceLastSample = 0f;
        PushToMaterial();
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        _sinceLastSample += Time.deltaTime;
        _shown = Mathf.SmoothDamp(_shown, _latest, ref _shownVelocity, Mathf.Max(0.0001f, stateSmoothTime));
        PushToMaterial();
    }

    void PushToMaterial()
    {
        if (_mat == null) return;

        float t = Mathf.Clamp01(_sinceLastSample / Mathf.Max(sampleInterval, 0.0001f));
        float max = Mathf.Max(maxEnergy, 0.0001f);

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 sizePx = rectTransform.rect.size * scale;

        // Note: if this graph sits inside a Mask / RectMask2D, Unity renders a derived copy of the
        // material for the stencil; per-frame changes to _mat may not reach it. A HUD graph normally isn't masked.
        _mat.SetFloatArray(SamplesId, _samples);
        _mat.SetFloat(ScrollId, t);
        _mat.SetFloat(GridPhaseId, (_pushCount % GridSpanSamples) + t);
        _mat.SetFloat(DullId, Dull01);
        _mat.SetFloat(DangerId, Danger01);
        _mat.SetFloat(BaselineId, Mathf.Clamp01(baselineEnergy / max));
        _mat.SetFloat(DangerLineId, Mathf.Clamp01(dangerStartEnergy / max));
        _mat.SetVector(RectPxId, new Vector4(sizePx.x, sizePx.y, 0f, 0f));
    }

    // ---- Mesh --------------------------------------------------------------

    // The shader works in panel space and needs UVs running 0-1 across the rect
    // (bottom-left = 0,0), so the quad is built explicitly instead of relying on the default.
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        Rect r = GetPixelAdjustedRect();
        Color32 c = color;

        vh.Clear();
        vh.AddVert(new Vector3(r.xMin, r.yMin), c, new Vector2(0f, 0f));
        vh.AddVert(new Vector3(r.xMin, r.yMax), c, new Vector2(0f, 1f));
        vh.AddVert(new Vector3(r.xMax, r.yMax), c, new Vector2(1f, 1f));
        vh.AddVert(new Vector3(r.xMax, r.yMin), c, new Vector2(1f, 0f));
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }

    // ---- Material lifetime -------------------------------------------------

    void EnsureMaterial()
    {
        if (_mat != null) return;

        Shader s = shader != null ? shader : Shader.Find("UI/EnergyGraph");
        if (s == null)
        {
            Debug.LogWarning("EnergyGraph: shader 'UI/EnergyGraph' not found. Assign it on the component.", this);
            return;
        }

        _mat = new Material(s)
        {
            name = "EnergyGraph (Instance)",
            hideFlags = HideFlags.HideAndDontSave
        };
        material = _mat;
    }

    void DestroyMaterial()
    {
        if (_mat == null) return;

        material = null;
        if (Application.isPlaying) Destroy(_mat);
        else DestroyImmediate(_mat);
        _mat = null;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureMaterial();

        if (!_hasData)
        {
            // Preview (also in edit mode): flat line at baseline until the first real sample arrives.
            float v = Mathf.Clamp01(baselineEnergy / Mathf.Max(maxEnergy, 0.0001f));
            for (int i = 0; i < SampleCount; i++) _samples[i] = v;
            _latest = baselineEnergy;
            _shown = baselineEnergy;
        }

        PushToMaterial();
    }

    protected override void OnDisable()
    {
        DestroyMaterial();
        base.OnDisable();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        PushToMaterial();
    }

#if UNITY_EDITOR
    protected override void Reset()
    {
        base.Reset();

        // Top-right corner, 360 x 140, 24 px margin.
        RectTransform rt = rectTransform;
        rt.anchorMin = Vector2.one;
        rt.anchorMax = Vector2.one;
        rt.pivot = Vector2.one;
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(360f, 140f);

        raycastTarget = false;
        shader = Shader.Find("UI/EnergyGraph");
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        maxEnergy = Mathf.Max(1f, maxEnergy);
        baselineEnergy = Mathf.Clamp(baselineEnergy, 0.1f, maxEnergy);
        dangerStartEnergy = Mathf.Clamp(dangerStartEnergy, baselineEnergy, maxEnergy);
        sampleInterval = Mathf.Max(0.01f, sampleInterval);
        PushToMaterial();
    }
#endif
}