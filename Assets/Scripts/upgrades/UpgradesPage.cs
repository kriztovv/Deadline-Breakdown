using TMPro;
using UnityEngine;

public class UpgradesPage : MonoBehaviour
{
    [System.Serializable]
    public class OfferCard
    {
        public GameObject root;
        public SpriteRenderer icon;
        public TMP_Text title;
        public TMP_Text description;
        public ClassroomClickable clickable;
    }

    [SerializeField] private GameObject offerGroup;        // both cards and the "OR" text
    [SerializeField] private TMP_Text waitText;            // "Next upgrade in 1:42"
    [SerializeField] private OfferCard[] cards = new OfferCard[2];
    
    [Header("Owned upgrades")]
    [SerializeField] private OwnedUpgradeSlot ownedSlotPrefab;
    [SerializeField] private Transform ownedContainer;     // an empty object where the slots appear
    [SerializeField] private int columns = 8;              // slots per row before it wraps
    [SerializeField] private Vector2 slotSpacing = new Vector2(1f, 1f);

    private void Awake()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;
            cards[i].clickable.onClick.AddListener(() => UpgradeManager.Instance.Pick(index));
        }
    }

    private void OnEnable()
    {
        if (UpgradeManager.Instance != null) UpgradeManager.Instance.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (UpgradeManager.Instance != null) UpgradeManager.Instance.Changed -= Refresh;
    }

    private void Update()
    {
        var m = UpgradeManager.Instance;
        if (m == null || m.Offer.Length > 0) return;

        if (!m.Running) { waitText.text = "No upgrades yet"; return; }
        int t = Mathf.CeilToInt(m.TimeLeft);
        waitText.text = $"Next upgrade in {t / 60}:{t % 60:00}";
    }

    private void Refresh()
    {
        var m = UpgradeManager.Instance;
        if (m == null) return;

        bool hasOffer = m.Offer.Length > 0;
        offerGroup.SetActive(hasOffer);
        waitText.gameObject.SetActive(!hasOffer);

        for (int i = 0; i < cards.Length; i++)
        {
            bool show = i < m.Offer.Length;
            cards[i].root.SetActive(show);
            if (!show) continue;
            cards[i].icon.sprite = m.Offer[i].icon;
            cards[i].icon.enabled = m.Offer[i].icon != null;
            cards[i].title.text = m.Offer[i].displayName;
            cards[i].description.text = m.Offer[i].description;
        }
        
        // only the missing slots are created, so existing ones don't pop in again
        for (int i = ownedContainer.childCount; i < m.Owned.Count; i++)
        {
            OwnedUpgradeSlot slot = Instantiate(ownedSlotPrefab, ownedContainer);
            slot.transform.localPosition = new Vector3((i % columns) * slotSpacing.x,
                -(i / columns) * slotSpacing.y, 0f);
            slot.icon.sprite = m.Owned[i].icon;
        }
    }
}
