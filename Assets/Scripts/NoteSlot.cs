using System.Collections.Generic;
using UnityEngine;

public class NoteSlot : MonoBehaviour
{
    public static NoteSlot Instance { get; private set; }

    [SerializeField] private GameObject defaultNotePrefab;   // the classroom note

    private readonly List<GameObject> stack = new List<GameObject>();
    private readonly Dictionary<GameObject, GameObject> spawned = new Dictionary<GameObject, GameObject>();
    private GameObject current;

    private void Awake()
    {
        Instance = this;
        Refresh();
    }

    public void Push(GameObject prefab)
    {
        stack.Remove(prefab);
        stack.Add(prefab);      // newest scene wins
        Refresh();
    }

    public void Pop(GameObject prefab)
    {
        stack.Remove(prefab);
        Refresh();              // falls back to the previous one, ending at the classroom note
    }

    private void Refresh()
    {
        GameObject wanted = stack.Count > 0 ? stack[stack.Count - 1] : defaultNotePrefab;
        if (current != null) current.SetActive(false);
        if (wanted == null) { current = null; return; }

        if (!spawned.TryGetValue(wanted, out GameObject inst) || inst == null)
        {
            inst = Instantiate(wanted, transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            spawned[wanted] = inst;
        }
        inst.SetActive(true);
        current = inst;
    }
}