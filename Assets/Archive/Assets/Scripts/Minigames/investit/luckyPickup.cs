using System.Collections.Generic;
using UnityEngine;

public class luckyPickup : MonoBehaviour
{
    [SerializeField] private Collider2D collectCollider;   // optional: the trigger collider that collects pickups (found automatically)
    [SerializeField] private AudioClip pickupSound;

    [Header("Sounds")]
    [SerializeField] private AudioClip badSound;
    [SerializeField, Range(0f, 1f)] private float badVolume = 0.4f;
    [SerializeField] private AudioClip moneySound;
    [SerializeField, Range(0f, 1f)] private float moneyVolume = 0.6f;
    [SerializeField] private AudioClip goldSound;
    [SerializeField, Range(0f, 1f)] private float goldVolume = 0.8f;
    
    private investit _controllerScript;
    private readonly HashSet<GameObject> _collected = new HashSet<GameObject>();
    
    private static string CurrentSubject()
    {
        var ci = ClassroomInput.Instance;
        return ci != null && ci.InMinigame ? ci.OpenSubject : "Finance";   // "Finance" is only a fallback for testing the scene on its own
    }
    
    private void Play(AudioClip clip, float volume)
    {
        if (clip != null) SFXManager.instance.PlaySFX(clip, transform, volume);
    }

    private void Start()
    {
        _controllerScript = GameObject.Find("minigameController").GetComponent<investit>();

        if (collectCollider == null)
        {
            foreach (Collider2D c in GetComponents<Collider2D>())
            {
                if (c.isTrigger) { collectCollider = c; break; }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision) => TryCollect(collision);
    private void OnTriggerStay2D(Collider2D collision) => TryCollect(collision);

    private void TryCollect(Collider2D pickup)
    {
        if (!pickup.CompareTag("pickup")) return;

        // Only the trigger collider collects, and only while it is switched on
        if (collectCollider != null)
        {
            if (!collectCollider.enabled) return;
            if (!collectCollider.Distance(pickup).isOverlapped) return;   // a hit on the solid collider doesn't count
        }

        // Each pickup is counted once, even if several events arrive before it's destroyed
        _collected.RemoveWhere(g => g == null);
        if (!_collected.Add(pickup.gameObject)) return;
        switch (pickup.name)
        {
            case "bad(Clone)":
                _controllerScript.collectedMoney -= 10;
                Play(badSound, badVolume);
                AssignmentManager.Instance.Worsen(CurrentSubject());
                break;
            case "money(Clone)":
                _controllerScript.collectedMoney++;
                GameStats.Instance.AddStat("Finance", "coins", 1);
                Play(moneySound, moneyVolume);
                break;
            case "gold(Clone)":
                _controllerScript.collectedMoney += 3;
                GameStats.Instance.AddStat("Finance", "gold", 1);
                Play(goldSound, goldVolume);
                break;
        }
        
        _controllerScript.UpdateCounter();
        Destroy(pickup.gameObject);
    }
}