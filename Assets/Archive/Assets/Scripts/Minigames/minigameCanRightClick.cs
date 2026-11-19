using System;
using UnityEngine;

public class minigameCanRightClick : MonoBehaviour
{
    [SerializeField] private bool[] enabledOptions;   // one entry per premade menu option

    public event Action<int> OptionSelected;          // other scripts on this object subscribe to this

    public void RightClick(Vector2 worldPos)          // Open
    {
        minigameContextMenu menu = minigameContextMenu.Instance;
        if (menu != null) menu.Show(this, worldPos, enabledOptions);
    }

    public void Close()
    {
        minigameContextMenu menu = minigameContextMenu.Instance;
        if (menu != null && menu.Owner == this) menu.Hide();
    }

    public void SelectOption(int index) => OptionSelected?.Invoke(index);   // called by the menu

    public void SetOptionEnabled(int index, bool on)   // for options that change at runtime
    {
        if (index >= 0 && index < enabledOptions.Length) enabledOptions[index] = on;
    }

    private void OnDisable() => Close();   // also runs when destroyed, so the menu never follows a dead object
    
    public bool IsOptionEnabled(int index) =>
        index >= 0 && index < enabledOptions.Length && enabledOptions[index];

    // runs an option as if it had been clicked in the menu, but only if this object offers it
    public bool TriggerOption(int index)
    {
        if (!IsOptionEnabled(index)) return false;
        SelectOption(index);
        return true;
    }
}