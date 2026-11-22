using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Stat upgrade")]
public class StatUpgrade : Upgrade
{
    public string statKey = "walkSpeed";
    public float add = 0f;
    public float multiply = 1f;

    public override void OnAcquire(UpgradeManager m) => m.ModifyStat(statKey, add, multiply);
}
