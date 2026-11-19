using System.Collections.Generic;
using UnityEngine;

public class luckyBriefcase : MonoBehaviour
{
    [SerializeField] private minigameController controller;   // optional: found automatically in this scene if left empty

    [Header("Right click held")]
    [SerializeField] private Collider2D triggerCollider;      // optional: the trigger collider on this object is found automatically

    [Header("Sounds")]
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private AudioClip openSound;
    [SerializeField, Range(0f, 1f)] private float caseVolume = 0.8f;
    [SerializeField] private AudioClip suckLoopSound;
    [SerializeField, Range(0f, 1f)] private float suckVolume = 0.5f;
    [SerializeField] private float suckFadeTime = 0.25f;   // seconds to fade in or out

    private AudioSource _suckSource;
    
    [Header("Left click held (and right not)")]
    [SerializeField] private string pickupTag = "pickup";
    [SerializeField] private float pullRadius = 3f;
    [SerializeField] private float pullSpeed = 4f;            // horizontal speed in units per second
    [SerializeField] private LayerMask pickupLayers = ~0;

    private Animator _animator;
    private ContactFilter2D _filter;
    private readonly Collider2D[] _hits = new Collider2D[32];

    // Which bodies were pulled in the last physics step, to stop them when the pull ends
    private HashSet<Rigidbody2D> _pulledBefore = new HashSet<Rigidbody2D>();
    private HashSet<Rigidbody2D> _pulledNow = new HashSet<Rigidbody2D>();

    private bool _wasRightHeld;
    private bool _wasPulling;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (controller == null) controller = FindController();
        if (controller == null) Debug.LogWarning("luckyBriefcase: no minigameController found in this scene", this);

        if (triggerCollider == null)
        {
            foreach (Collider2D c in GetComponents<Collider2D>())
            {
                if (c.isTrigger) { triggerCollider = c; break; }
            }
        }
        if (triggerCollider == null) Debug.LogWarning("luckyBriefcase: no trigger collider found on this object", this);

        _filter = new ContactFilter2D();
        _filter.SetLayerMask(pickupLayers);
        _filter.useTriggers = true;   // pickups may be triggers too
        
        if (suckLoopSound != null)
        {
            _suckSource = gameObject.AddComponent<AudioSource>();
            _suckSource.clip = suckLoopSound;
            _suckSource.loop = true;
            _suckSource.playOnAwake = false;
            _suckSource.spatialBlend = 0f;   // 2D
            _suckSource.volume = 0f;
        }
    }
    
    private void UpdateSuckSound(bool pulling)
    {
        if (_suckSource == null) return;

        float target = pulling ? suckVolume : 0f;
        if (target > 0f && !_suckSource.isPlaying) _suckSource.Play();

        float step = suckFadeTime > 0f ? suckVolume / suckFadeTime * Time.unscaledDeltaTime : suckVolume;
        _suckSource.volume = Mathf.MoveTowards(_suckSource.volume, target, step);

        if (target == 0f && _suckSource.volume <= 0f && _suckSource.isPlaying) _suckSource.Stop();
    }

    // Looks through this object's own scene
    private minigameController FindController()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<minigameController>(true);
            if (found != null) return found;
        }
        return null;
    }

    private void Update()
    {
        if (controller == null) return;

        // Right button held: the trigger collider is off
        bool rightHeld = controller.RightHeld;
        if (triggerCollider != null && triggerCollider.enabled == rightHeld) triggerCollider.enabled = !rightHeld;
        
        if (rightHeld != _wasRightHeld)
        {
            _wasRightHeld = rightHeld;
            _animator.SetTrigger(rightHeld ? "caseClosing" : "caseOpening");

            AudioClip clip = rightHeld ? closeSound : openSound;
            if (clip != null) SFXManager.instance.PlaySFX(clip, transform, caseVolume);
        }

        UpdateSuckSound(controller.LeftHeld && !rightHeld);
    }

    private void FixedUpdate()
    {
        if (controller == null) return;

        bool pulling = controller.LeftHeld && !controller.RightHeld;

        _pulledNow.Clear();
        if (pulling) Pull();

        // Bodies that were pulled last step but not this one stop moving sideways
        foreach (Rigidbody2D rb in _pulledBefore)
        {
            if (rb != null && !_pulledNow.Contains(rb)) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);   // 'velocity' before Unity 6
        }
        (_pulledBefore, _pulledNow) = (_pulledNow, _pulledBefore);

        if (pulling != _wasPulling)
        {
            _wasPulling = pulling;
            // ANIMATION: pull effect on / off (glow, magnet lines, sound loop)
        }
    }

    private void Pull()
    {
        int count = gameObject.scene.GetPhysicsScene2D().OverlapCircle(transform.position, pullRadius, _filter, _hits);
        
        for (int i = 0; i < count; i++)
        {
            Collider2D col = _hits[i];
            Rigidbody2D rb = col.attachedRigidbody;

            // the tag can be on the collider's object or on the body's object
            if (!col.CompareTag(pickupTag) && (rb == null || !rb.CompareTag(pickupTag))) continue;

            if (rb != null)
            {
                if (!_pulledNow.Add(rb)) continue;   // several colliders on one body

                // Horizontal speed towards the briefcase, capped so it never overshoots; the fall stays untouched
                float dx = transform.position.x - rb.position.x;
                float vx = Mathf.Clamp(dx / Time.fixedDeltaTime, -pullSpeed, pullSpeed);
                rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            }
            else
            {
                // no body (moved by a script): shift the transform instead
                Transform t = col.transform;
                Vector3 p = t.position;
                p.x = Mathf.MoveTowards(p.x, transform.position.x, pullSpeed * Time.fixedDeltaTime);
                t.position = p;
            }
        }
    }

    private void OnDisable()
    {
        if (triggerCollider != null) triggerCollider.enabled = true;   // never leave it switched off
        if (_suckSource != null) _suckSource.Stop();
    }

    private void OnDrawGizmosSelected()   // shows the pull radius in the Scene view
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, pullRadius);
    }
}