using UnityEngine;

public class mainMenu : minigameController
{
    [SerializeField] private float recoverTime = 1f;
    [SerializeField] private GameObject luckyObj;
    private bool _wasHeld;
    private float _pausedUntil;
    private Rigidbody2D _rb;

    protected override void MgStart()
    {
       _rb = luckyObj.GetComponent<Rigidbody2D>();
    }

    protected override void MgFixedUpdate()
    {
        bool held = luckyObj.TryGetComponent<TargetJoint2D>(out _);
        if (!held && _wasHeld) _pausedUntil = Time.time + recoverTime;   // let go (thrown): wait before moving again
        _wasHeld = held;
        if (held || Time.time < _pausedUntil || held == _wasHeld) return;
        _rb.rotation = 0;
        _rb.freezeRotation = true;
    }
}