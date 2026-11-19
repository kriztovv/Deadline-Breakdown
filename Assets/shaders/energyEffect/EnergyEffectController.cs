using UnityEngine;

/// <summary>
/// Maps a 0..100 "energy" value to saturation, fisheye, camera FOV, a red vignette
/// and a heartbeat pulse. Every mapping is an editable AnimationCurve where
/// X = energy (0..100). Baseline is 20 (no effect). Danger starts at 80.
/// Drive it with SetEnergy(value) from your gameplay code.
/// </summary>
public class EnergyEffectController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Material material;   // material used by the Full Screen Pass feature
    [SerializeField] Camera targetCamera; // camera whose FOV is driven (defaults to Camera.main)

    [Header("Input")]
    [Range(0f, 100f)] [SerializeField] public float energy = 20f;
    [Tooltip("Seconds to smooth energy changes, so visuals never jump.")]
    [SerializeField] float smoothTime = 0.25f;
    [SerializeField] bool unscaledTime = false;

    [Header("Maximum amounts (curve value of +1 / -1 = this)")]
    [SerializeField] float maxSaturation = 0.35f;
    [SerializeField] float maxFisheye = 0.08f;
    [SerializeField] float maxFovDelta = 12f;   // degrees added at curve = 1

    [Header("Curves: X = energy 0..100, Y = strength (-1..1, 0 at baseline)")]
    [SerializeField] AnimationCurve saturationCurve = Make((0, -1f), (20, 0f), (80, 1f), (100, 1f));
    [SerializeField] AnimationCurve fisheyeCurve    = Make((0, -1f), (20, 0f), (80, 1f), (100, 1f));
    [SerializeField] AnimationCurve fovCurve        = Make((0, -1f), (20, 0f), (80, 1f), (100, 1f));

    [Header("Danger: vignette")]
    [SerializeField] Color vignetteColor = new Color(0.8f, 0f, 0f, 1f);
    [Range(0f, 1f)] [SerializeField] float vignetteInnerRadius = 0.35f;
    [Tooltip("Y = 0..1. Default starts at 80.")]
    [SerializeField] AnimationCurve vignetteCurve = Make((0, 0f), (80, 0f), (100, 1f));

    [Header("Danger: heartbeat")]
    [Tooltip("Y = 0..1 pulse strength. Default starts at 80.")]
    [SerializeField] AnimationCurve pulseStrengthCurve = Make((0, 0f), (80, 0f), (100, 1f));
    [Tooltip("Y = beats per minute.")]
    [SerializeField] AnimationCurve pulseBpmCurve = Make((0, 60f), (80, 70f), (100, 150f));
    [SerializeField] float pulseFisheye = 0.12f;   // extra fisheye on each beat
    [SerializeField] float pulseVignette = 0.5f;   // extra vignette intensity on each beat
    [SerializeField] float pulseFovKick = 3f;      // extra FOV degrees on each beat

    [Header("Danger: scrolling grid (masked by a slightly larger vignette)")]
    [SerializeField] Color gridColor = new Color(1f, 0.25f, 0.25f, 0.6f);
    [Tooltip("Number of cells across the screen height.")]
    [SerializeField] float gridScale = 14f;
    [Tooltip("Line thickness in cell units. 0.02 = thin.")]
    [SerializeField] float gridThickness = 0.025f;
    [Tooltip("How much larger than the red vignette the grid mask is (reduces the inner radius).")]
    [Range(0f, 0.5f)] [SerializeField] float gridMaskExtraRadius = 0.15f;
    [Tooltip("Y = 0..1 grid strength. Default starts at 80.")]
    [SerializeField] AnimationCurve gridCurve = Make((0, 0f), (80, 0f), (100, 1f));
    [Tooltip("Y = 0..1, multiplies Grid Max Scroll Speed.")]
    [SerializeField] AnimationCurve gridSpeedCurve = Make((0, 0.3f), (80, 0.3f), (100, 1f));
    [Tooltip("Cells per second at curve = 1. X = sideways, Y = vertical.")]
    [SerializeField] Vector2 gridMaxScrollSpeed = new Vector2(0f, 1.5f);
    [SerializeField] float gridPulse = 0.3f;       // extra grid strength on each beat

    [Header("High energy: hue shift over time")]
    [Tooltip("Y = 0..1, multiplies Max Hue Speed. Default starts shifting around 65.")]
    [SerializeField] AnimationCurve hueSpeedCurve = Make((0, 0f), (65, 0f), (100, 1f));
    [Tooltip("Full colour-wheel turns per second at curve = 1.")]
    [SerializeField] float maxHueSpeed = 0.12f;
    [Tooltip("How fast (turns/second) the hue eases back to normal when energy drops.")]
    [SerializeField] float hueReturnSpeed = 0.5f;

    [Header("Danger: periodic pop (pixelate + saturation + fisheye)")]
    [Tooltip("Y = 0..1. Pops only happen while this is above 0, and it scales their strength. Default starts at 80.")]
    [SerializeField] AnimationCurve popStrengthCurve = Make((0, 0f), (80, 0f), (100, 1f));
    [Tooltip("Y = seconds between pops. Default: every 4 s at 80, every 1.5 s at 100.")]
    [SerializeField] AnimationCurve popIntervalCurve = Make((0, 4f), (80, 4f), (100, 1.5f));
    [Tooltip("Length of one pop in seconds. Keep it shorter than the interval.")]
    [SerializeField] float popDuration = 0.5f;
    [SerializeField] float popMaxPixelSize = 20f;
    [SerializeField] float popMaxSaturation = 0.8f;
    [SerializeField] float popMaxFisheye = 0.3f;
    [Tooltip("X = normalized pop time 0..1, Y = 0..1. Edit to shape the ease in / out.")]
    [SerializeField] AnimationCurve popPixelCurve      = Make((0, 0f), (0.4f, 1f), (1, 0f));
    [SerializeField] AnimationCurve popSaturationCurve = Make((0, 0f), (0.4f, 1f), (1, 0f));
    [SerializeField] AnimationCurve popFisheyeCurve    = Make((0, 0f), (0.4f, 1f), (1, 0f));

    static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    static readonly int FisheyeId    = Shader.PropertyToID("_Fisheye");
    static readonly int VigIntId     = Shader.PropertyToID("_VignetteIntensity");
    static readonly int VigInnerId   = Shader.PropertyToID("_VignetteInner");
    static readonly int VigColorId   = Shader.PropertyToID("_VignetteColor");
    static readonly int HueId        = Shader.PropertyToID("_HueShift");
    static readonly int PixelSizeId  = Shader.PropertyToID("_PixelSize");
    static readonly int GridColorId  = Shader.PropertyToID("_GridColor");
    static readonly int GridIntId    = Shader.PropertyToID("_GridIntensity");
    static readonly int GridInnerId  = Shader.PropertyToID("_GridInner");
    static readonly int GridScaleId  = Shader.PropertyToID("_GridScale");
    static readonly int GridThickId  = Shader.PropertyToID("_GridThickness");
    static readonly int GridOffsetId = Shader.PropertyToID("_GridOffset");

    float smoothedEnergy;
    float energyVelocity;
    float beatPhase;
    float baseFov;
    float hue;            // current hue offset in turns
    float popTimer;       // seconds until the next pop
    float popT = -1f;     // normalized time of the running pop, -1 = idle
    float popScale;       // strength captured when the pop started
    Vector2 gridOffset;   // accumulated scroll in cells

    public float Energy => energy;

    public void SetEnergy(float value) => energy = Mathf.Clamp(value, 0f, 100f);

    void OnEnable()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null) baseFov = targetCamera.fieldOfView;
        smoothedEnergy = energy;
        beatPhase = 0f;
    }

    void OnDisable()
    {
        // The material is a project asset: always put it (and the camera) back.
        if (material != null)
        {
            material.SetFloat(SaturationId, 0f);
            material.SetFloat(FisheyeId, 0f);
            material.SetFloat(VigIntId, 0f);
            material.SetFloat(GridIntId, 0f);
            material.SetFloat(HueId, 0f);
            material.SetFloat(PixelSizeId, 1f);
        }
        hue = 0f;
        popT = -1f;
        if (targetCamera != null) targetCamera.fieldOfView = baseFov;
    }

    void LateUpdate()
    {
        float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        smoothedEnergy = Mathf.SmoothDamp(smoothedEnergy, energy, ref energyVelocity, smoothTime,
                                          Mathf.Infinity, dt);
        float e = smoothedEnergy;

        // Heartbeat
        float pulseStrength = Mathf.Clamp01(pulseStrengthCurve.Evaluate(e));
        float bpm = Mathf.Max(1f, pulseBpmCurve.Evaluate(e));
        beatPhase = (beatPhase + dt * bpm / 60f) % 1f;
        float beat = HeartBeat(beatPhase) * pulseStrength; // 0..1

        // Continuous effects
        float sat  = saturationCurve.Evaluate(e) * maxSaturation;
        float fish = fisheyeCurve.Evaluate(e) * maxFisheye + beat * pulseFisheye;
        float vig  = Mathf.Clamp01(vignetteCurve.Evaluate(e) + beat * pulseVignette * (vignetteCurve.Evaluate(e) > 0f ? 1f : 0f));
        float fov  = fovCurve.Evaluate(e) * maxFovDelta + beat * pulseFovKick;

        // Periodic pop: only while the strength curve is above 0, repeating every "interval" seconds
        float popStrength = Mathf.Clamp01(popStrengthCurve.Evaluate(e));
        float popInterval = Mathf.Max(0.1f, popIntervalCurve.Evaluate(e));
        if (popStrength > 0.001f)
        {
            popTimer -= dt;
            if (popTimer <= 0f)
            {
                popT = 0f;
                popScale = popStrength;
                popTimer = popInterval;
            }
        }
        else
        {
            popTimer = popInterval; // first pop comes one interval after entering the danger zone
        }

        float popPixelSize = 1f, popSat = 0f, popFish = 0f;
        if (popT >= 0f)
        {
            popT += dt / Mathf.Max(0.0001f, popDuration);
            if (popT >= 1f)
            {
                popT = -1f;
            }
            else
            {
                popPixelSize = Mathf.Lerp(1f, popMaxPixelSize, popPixelCurve.Evaluate(popT) * popScale);
                popSat  = popSaturationCurve.Evaluate(popT) * popMaxSaturation * popScale;
                popFish = popFisheyeCurve.Evaluate(popT) * popMaxFisheye * popScale;
            }
        }

        // Grid: strength, scrolling (accumulated so speed changes never jump)
        float gridStrength = Mathf.Clamp01(gridCurve.Evaluate(e));
        if (gridStrength > 0f) gridStrength = Mathf.Clamp01(gridStrength + beat * gridPulse);
        gridOffset += gridMaxScrollSpeed * (gridSpeedCurve.Evaluate(e) * dt);
        gridOffset.x = Mathf.Repeat(gridOffset.x, 1000f);
        gridOffset.y = Mathf.Repeat(gridOffset.y, 1000f);

        // Hue: keeps rotating while the speed curve is above 0, eases home otherwise
        float hueSpeed = Mathf.Max(0f, hueSpeedCurve.Evaluate(e)) * maxHueSpeed;
        if (hueSpeed > 0.0005f)
        {
            hue = Mathf.Repeat(hue + hueSpeed * dt, 1f);
        }
        else
        {
            if (hue > 0.5f) hue -= 1f;                       // shortest way back to 0
            hue = Mathf.MoveTowards(hue, 0f, hueReturnSpeed * dt);
        }

        if (material != null)
        {
            material.SetFloat(HueId, hue);
            material.SetColor(GridColorId, gridColor);
            material.SetFloat(GridIntId, gridStrength);
            material.SetFloat(GridInnerId, Mathf.Max(0f, vignetteInnerRadius - gridMaskExtraRadius));
            material.SetFloat(GridScaleId, gridScale);
            material.SetFloat(GridThickId, gridThickness);
            material.SetVector(GridOffsetId, new Vector4(gridOffset.x, gridOffset.y, 0f, 0f));
            material.SetFloat(SaturationId, sat + popSat);
            material.SetFloat(FisheyeId, fish + popFish);
            material.SetFloat(PixelSizeId, popPixelSize);
            material.SetFloat(VigIntId, vig);
            material.SetFloat(VigInnerId, vignetteInnerRadius);
            material.SetColor(VigColorId, vignetteColor);
        }

        if (targetCamera != null) targetCamera.fieldOfView = baseFov + fov;
    }

    // "Lub-dub": two quick bumps per cycle. Returns 0..1.
    static float HeartBeat(float p)
    {
        float a = Mathf.Exp(-Mathf.Pow((p - 0.10f) / 0.05f, 2f));        // lub
        float b = 0.7f * Mathf.Exp(-Mathf.Pow((p - 0.28f) / 0.06f, 2f)); // dub
        return Mathf.Clamp01(a + b);
    }

    // Builds a smooth curve from (x, y) points.
    static AnimationCurve Make(params (float x, float y)[] pts)
    {
        var c = new AnimationCurve();
        foreach (var p in pts) c.AddKey(p.x, p.y);
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }
}