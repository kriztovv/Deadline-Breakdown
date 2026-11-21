using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Pill pack")]
public class PillPackUpgrade : Upgrade
{
    public string sceneObjectKey = "boosterpack";

    public override void OnAcquire(UpgradeManager m)
    {
        GameObject pack = m.GetObject(sceneObjectKey);
        pack.SetActive(true);
    }
}
