using UnityEngine;

public class HeldItemHints : MonoBehaviour
{
    [SerializeField] private PickupController pickup;
    [SerializeField] private CanvasGroup heldGroup;
    [SerializeField] private GameObject eatLine;
    [SerializeField] private float fadeSpeed = 8f;

    private void Start()
    {
        heldGroup.alpha = 0f;
        eatLine.SetActive(false);
    }

    private void Update()
    {
        Rigidbody held = pickup.Held;
        bool holding = held != null;

        if (holding)
            eatLine.SetActive(held.TryGetComponent(out Pill _));

        float target = holding ? 1f : 0f;
        heldGroup.alpha = Mathf.MoveTowards(heldGroup.alpha, target, fadeSpeed * Time.deltaTime);
    }
}