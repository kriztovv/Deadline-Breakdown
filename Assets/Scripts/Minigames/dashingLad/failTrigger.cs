using System;
using UnityEngine;

public class failTrigger : MonoBehaviour
{
    [SerializeField] private dashingLad ladScript;
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            ladScript.End(true);
        }
    }
}
