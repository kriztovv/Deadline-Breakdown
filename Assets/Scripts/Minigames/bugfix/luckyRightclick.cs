using System.Collections;
using UnityEngine;

public class luckyRightclick : MonoBehaviour
{
    [SerializeField] private float radius = 3f;
    [SerializeField] private int menuOption = 0;       // the context menu slot that triggers it
    [SerializeField] private LayerMask bugLayers = ~0;

    [Header("Sounds")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField, Range(0f, 1f)] private float fireVolume = 1f;
    [SerializeField] private AudioClip hurtSound;
    [SerializeField, Range(0f, 1f)] private float hurtVolume = 1f;
    
    private ContactFilter2D _filter;
    private readonly Collider2D[] _hits = new Collider2D[32];
    private bool _fireCharged = true;
    private Animator animator;
    
    [SerializeField] private float eliminateDelay = 0.6f;   // seconds from the start of the fire animation to the hit
    private void Awake()
    {
        animator = GetComponent<Animator>();
        _filter = new ContactFilter2D();
        _filter.SetLayerMask(bugLayers);
        _filter.useTriggers = true;
    }

    private void Start()
    {
        if (TryGetComponent(out minigameCanRightClick rc)) rc.OptionSelected += OnOption;
    }

    private void OnOption(int index)
    {
        if (index == menuOption) EliminateNearby();
    }
    
    public void EliminateNearby()
    {
        if (!_fireCharged) return;
        animator.SetTrigger("fire");
        if (fireSound != null) SFXManager.instance.PlaySFX(fireSound, transform, fireVolume);
        _fireCharged = false;

        StartCoroutine(EliminateAfterDelay());
        StartCoroutine(FireDelay());
    }

    private IEnumerator EliminateAfterDelay()
    {
        if (eliminateDelay > 0f) yield return new WaitForSeconds(eliminateDelay);

        int count = gameObject.scene.GetPhysicsScene2D()
            .OverlapCircle(transform.position, radius, _filter, _hits);

        for (int i = 0; i < count; i++)
        {
            bugRightclick bug = _hits[i].GetComponentInParent<bugRightclick>();
            if (bug != null) bug.Delete();
        }
    }

    public void Hurt()
    {
        animator.SetTrigger("hurt");
        if (hurtSound != null) SFXManager.instance.PlaySFX(hurtSound, transform, hurtVolume);
    }
    
    public void OnFireAnimationEnd()
    {
        var particleSystem = GetComponentInChildren<ParticleSystem>();
        if (particleSystem != null)
        {
            particleSystem.Play();
        }
    }

    private void OnDrawGizmosSelected()   // shows the radius in the Scene view when the object is selected
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }

    private IEnumerator FireDelay()
    {
        yield return new WaitForSeconds(5f);
        _fireCharged = true;
    }
}