using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PillEater : MonoBehaviour
{
    [SerializeField] private PickupController pickup;
    [SerializeField] private GameObject controller;
    [SerializeField] private AudioClip eatSound;
    [SerializeField] private AudioClip gulpSound;
    [SerializeField] private AudioClip startMusic;
    [SerializeField] private AudioClip loopMusic;
    [SerializeField] private AudioSource loopSource;
    
    private EnergyBoostEffect effect;
    private doseController doseController;
    private mainMenuCanLeftClick _mainMenuScript;
    private bool _start;

    private void Start()
    {
        effect = controller.GetComponent<EnergyBoostEffect>();
        doseController = controller.GetComponent<doseController>();

        foreach (AudioClip c in new[] { eatSound, gulpSound, startMusic, loopMusic })
            if (c != null && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();

        var play = GameObject.Find("play");   // once, at start, and no try/catch
        if (play != null) _mainMenuScript = play.GetComponent<mainMenuCanLeftClick>();
    }

    private static void Timed(string label, Action action)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        action();
        Debug.Log($"{label}: {sw.Elapsed.TotalMilliseconds:F1} ms");
    }
    
    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

        Rigidbody held = pickup.Held;
        if (held == null || !held.TryGetComponent(out Pill pill)) return;

        // >>> call your friend's pill effect here <<<
        Timed("effect",   () => effect.Trigger());
        Timed("dose",     () => doseController.AddDoser(pill.doser));

        pickup.ForgetHeld();
        GameStats.Instance.AddPill();
        Destroy(held.gameObject);
        
        if (_mainMenuScript == null)
            try
            {
                _mainMenuScript = GameObject.Find("play").GetComponent<mainMenuCanLeftClick>();
            } catch (NullReferenceException){}

        Timed("sfx",      () => SFXManager.instance.PlaySFX(eatSound, transform, 0.3f));
        Timed("sfx",      () => SFXManager.instance.PlaySFX(gulpSound, transform, 1));
        if (!_start)
        {
            Timed("tutorial", () => _mainMenuScript.TutAdvance());
            Timed("music",    () => { loopSource.Stop(); loopSource.clip = startMusic; loopSource.Play(); });
            loopSource.volume = 0.2f;
            _start = true;
        }
    }
}