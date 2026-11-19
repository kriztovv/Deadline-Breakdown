using System.Collections.Generic;
using UnityEngine;

public class minigameSelection : MonoBehaviour
{
    [SerializeField] private Vector2 maxSize = new Vector2(6f, 4f);   // largest box the player can drag (world units)
    [SerializeField] private LayerMask selectableLayers = ~0;

    private SpriteRenderer _renderer;
    private ContactFilter2D _filter;
    private readonly Collider2D[] _hits = new Collider2D[64];
    private readonly List<minigameCanSelect> _selected = new List<minigameCanSelect>();

    private bool _active;
    private Vector2 _start;   // the corner where the drag began, it stays fixed
    private Vector2 _end;     // the opposite corner, follows the cursor (clamped)

    public IReadOnlyList<minigameCanSelect> Selected => _selected;   // for later use
    
    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _renderer.enabled = false;   // hide the sprite, but keep the object (and this script) alive

        _filter = new ContactFilter2D();
        _filter.SetLayerMask(selectableLayers);
        _filter.useTriggers = true;
    }

    public void Begin(Vector2 worldPos)
    {
        _active = true;
        _start = worldPos;
        _end = worldPos;
        _renderer.enabled = true;
        Apply();
    }

    public void Drag(Vector2 worldPos)
    {
        if (!_active) return;

        // limit how far the box can extend from the start corner, in either direction
        Vector2 delta = worldPos - _start;
        delta.x = Mathf.Clamp(delta.x, -maxSize.x, maxSize.x);
        delta.y = Mathf.Clamp(delta.y, -maxSize.y, maxSize.y);

        _end = _start + delta;
        Apply();
    }

    public void End()
    {
        if (!_active) return;
        _active = false;
        _renderer.enabled = false;
        
        Clear();
        int count = gameObject.scene.GetPhysicsScene2D().OverlapArea(_start, _end, _filter, _hits);

        for (int i = 0; i < count; i++)
        {
            // GetComponentInParent also finds the script when the collider is on a child
            minigameCanSelect selectable = _hits[i].GetComponentInParent<minigameCanSelect>();
            if (selectable == null || _selected.Contains(selectable)) continue;   // skip duplicates (several colliders on one object)

            _selected.Add(selectable);
            selectable.SetSelected(true);
            Debug.Log("Selected: " + selectable.name);
        }

        if (_selected.Count == 0) Debug.Log("Nothing selected");
    }

    public void Clear()
    {
        foreach (var s in _selected) if (s != null) s.SetSelected(false);
        _selected.Clear();
    }
    
    // position the box at the midpoint of the two corners and scale it to span them
    private void Apply()
    {
        Vector2 center = (_start + _end) / 2f;
        Vector2 size = new Vector2(Mathf.Abs(_end.x - _start.x), Mathf.Abs(_end.y - _start.y));

        transform.position = new Vector3(center.x, center.y, transform.position.z);
        transform.localScale = new Vector3(size.x, size.y, 1f);
    }
}