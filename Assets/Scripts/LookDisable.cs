using UnityEngine;

public class LookDisable : MonoBehaviour
{
    [SerializeField] private PickupController pickup;
    [SerializeField] private MonoBehaviour lookScript; // your controller's camera look component

    private void Update()
    {
        lookScript.enabled = !pickup.IsRotating;
    }
}
