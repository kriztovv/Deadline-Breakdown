using UnityEngine;

[CreateAssetMenu(fileName = "New Extra Time", menuName = "Upgrades/Extra time subject")]
public class ExtraTimeUpgrade : Upgrade
{
    [SerializeField] private float seconds = 50f;

    public override void OnAcquire(UpgradeManager m)
    {
        if (AssignmentManager.Instance != null)
            AssignmentManager.Instance.PickBonusSubject(seconds);
    }
}
