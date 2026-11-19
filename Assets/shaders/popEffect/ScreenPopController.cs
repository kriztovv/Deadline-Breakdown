using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the ScreenPop material over time using editable curves.
/// Call Trigger() from anywhere (UnityEvent, other script, etc.).
/// </summary>
public class ScreenPopController : MonoBehaviour
{
    [Header("Material used by the Full Screen Pass Renderer Feature")]
    [SerializeField] Material material;

    [Header("Timing")]
    [SerializeField] float duration = 0.6f;
    [SerializeField] bool unscaledTime = true; // keeps working during hit-stop / slow-mo

    [Header("Max values (curve value 1 = this)")]
    [SerializeField] float maxPixelSize = 24f;
    [SerializeField] float maxSaturation = 1.0f;
    [SerializeField] float maxFisheye = 0.5f;

    [Header("Curves (x = normalized time 0..1, y = strength 0..1)")]
    // Ease in, hold, ease out. Edit freely in the inspector.
    [SerializeField] AnimationCurve pixelCurve      = AnimationCurve.EaseInOut(0, 0, 1, 0).WithPeak(0.4f);
    [SerializeField] AnimationCurve saturationCurve = AnimationCurve.EaseInOut(0, 0, 1, 0).WithPeak(0.4f);
    [SerializeField] AnimationCurve fisheyeCurve    = AnimationCurve.EaseInOut(0, 0, 1, 0).WithPeak(0.4f);

    static readonly int PixelSizeId  = Shader.PropertyToID("_PixelSize");
    static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    static readonly int FisheyeId    = Shader.PropertyToID("_Fisheye");

    Coroutine running;

    void OnEnable()  => Apply(0, 0, 0);
    void OnDisable() => Apply(0, 0, 0); // important: material is a project asset, reset it

    [ContextMenu("Trigger")]
    public void Trigger()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += (unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) / Mathf.Max(0.0001f, duration);
            float n = Mathf.Clamp01(t);
            Apply(pixelCurve.Evaluate(n), saturationCurve.Evaluate(n), fisheyeCurve.Evaluate(n));
            yield return null;
        }
        Apply(0, 0, 0);
        running = null;
    }

    void Apply(float pixel, float sat, float fish)
    {
        if (material == null) return;
        material.SetFloat(PixelSizeId, Mathf.Lerp(1f, maxPixelSize, pixel));
        material.SetFloat(SaturationId, sat * maxSaturation);
        material.SetFloat(FisheyeId, fish * maxFisheye);
    }
}

static class CurveExt
{
    // Builds a smooth 0 -> 1 -> 0 curve with the peak at the given normalized time.
    public static AnimationCurve WithPeak(this AnimationCurve _, float peakTime)
    {
        return new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(peakTime, 1f, 0f, 0f),
            new Keyframe(1f, 0f, 0f, 0f));
    }
}
