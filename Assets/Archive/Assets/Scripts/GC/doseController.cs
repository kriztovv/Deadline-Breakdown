using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class doseController : MonoBehaviour
{
    //Mechanics
    public int doseMeter;
    public List<Doser> activeDosers;
    [SerializeField] private EnergyEffectController effect;
    private Queue<int> _energyQueue;

    private Coroutine _updateDoseCoroutine;
    private Coroutine _updateSecondary;
    
    //UI
    [SerializeField] private EnergyGraph graph;
    [SerializeField] private DeathSequence deathSequence;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private GameObject graphObj;
    [SerializeField] private GameObject heldText;
    [SerializeField] private GameObject tutText;
    [SerializeField] private GameObject workPrompt;
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip menuMusic;

    private void Start()
    {
        _energyQueue = new Queue<int>();
        _energyQueue.Enqueue(0);
        _energyQueue.Enqueue(0);
        _energyQueue.Enqueue(0);
        _updateDoseCoroutine = StartCoroutine(UpdateDose());
        
        audioSource.clip = menuMusic;
        audioSource.loop = true;
        audioSource.volume = 0.2f;
        audioSource.Play();
    }

    public void AddDoser(Doser doser)
    {
        Doser copy = Instantiate(doser);
        copy.SetQueue();
        activeDosers.Add(copy);
    }

    private IEnumerator UpdateDose(bool fromSecondary = false)
    {
        if (fromSecondary)
        {
            _updateDoseCoroutine = _updateSecondary;
            _updateSecondary = null;
        }
        
        while (doseMeter < 100)
        {
            doseMeter = 0;
            for (int i = activeDosers.Count - 1; i >= 0; i--)
            {
                Doser activeDoser = activeDosers[i];
                doseMeter += CountDose(activeDoser);
            }
            graph.PushSample(Mathf.Min(doseMeter, 100));
            effect.energy = _energyQueue.Dequeue();
            _energyQueue.Enqueue(Mathf.Min(doseMeter, 100));

            float normalized = Mathf.Clamp01(Mathf.Min(doseMeter, 100) / 100f);
            Time.timeScale = 1f - Mathf.Lerp(0f, 0.5f, normalized);
            
            yield return new WaitForSecondsRealtime(1);
        }
        
        for (int i = 0; i < 6; i++)
        {
            graph.PushSample(100);
            effect.energy = 100;
            yield return new WaitForSecondsRealtime(1);
        }

        if (UpgradeManager.Instance != null && UpgradeManager.Instance.TryUseExtraLife())
        {
            SaveFromOverdose();
            _updateSecondary = StartCoroutine(UpdateDose(true));
            StopCoroutine(_updateDoseCoroutine);
        }
        
        Fail();
    }
    
    private int CountDose(Doser doser)
    {
        if (doser.delay > 0)
        {
            doser.delay--;
            return 0;
        }

        float dose;

        if (doser.CurveQueue.Peek() >= 1 && doser.duration > 0)
        {
            doser.duration--;
            dose = doser.value * doser.CurveQueue.Peek();
        }
        else
        {
            dose = doser.value * doser.CurveQueue.Dequeue();
        }
        dose += Random.Range(-doser.uncertainty, doser.uncertainty);

        if (doser.CurveQueue.Count == 0)
        {
            activeDosers.Remove(doser);
            Destroy(doser);
        }

        return Mathf.RoundToInt(dose);
    }

    public void Fail()
    {
        Debug.Log("Game over");
        deathSequence.TriggerDeath();

        SFXManager.instance.PlaySFX(deathSound, transform, 1);
        
        crosshair.SetActive(false);
        tutText.SetActive(false);
        heldText.SetActive(false);
        graphObj.SetActive(false);
        workPrompt.SetActive(false);
    }
}