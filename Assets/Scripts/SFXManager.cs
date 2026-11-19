using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager instance;
    
    [SerializeField] private AudioSource SFXObject;
    [SerializeField] private AudioSource _loopSource;
    [SerializeField] private GameObject monitorObject;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    public void PlaySFX(AudioClip clip, Transform spawnTransform, float volume, bool fromMonitor = false)
    {
        
        // spawn in gameObject
        AudioSource audioSource = Instantiate(SFXObject, fromMonitor ? monitorObject.transform.position : spawnTransform.position , Quaternion.identity);
        
        //assign the audioClip
        audioSource.clip = clip;
        
        //assign volume
        audioSource.volume = volume;
        
        //play sound
        audioSource.Play();
        
        //get length of SFX
        float clipLength = clip.length;
        
        //destroy when done playing SFX
        Destroy(audioSource.gameObject, clipLength);
    }
    
    public void PlayLoopSFX(AudioClip clip, Transform spawnTransform, float volume)
    {
        if (clip == null)
        {
            return;
        }

        if (_loopSource != null)
        {
            Destroy(_loopSource.gameObject);
        }

        GameObject go = new GameObject("LoopSFX");
        go.transform.position = spawnTransform.position;
        _loopSource = go.AddComponent<AudioSource>();
        _loopSource.clip = clip;
        _loopSource.volume = volume;
        _loopSource.loop = true;
        _loopSource.Play();
    }
    
    public void StopLoopSFX()
    {
        if (_loopSource != null)
        {
            Destroy(_loopSource.gameObject);
            _loopSource = null;
        }
    }
}