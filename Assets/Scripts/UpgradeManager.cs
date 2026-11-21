using System;
using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [SerializeField] private List<Upgrade> pool = new List<Upgrade>();
    [SerializeField] private float interval = 120f;     // seconds from the start (and after each pick) until the next offer

    public event Action Changed;                        // the offer or the owned list changed

    public bool Running { get; private set; }
    public Upgrade[] Offer { get; private set; } = new Upgrade[0];
    public IReadOnlyList<Upgrade> Owned => owned;
    public float TimeLeft => timer;

    private readonly List<Upgrade> owned = new List<Upgrade>();
    private readonly Dictionary<string, float> adds = new Dictionary<string, float>();
    private readonly Dictionary<string, float> mults = new Dictionary<string, float>();
    private float timer;
    
    //xtra xtra lives
    [Header("Extra life")]
    [SerializeField] private AudioClip extraLifeSound;
    [SerializeField, Range(0f, 1f)] private float extraLifeVolume = 1f;
    
    public event Action ExtraLifeUsed;     // for UI or effects
    private int livesUsed;
    
    public int ExtraLivesLeft => Mathf.Max(0, Mathf.RoundToInt(Stat("extraLives", 0f)) - livesUsed);

    private void Awake() => Instance = this;

    public void Begin()
    {
        if (Running) return;
        Running = true;
        timer = interval;
    }

    private void Update()
    {
        if (!Running || AssignmentManager.Dead || Offer.Length > 0) return;   // the timer only runs while nothing is waiting to be picked

        timer -= Time.unscaledDeltaTime;
        if (timer <= 0f) MakeOffer();
    }

    private void MakeOffer()
    {
        var candidates = new List<Upgrade>();
        foreach (Upgrade u in pool) if (u != null && !owned.Contains(u)) candidates.Add(u);

        if (candidates.Count == 0) { timer = interval; return; }   // everything is owned

        int n = Mathf.Min(2, candidates.Count);
        Offer = new Upgrade[n];
        for (int i = 0; i < n; i++)
        {
            int k = UnityEngine.Random.Range(0, candidates.Count);
            Offer[i] = candidates[k];
            candidates.RemoveAt(k);
        }
        Changed?.Invoke();
    }

    public void Pick(int index)
    {
        if (index < 0 || index >= Offer.Length) return;

        Upgrade u = Offer[index];
        Offer = new Upgrade[0];       // the other one disappears
        owned.Add(u);
        u.OnAcquire(this);
        timer = interval;             // the timer restarts after the pick
        Changed?.Invoke();
    }

    // ---- stats ----
    public void ModifyStat(string key, float add, float mult)
    {
        adds[key] = (adds.TryGetValue(key, out float a) ? a : 0f) + add;
        mults[key] = (mults.TryGetValue(key, out float m) ? m : 1f) * mult;
        Changed?.Invoke();
    }

    public float Stat(string key, float baseValue)
    {
        float a = adds.TryGetValue(key, out float x) ? x : 0f;
        float m = mults.TryGetValue(key, out float y) ? y : 1f;
        return (baseValue + a) * m;
    }

    // safe to call from anywhere, even before the manager exists
    public static float Get(string key, float baseValue) =>
        Instance == null ? baseValue : Instance.Stat(key, baseValue);
    
    [System.Serializable]
    public class SceneObject
    {
        public string key;
        public GameObject target;
    }

    [SerializeField] private List<SceneObject> sceneObjects = new List<SceneObject>();

    public GameObject GetObject(string key)
    {
        foreach (SceneObject s in sceneObjects)
            if (s.key == key) return s.target;
        return null;
    }
    
    public bool TryUseExtraLife()
    {
        if (ExtraLivesLeft <= 0) return false;

        livesUsed++;
        if (extraLifeSound != null) SFXManager.instance.PlaySFX(extraLifeSound, transform, extraLifeVolume);
        ExtraLifeUsed?.Invoke();
        Changed?.Invoke();
        return true;
    }
}