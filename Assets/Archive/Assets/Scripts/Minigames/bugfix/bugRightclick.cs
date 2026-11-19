using System.Collections;
using UnityEngine;

public class bugRightclick : MonoBehaviour
{
    [SerializeField] private float launchForce = 10f;
    [SerializeField] private float sideSpread = 0.4f;   // 0 = straight up, 1 = up to 45Â° to either side
    [SerializeField] private float spinSpeed = 360f;
    
    [Header("Sounds")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField, Range(0f, 1f)] private float deathVolume = 1f;
    
    public static event System.Action<bugRightclick> Eliminated;
    private Coroutine _rotationReset;
    private Rigidbody2D _rb;
    private Animator animator;

    private bool _dead;

    public void Delete()
    {
        if (_dead) return;
        _dead = true;

        if (TryGetComponent(out bugChaseLucky ai)) ai.StopSounds();
        if (deathSound != null) SFXManager.instance.PlaySFX(deathSound, transform, deathVolume);

        //Animation
        Eliminated?.Invoke(this);
        animator.SetTrigger("evaporate");
    }

    public void OnEvaporateAnimationEnd()
    {
        Destroy(gameObject);
    }
    
    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        GetComponent<minigameCanRightClick>().OptionSelected += OnOption;
    }

    void OnOption(int index)
    {
        switch (index)
        {
            case 0:
                Delete();
                break;
            case 2:
                Launch();
                break;
        }
    }

    public void Launch()
    {
        if (_rb == null) return;
        if (_rotationReset != null) StopCoroutine(_rotationReset);
        if (TryGetComponent(out bugChaseLucky ai)) ai.Stun();
        
        _rb.freezeRotation =  false;
        // mostly up, with a random sideways lean
        Vector2 direction = new Vector2(Random.Range(-sideSpread, sideSpread), 1f).normalized;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = Random.Range(-spinSpeed, spinSpeed);   // degrees per second, random direction
        _rb.AddForce(direction * launchForce * _rb.mass, ForceMode2D.Impulse);
    }
    
    private IEnumerator RotationReset()
    {
        yield return new WaitForSeconds(2);
        _rb.freezeRotation = true;
        _rb.rotation = 0;
    }
}