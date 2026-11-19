using UnityEngine;

public class recycleBin : MonoBehaviour
{

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Bug")){collision.GetComponent<bugRightclick>().Delete();}
    }
}
