using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class mobliterator : minigameController
{
    [Header("Molecules")]
    [SerializeField] private GameObject[] moleculePrefabs;   // each spawn picks one at random
    [SerializeField] public int moleculeCount = 40;         // X: how many molecules are spawned
    [SerializeField] private float spawnRadius = 2f;         // they are scattered inside this circle around this object

    [Header("Shooting")]
    [SerializeField] private Transform shootPoint;           // where each new molecule to shoot appears
    [SerializeField] private Transform gravityCenter;        // the point the shot molecule falls toward after its first contact
    [SerializeField] private bool shootLeft = true;          // the side the molecules are shot toward
    [SerializeField] private float shotSpeed = 8f;
    [SerializeField] private float respawnDelay = 0.5f;      // pause before the next molecule to shoot appears

    [Header("Aiming")]
    [SerializeField] private float aimSpeed = 60f;           // degrees per second while holding W/S
    [SerializeField] private float maxAimAngle = 60f;        // how far up or down it can aim
    [SerializeField] private Transform aimIndicator;         // optional arrow, its sprite must point right (+X)

    [Header("Goal")]
    [SerializeField] private TMP_Text counterText;           // the 3D TextMeshPro object, shows popped/required
    private string assignmentName = "Chemistry";             // as AssignmentManager knows it (investit uses "Finance")

    [Header("Timer")]
    [SerializeField] private float secondsPerMolecule = 3f;  // total time = required pops * this
    [SerializeField] private Transform timerBar;             // scaled horizontally, runs out over the total time, its left edge stays in place
    [SerializeField] private float endDelay = 1.5f;          // real seconds between the end and closing the minigame
    public bool tutorial;

    [Header("Sounds")]
    [SerializeField] private AudioClip spawnSound;       // the molecule to shoot appears
    [SerializeField, Range(0f, 1f)] private float spawnVolume = 0.4f;
    [SerializeField] private AudioClip shootSound;       // the player fires it
    [SerializeField, Range(0f, 1f)] private float shootVolume = 0.8f;
    [SerializeField] private AudioClip hitSound;         // the shot hits something
    [SerializeField, Range(0f, 1f)] private float hitVolume = 0.8f;
    [SerializeField] private AudioClip settleSound;      // it settled without popping
    [SerializeField, Range(0f, 1f)] private float settleVolume = 0.6f;
    [SerializeField] private AudioClip popSound;         // any molecule pops
    [SerializeField, Range(0f, 1f)] private float popVolume = 0.6f;
    [SerializeField] private AudioClip timeoutSound;     // the shot flew away and was removed
    [SerializeField, Range(0f, 1f)] private float timeoutVolume = 0.6f;
    
    [SerializeField] private float popPitchStep = 0.05f;   // pitch added for each pop in a row
    [SerializeField] private float maxPopPitch = 2f;       // the pitch never goes above this
    [SerializeField] private float comboWindow = 1f;       // seconds without a pop before the pitch resets (world time)

    private int _popCombo;
    private float _lastPopTime = -999f;
    
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private molecule _shot;        // the molecule that is waiting or flying
    private Coroutine _respawn;
    private float _aimAngle;       // degrees above (+) or below (-) the horizontal

    private int _popped, _required, _maxRequired;
    private float _gameTime, _elapsed;
    private bool _running, _ended;

    private bool _barReady;
    private Vector3 _barFullScale;
    private float _barLeftEdge;
    private float _barSpriteMinX;

    protected override void MgOnEnable()  { molecule.AnyPopped += OnMoleculePopped; }
    protected override void MgOnDisable() { molecule.AnyPopped -= OnMoleculePopped; }

    public void StartGame()
    {
        if (moleculePrefabs == null || moleculePrefabs.Length == 0)
        {
            Debug.LogWarning("mobliterator: no molecule prefabs assigned", this);
            return;
        }

        // a second call starts a fresh round instead of piling up molecules
        if (_respawn != null) { StopCoroutine(_respawn); _respawn = null; }
        if (_shot != null)
        {
            _shot.ShotFinished -= OnShotFinished;
            _shot.ShotEventRaised -= OnShotEvent;
            Destroy(_shot.gameObject);
            _shot = null;
        }
        foreach (GameObject old in _spawned) if (old != null) Destroy(old);
        _spawned.Clear();

        for (int i = 0; i < moleculeCount; i++)
        {
            GameObject prefab = moleculePrefabs[Random.Range(0, moleculePrefabs.Length)];
            if (prefab == null) continue;

            Vector3 pos = transform.position + (Vector3)(Random.insideUnitCircle * spawnRadius);

            // as a child of this object, so it ends up in THIS scene (Instantiate alone uses the active scene)
            _spawned.Add(Instantiate(prefab, pos, prefab.transform.rotation, transform));
        }

        // goal and timer
        _popped = 0;
        _required = moleculeCount / 3;
        _maxRequired = moleculeCount / 2;
        Debug.Log(_required + "/" + _maxRequired);
        _gameTime = tutorial ? 0 : _required * secondsPerMolecule;
        _elapsed = 0f;
        _ended = false;
        _running = true;
        InitBar();
        UpdateBar();
        UpdateCounter();

        _aimAngle = 0f;
        SpawnShot();
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip != null) SFXManager.instance.PlaySFX(clip, transform, volume);
    }
    
    // SFXManager has no pitch, so this plays through its own short-lived AudioSource
    private void PlayPitched(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;

        GameObject go = new GameObject("popSFX");
        go.transform.SetParent(transform, false);   // child of this object, so it stays in this scene

        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.spatialBlend = 0f;   // 2D
        src.playOnAwake = false;
        src.Play();

        Destroy(go, clip.length / pitch + 0.1f);   // a higher pitch plays the clip faster
    }

    private void OnShotEvent(molecule m, molecule.ShotEvent e)
    {
        switch (e)
        {
            case molecule.ShotEvent.Launched: Play(shootSound, shootVolume); break;
            case molecule.ShotEvent.Hit:      Play(hitSound, hitVolume); break;
            case molecule.ShotEvent.Settled:  Play(settleSound, settleVolume); break;
            case molecule.ShotEvent.TimedOut: Play(timeoutSound, timeoutVolume); break;
        }
    }
    
    private void SpawnShot()
    {
        _respawn = null;
        if (_ended) return;

        GameObject prefab = moleculePrefabs[Random.Range(0, moleculePrefabs.Length)];
        if (prefab == null || shootPoint == null)
        {
            Debug.LogWarning("mobliterator: missing prefab or shoot point, no molecule to shoot", this);
            return;
        }

        GameObject go = Instantiate(prefab, shootPoint.position, prefab.transform.rotation, transform);
        if (!go.TryGetComponent(out _shot))
        {
            Debug.LogWarning("mobliterator: the prefab has no molecule script", this);
            Destroy(go);
            return;
        }

        _shot.ShotFinished += OnShotFinished;
        _shot.ShotEventRaised += OnShotEvent;
        _shot.PrepareAsShot(gravityCenter);
        Play(spawnSound, spawnVolume);
        // ANIMATION: the new molecule appears at the shoot point (scale or fade in)
    }

    private void OnShotFinished(molecule m)
    {
        m.ShotFinished -= OnShotFinished;
        m.ShotEventRaised -= OnShotEvent;
        if (_shot == m) _shot = null;

        // a settled shot stays in the mass, so the next round clears it too (a popped one is already being destroyed)
        _spawned.Add(m.gameObject);
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);
        SpawnShot();
    }

    // Counting
    private void OnMoleculePopped(molecule m)
    {
        // the pitch rises with every pop in a row and resets after a pause
        if (Time.time - _lastPopTime > comboWindow) _popCombo = 0;
        _lastPopTime = Time.time;
        float pitch = Mathf.Min(maxPopPitch, 1f + popPitchStep * _popCombo);
        _popCombo++;
        PlayPitched(popSound, popVolume, pitch);
        _popped++;
        UpdateCounter();
        StartCoroutine(CheckAllPopped());
    }

    // Nothing left in the mass (the molecule waiting or flying to be shot doesn't count)
    private IEnumerator CheckAllPopped()
    {
        yield return null;   // the molecule that just popped is destroyed at the end of this frame
        if (!_running) yield break;

        foreach (molecule m in GetComponentsInChildren<molecule>())
        {
            if (m.InMass) yield break;   // something is still in the mass, or about to pop
        }

        StartCoroutine(EndGame(true));
    }

    private void UpdateCounter()
    {
        counterText.text = _popped.ToString();
        if (_popped < _required)
        {
            counterText.color = Color.red;
        } else if (_popped >= _required && _popped < _maxRequired)
        {
            counterText.color = Color.yellow;
        }
        else if (_popped >= _maxRequired)
        {
            counterText.color = Color.green;
        }
    }

    // End
    private IEnumerator EndGame(bool poppedEnough)
    {
        if (!_ended)
        {
            _ended = true;
            _running = false;
            if (_respawn != null) { StopCoroutine(_respawn); _respawn = null; }
            yield return new WaitForSecondsRealtime(endDelay);
            Debug.Log($"mobliterator: popped {_popped}/{_required}, all popped: {poppedEnough}");
            if (_popped < _required) Grade(false);
            else if (_popped <= _maxRequired) Grade(true);
            // else: the required number was reached but not everything popped, the grade stays
            ClassroomInput.Instance.CloseMinigame(true);
        }

        yield return null;
    }

    private void Grade(bool improve)
    {
        if (string.IsNullOrEmpty(assignmentName))
        {
            Debug.LogWarning("mobliterator: assignment name is empty, the grade was not changed", this);
            return;
        }

        if (improve) AssignmentManager.Instance.Improve(assignmentName);
        else AssignmentManager.Instance.Worsen(assignmentName);
    }

    // The aimed direction: W/S tilt it up or down from the horizontal
    private Vector2 AimDirection()
    {
        float rad = _aimAngle * Mathf.Deg2Rad;
        return new Vector2((shootLeft ? -1f : 1f) * Mathf.Cos(rad), Mathf.Sin(rad));
    }

    protected override void MgUpdate()
    {
        // Timer
        if (_running && !tutorial)
        {
            _elapsed += Time.deltaTime;   // the world clock, follows timeScale like the other timers
            UpdateBar();
            if (_elapsed >= _gameTime) StartCoroutine(EndGame(false));
        }

        // Aim
        bool aiming = _running && _shot != null && _shot.IsWaiting;

        if (aiming)
        {
            // MovementDeltaTime: the aiming follows the "movement ignores time scale" toggle
            _aimAngle += MovementInput.y * aimSpeed * MovementDeltaTime;
            _aimAngle = Mathf.Clamp(_aimAngle, -maxAimAngle, maxAimAngle);
        }

        if (aimIndicator != null)
        {
            aimIndicator.gameObject.SetActive(aiming);   // hidden once it is fired
            if (aiming)
            {
                Vector2 dir = AimDirection();
                aimIndicator.position = shootPoint.position;
                aimIndicator.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                // ANIMATION: the arrow or trajectory line can pulse or stretch here
            }
        }

        if (_running && _popped >= _maxRequired)
        {
            StartCoroutine(EndGame(true));
        }
    }

    // Fire
    protected override void OnLeftDown()
    {
        if (!_running || _shot == null) return;
        _shot.Launch(AimDirection(), shotSpeed);   // ignored if it was already fired
        if (_respawn != null) return;
        _respawn = StartCoroutine(RespawnRoutine());
    }

    // Timer bar (same as investit)
    private void InitBar()
    {
        if (timerBar == null || _barReady) return;
        _barReady = true;

        _barFullScale = timerBar.localScale;

        // the bar shrinks towards its left edge, whatever the sprite's pivot is
        var sr = timerBar.GetComponent<SpriteRenderer>();
        _barSpriteMinX = (sr != null && sr.sprite != null) ? sr.sprite.bounds.min.x : 0f;
        _barLeftEdge = timerBar.localPosition.x + _barSpriteMinX * _barFullScale.x;
    }

    private void UpdateBar()
    {
        if (timerBar == null || !_barReady) return;

        float fraction = _gameTime > 0f ? Mathf.Clamp01(1f - _elapsed / _gameTime) : 0f;

        Vector3 scale = _barFullScale;
        scale.x = _barFullScale.x * fraction;
        timerBar.localScale = scale;

        Vector3 pos = timerBar.localPosition;
        pos.x = _barLeftEdge - _barSpriteMinX * scale.x;
        timerBar.localPosition = pos;
    }

    private void OnDrawGizmosSelected()   // shows the spawn area, the shoot point and the current aim in the Scene view
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        if (shootPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(shootPoint.position, 0.3f);
            Gizmos.DrawRay(shootPoint.position, (Vector3)AimDirection() * 2f);
        }
    }
}