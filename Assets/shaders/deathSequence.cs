using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DeathSequence : MonoBehaviour
{
    public EnergyEffectController energyEffect;
    public PixelSortController pixelSort;
    public float delayBeforePixelSort = 0.5f;

    [Tooltip("How long the energy takes to drop to 0, in seconds.")]
    public float energyDropDuration = 1.5f;

    [Tooltip("X = normalized time (0-1), Y = progress (0 = start energy, 1 = zero energy).")]
    public AnimationCurve energyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [SerializeField] private GameObject deathHud;
    [SerializeField] private AudioSource music;

    public bool deathFinished;
    
    public static event System.Action Died;
    public static event System.Action Respawned;

    public void TriggerDeath()
    {
        if (energyEffect == null || pixelSort == null)
            return;
        Died?.Invoke();

        StartCoroutine(DeathRoutine());
        StartCoroutine(DeathHudCouroutine());
    }
    
    private void Update()
    {
        if (!deathFinished || Keyboard.current == null) return;
        if (Keyboard.current.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(gameObject.scene.name, LoadSceneMode.Single);
    }
    
    private IEnumerator FadeOutMusic()
    {
        while (music.volume > 0)
        {
            music.volume -= 0.001f;
            yield return null;
        }
        music.volume = 0;
        music.Stop();
    }

    IEnumerator DeathRoutine()
    {
        pixelSort.TriggerAfterDelay(delayBeforePixelSort);
        StartCoroutine(FadeOutMusic());

        float startEnergy = energyEffect.energy;
        float elapsed = 0f;
        

        while (elapsed < energyDropDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / energyDropDuration);

            // Curve output: 0 = full start energy, 1 = fully drained
            float progress = energyCurve.Evaluate(t);
            energyEffect.SetEnergy(Mathf.LerpUnclamped(startEnergy, 0f, progress));
            
            
            
            yield return null;
        }

        //energyEffect.SetEnergy(0f);
    }
    
    IEnumerator DeathHudCouroutine()
    {
        deathFinished = false;
        yield return new WaitForSecondsRealtime(8);
        deathHud.SetActive(true);
        deathFinished = true;
    }

    public void resetEnergy()
    {
        deathFinished = false;
        

        if (energyEffect == null)
            return;

        energyEffect.SetEnergy(100f);
    }
}