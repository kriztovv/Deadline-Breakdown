using UnityEngine;

public class minigameCanSelect : MonoBehaviour
{
    private SpriteRenderer _sr;
    private Color _originalColor;
    private bool _highlighted;

    private void Awake() => _sr = GetComponentInChildren<SpriteRenderer>();

    public void SetSelected(bool selected)
    {
        if (selected == _highlighted) return;   // never overwrite the stored original
        _highlighted = selected;

        if (selected)
        {
            _originalColor = _sr.color;
            _sr.color = _originalColor * new Color(0.6f, 0.8f, 1f, 1f);   // tint towards blue, keeps alpha
        }
        else
        {
            _sr.color = _originalColor;
        }
    }
}