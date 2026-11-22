using UnityEngine;

public abstract class Upgrade : ScriptableObject
{
    public string displayName = "New upgrade";
    [TextArea] public string description = "This does...";
    public Sprite icon;

    public abstract void OnAcquire(UpgradeManager manager);
}
