using UnityEngine;

public class CursorSkin : MonoBehaviour
{
    public enum Mode { Default, Hand, HandClick }

    [SerializeField] private Sprite defaultSprite;   // "default"
    [SerializeField] private Sprite handSprite;      // "hand"
    [SerializeField] private Sprite handClickSprite; // "hand-click"

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        Set(Mode.Default);
    }

    public void Set(Mode mode)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        switch (mode)
        {
            case Mode.Hand:      sr.sprite = handSprite; break;
            case Mode.HandClick: sr.sprite = handClickSprite; break;
            default:             sr.sprite = defaultSprite; break;
        }
    }
}