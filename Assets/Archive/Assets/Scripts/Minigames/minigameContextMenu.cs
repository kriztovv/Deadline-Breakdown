using System;
using UnityEngine;

public class minigameContextMenu : MonoBehaviour
{
    public static minigameContextMenu Instance { get; private set; }
    public minigameCanRightClick Owner { get; private set; }

    [SerializeField] private GameObject[] options;
    [SerializeField] private float optionWidth = 2f;
    [SerializeField] private float optionHeight = 0.5f;

    private Vector2 _offset;

    private void Awake()
    {
        Instance = this;
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(minigameCanRightClick owner, Vector2 worldPos, bool[] enabledOptions)
    {
        int row = 0;
        for (int i = 0; i < options.Length; i++)
        {
            bool on = i < enabledOptions.Length && enabledOptions[i];
            options[i].SetActive(on);
            if (!on) continue;

            options[i].transform.localPosition = new Vector3(optionWidth / 2f, -(row + 0.5f) * optionHeight, 0f);
            row++;
        }

        if (row == 0) { Hide(); return; }
        
        Owner = owner;
        _offset = worldPos + new Vector2(0.05f, -0.05f) - (Vector2)owner.transform.position;
        FollowOwner();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        Owner = null;
        foreach (GameObject option in options) option.SetActive(false);
        gameObject.SetActive(false);
    }

    // Call this on every left press. Returns true if an option was clicked.
    public bool HandleClick(Vector2 cursorWorldPos)
    {
        if (Owner == null) return false;

        int index = -1;
        for (int i = 0; i < options.Length; i++)
        {
            if (!options[i].activeSelf) continue;

            // finds the collider on the Option root or on a child like Background
            Collider2D col = options[i].GetComponentInChildren<Collider2D>();
            if (col != null && col.OverlapPoint(cursorWorldPos)) { index = i; break; }
        }

        minigameCanRightClick owner = Owner;
        Hide();
        if (index < 0) return false;

        owner.SelectOption(index);
        return true;
    }

    private void LateUpdate()
    {
        if (Owner == null) { Hide(); return; }
        FollowOwner();
    }

    private void FollowOwner()
    {
        Vector2 p = (Vector2)Owner.transform.position + _offset;
        transform.position = new Vector3(p.x, p.y, transform.position.z);
    }
}