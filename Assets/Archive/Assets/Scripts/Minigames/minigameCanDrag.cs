using System.Collections;
using UnityEngine;

public class minigameCanDrag : MonoBehaviour
{
    private Rigidbody2D _rb;         // the object's own body, if it has one
    private Rigidbody2D _tempBody;   // a body added just for the drag, for objects that have none
    private Coroutine _rotationReset;
    private Coroutine _removeBody;

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    public void StartDrag()
    {
        // grabbed again before the temporary body was removed
        if (_removeBody != null) { StopCoroutine(_removeBody); _removeBody = null; }

        if (_rb != null)
        {
            // the object's own body: same behavior as before
            if (_rotationReset != null) StopCoroutine(_rotationReset);
            _rb.linearVelocity = Vector2.zero;
            _rb.freezeRotation = false;
            return;
        }

        if (_tempBody == null)
        {
            // No body of its own: add one before the joint does (the joint would add a default one with gravity)
            _tempBody = gameObject.AddComponent<Rigidbody2D>();
            _tempBody.gravityScale = 0f;
            _tempBody.freezeRotation = true;
            _tempBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;   // a fast drag won't pass through walls
        }
    }

    public void StopDrag()
    {
        if (_rb != null)
        {
            _rotationReset = StartCoroutine(RotationReset());
            return;
        }

        if (_tempBody != null) _removeBody = StartCoroutine(RemoveTempBody());
    }

    private IEnumerator RotationReset()
    {
        yield return new WaitForSeconds(2);
        _rb.freezeRotation = true;
        _rb.rotation = 0;
    }

    private IEnumerator RemoveTempBody()
    {
        _tempBody.linearVelocity = Vector2.zero;   // it stays exactly where it was let go
        yield return null;                         // wait one frame: the joint is destroyed by then, and a body can't be removed while a joint still needs it

        if (_tempBody != null) Destroy(_tempBody);
        _tempBody = null;
        _removeBody = null;
    }
}