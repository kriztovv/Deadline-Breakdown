using UnityEngine;

public class MinigameNote : MonoBehaviour
{
    [SerializeField] private GameObject notePrefab;

    private void OnEnable()  { if (NoteSlot.Instance != null) NoteSlot.Instance.Push(notePrefab); }
    private void OnDisable() { if (NoteSlot.Instance != null) NoteSlot.Instance.Pop(notePrefab); }
}