using UnityEngine;

/// <summary>
/// Plays the energy-boost pickup effect: zoom punch + flash + yellow tint + delayed zoomed
/// ghost copy with radial blur + Sobel outlines. Call Trigger() when the item is used.
/// Every curve uses X = normalized time (0..1 over "duration"), Y = strength (0..1).
/// The "max" fields define what a curve value of 1 means.
/// </summary>
public class EnergyBoostEffect : MonoBehaviour
{
    [Header("Material used by the Full Screen Pass Renderer Feature")]
    [SerializeField] Material material;

    [Header("Timing")]
    [SerializeField] float duration = 2f;
    [Tooltip("Keeps playing during hit-stop / slow-mo.")]
    [SerializeField] bool unscaledTime = true;

    [Header("Zoom (main image)")]
    [Tooltip("Normalized screen position the zoom is centred on.")]
    [SerializeField] Vector2 zoomCenter = new Vector2(0.5f, 0.5f);
    [Tooltip("Extra zoom at curve = 1. 0.35 = 35% magnification.")]
    [SerializeField] float maxZoom = 0.35f;
    [SerializeField] AnimationCurve zoomCurve = Make((0, 0f), (0.025f, 1f), (0.15f, 0.5f), (0.55f, 0f), (1f, 0f));

    [Header("Ghost copy (overlaid, zooms out later, radial blur)")]
    [Tooltip("Extra zoom of the ghost at curve = 1. Should be larger than Max Zoom.")]
    [SerializeField] float ghostMaxZoom = 0.6f;
    [Tooltip("Make this fall later than the main zoom curve to get the 'delayed' look.")]
    [SerializeField] AnimationCurve ghostZoomCurve    = Make((0, 0f), (0.025f, 1f), (0.2f, 0.95f), (0.7f, 0f), (1f, 0f));
    [Range(0f, 1f)] [SerializeField] float maxGhostOpacity = 0.7f;
    [SerializeField] AnimationCurve ghostOpacityCurve = Make((0, 0f), (0.025f, 1f), (0.4f, 0.8f), (0.75f, 0f), (1f, 0f));
    [Tooltip("Radial blur: fraction of the distance to the zoom centre that gets smeared.")]
    [SerializeField] float maxGhostBlur = 0.25f;
    [SerializeField] AnimationCurve ghostBlurCurve    = Make((0, 0f), (0.025f, 1f), (0.7f, 0.5f), (0.75f, 0f), (1f, 0f));
    [Tooltip("Also scale the blur by how far apart the ghost and main zoom are, so it vanishes as they converge.")]
    [SerializeField] bool blurFollowsZoomGap = true;

    [Header("Flash")]
    [SerializeField] [ColorUsage(false, true)] Color flashColor = new Color(1f, 0.93f, 0.65f);
    [Range(0f, 1f)] [SerializeField] float maxFlash = 1f;
    [SerializeField] AnimationCurve flashCurve = Make((0, 0f), (0.02f, 1f), (0.12f, 0.1f), (0.2f, 0f), (1f, 0f));

    [Header("Yellow tint")]
    [SerializeField] [ColorUsage(false, true)] Color tintColor = new Color(1f, 0.8f, 0.25f);
    [Range(0f, 1f)] [SerializeField] float tintDesaturate = 0.5f;
    [Range(0f, 1f)] [SerializeField] float maxTint = 1f;
    [SerializeField] AnimationCurve tintCurve = Make((0, 0f), (0.03f, 1f), (0.3f, 0.85f), (0.75f, 0f), (1f, 0f));

    [Header("Exposure dip (the dark phase after the flash)")]
    [Range(0f, 1f)] [SerializeField] float maxDarken = 0.5f;
    [SerializeField] AnimationCurve darkenCurve = Make((0, 0f), (0.1f, 0.3f), (0.22f, 1f), (0.6f, 0.3f), (0.85f, 0f), (1f, 0f));

    [Header("Sobel outlines")]
    [SerializeField] [ColorUsage(false, true)] Color edgeColor = new Color(1f, 0.85f, 0.1f);
    [Tooltip("Brightness multiplier of the outlines at curve = 1.")]
    [SerializeField] float maxEdgeStrength = 1.5f;
    [Tooltip("Minimum luminance gradient that counts as an edge. Lower = more edges.")]
    [SerializeField] float edgeThreshold = 0.15f;
    [Tooltip("Width of the threshold ramp. Larger = softer outlines.")]
    [SerializeField] float edgeSoftness = 0.1f;
    [Tooltip("Outline width in screen pixels.")]
    [SerializeField] float edgeWidth = 1.5f;
    [Tooltip("Make this outlast the other curves to get the lingering outlines.")]
    [SerializeField] AnimationCurve edgeCurve = Make((0, 0f), (0.04f, 1f), (0.5f, 0.7f), (0.9f, 0f), (1f, 0f));

    static readonly int ZoomId          = Shader.PropertyToID("_Zoom");
    static readonly int ZoomCenterId    = Shader.PropertyToID("_ZoomCenter");
    static readonly int GhostZoomId     = Shader.PropertyToID("_GhostZoom");
    static readonly int GhostOpacityId  = Shader.PropertyToID("_GhostOpacity");
    static readonly int GhostBlurId     = Shader.PropertyToID("_GhostBlur");
    static readonly int FlashId         = Shader.PropertyToID("_Flash");
    static readonly int FlashColorId    = Shader.PropertyToID("_FlashColor");
    static readonly int TintId          = Shader.PropertyToID("_Tint");
    static readonly int TintColorId     = Shader.PropertyToID("_TintColor");
    static readonly int TintDesatId     = Shader.PropertyToID("_TintDesaturate");
    static readonly int DarkenId        = Shader.PropertyToID("_Darken");
    static readonly int EdgeStrengthId  = Shader.PropertyToID("_EdgeStrength");
    static readonly int EdgeColorId     = Shader.PropertyToID("_EdgeColor");
    static readonly int EdgeThresholdId = Shader.PropertyToID("_EdgeThreshold");
    static readonly int EdgeSoftnessId  = Shader.PropertyToID("_EdgeSoftness");
    static readonly int EdgeWidthId     = Shader.PropertyToID("_EdgeWidth");

    float time;
    bool playing;

    void OnEnable()  => ResetMaterial();
    void OnDisable() { playing = false; ResetMaterial(); } // the material is a project asset: always reset it

    /// <summary>Starts (or restarts) the effect.</summary>
    [ContextMenu("Trigger")]
    public void Trigger()
    {
        time = 0f;
        playing = true;
        Apply(0f);
    }

    void Update()
    {
        if (!playing) return;

        time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = time / Mathf.Max(0.0001f, duration);
        if (t >= 1f)
        {
            playing = false;
            ResetMaterial();
            return;
        }
        Apply(t);
    }

    void Apply(float t)
    {
        if (material == null) return;

        float zoom      = 1f + Ev(zoomCurve, t) * maxZoom;
        float ghostZoom = 1f + Ev(ghostZoomCurve, t) * ghostMaxZoom;

        float blur = Ev(ghostBlurCurve, t) * maxGhostBlur;
        if (blurFollowsZoomGap)
        {
            float gap = Mathf.Clamp01((ghostZoom - zoom) / Mathf.Max(0.01f, ghostMaxZoom - maxZoom));
            blur *= gap;
        }

        material.SetFloat(ZoomId, zoom);
        material.SetVector(ZoomCenterId, new Vector4(zoomCenter.x, zoomCenter.y, 0f, 0f));

        material.SetFloat(GhostZoomId, ghostZoom);
        material.SetFloat(GhostOpacityId, Ev(ghostOpacityCurve, t) * maxGhostOpacity);
        material.SetFloat(GhostBlurId, blur);

        material.SetFloat(FlashId, Ev(flashCurve, t) * maxFlash);
        material.SetColor(FlashColorId, flashColor);

        material.SetFloat(TintId, Ev(tintCurve, t) * maxTint);
        material.SetColor(TintColorId, tintColor);
        material.SetFloat(TintDesatId, tintDesaturate);

        material.SetFloat(DarkenId, Ev(darkenCurve, t) * maxDarken);

        material.SetFloat(EdgeStrengthId, Ev(edgeCurve, t) * maxEdgeStrength);
        material.SetColor(EdgeColorId, edgeColor);
        material.SetFloat(EdgeThresholdId, edgeThreshold);
        material.SetFloat(EdgeSoftnessId, edgeSoftness);
        material.SetFloat(EdgeWidthId, edgeWidth);
    }

    void ResetMaterial()
    {
        if (material == null) return;
        material.SetFloat(ZoomId, 1f);
        material.SetFloat(GhostZoomId, 1f);
        material.SetFloat(GhostOpacityId, 0f);
        material.SetFloat(GhostBlurId, 0f);
        material.SetFloat(FlashId, 0f);
        material.SetFloat(TintId, 0f);
        material.SetFloat(DarkenId, 0f);
        material.SetFloat(EdgeStrengthId, 0f);
    }

    static float Ev(AnimationCurve c, float t) => Mathf.Clamp01(c.Evaluate(t));

    // Builds a smooth curve from (x, y) points; peaks and valleys get flat tangents so the
    // curve never overshoots past the values you typed in.
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
