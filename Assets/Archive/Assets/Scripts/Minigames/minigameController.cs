using System;
using UnityEngine;

public class minigameController : MonoBehaviour
{
    //Hooks for the minigames to override. The built-in features run BEFORE these, so no base calls are needed.
    protected virtual void OnLeftDown() {}
    protected virtual void OnLeftUp() {}
    protected virtual void OnLeftClick() {}
    protected virtual void OnLeftHoldStart() {}
    protected virtual void OnLeftHoldEnd() {}

    protected virtual void OnRightDown() {}
    protected virtual void OnRightUp() {}
    protected virtual void OnRightClick() {}
    protected virtual void OnRightHoldStart() {}
    protected virtual void OnRightHoldEnd() {}

    protected virtual void OnKeyPressed(KeyCode key) {}

    //Features: toggled per minigame in the Inspector
    [Header("Features")]
    [SerializeField] private bool useCursor;
    [SerializeField] private bool useRightClick = true;
    [SerializeField] private bool useDragSelect = true;
    [SerializeField] private bool useDragObjects = true;
    [SerializeField] private bool useHorizontalMovement;
    [SerializeField] private bool useVerticalMovement;
    [SerializeField] private bool movementIgnoresTimeScale;   // movement uses unscaled time (see MovementDeltaTime)

    [Header("Setup")]
    [SerializeField] private LayerMask clickableLayers;
    [SerializeField] private float dragThreshold = 0.01f;     // 0.01 = 1% of the monitor
    [SerializeField] private minigameSelection selection;     // needed for Use Drag Select
    
    [Header("Object dragging")]
    [SerializeField] private float dragFrequency = 5f;
    [SerializeField] private float dragDampingRatio = 1f;

    // Which key runs which context menu option on the selected objects
    // (works only with Use Drag Select and Use Right Click both on)
    [Header("Key shortcuts")]
    [SerializeField] private KeyOptionBinding[] keyBindings =
    {
        new KeyOptionBinding { key = KeyCode.Delete,    option = 0 },
        new KeyOptionBinding { key = KeyCode.Backspace, option = 0 },
        new KeyOptionBinding { key = KeyCode.F,         option = 1 },
        new KeyOptionBinding { key = KeyCode.Space,     option = 2 },
    };

    //Input state
    private bool _pressedLeft, _pressedRight;
    private bool _holdingLeft, _holdingRight;
    private Vector2 _leftPressUV, _rightPressUV;

    //Scene references
    private GameObject _cursorObject;
    private Camera _minigameCamera;

    //Hover
    protected GameObject _hoveredObject;
    private ContactFilter2D _filter;
    private readonly Collider2D[] _hits = new Collider2D[16];

    //Built-in feature state
    private minigameCanDrag _dragScript;
    private TargetJoint2D _joint;
    private bool _menuConsumedPress;   // this press clicked a context menu option
    private bool _menuWasOpen;         // the context menu was open when this press started
    
    private CursorSkin _cursorSkin;

    private void Awake()
    {
        _filter = new ContactFilter2D();
        _filter.SetLayerMask(clickableLayers);
        _filter.useTriggers = true;

        MgAwake();
    }

    private void Start()
    {
        _minigameCamera = GameObject.Find("camera").GetComponent<Camera>();
        _cursorObject = GameObject.Find("cursor");
        
        if (_cursorObject != null) _cursorObject.TryGetComponent(out _cursorSkin);

        MgStart();
    }

    private void OnEnable()
    {
        MonitorInput.OnInteraction += Interact;
        MonitorInput.OnKeyDown += HandleKey;

        MgOnEnable();
    }

    private void OnDisable()
    {
        MonitorInput.OnInteraction -= Interact;
        MonitorInput.OnKeyDown -= HandleKey;

        _pressedLeft = _pressedRight = _holdingLeft = _holdingRight = false;

        MgOnDisable();
    }
    
    private void RefreshCursor()
    {
        if (_cursorSkin == null) return;
        CursorSkin.Mode mode =
            (_holdingLeft || _holdingRight) ? CursorSkin.Mode.HandClick :
            (_pressedLeft || _pressedRight) ? CursorSkin.Mode.Hand :
            CursorSkin.Mode.Default;
        _cursorSkin.Set(mode);
    }

    //Input routing
    private void Interact(MonitorInteraction interaction)
    {
        switch (interaction)
        {
            case MonitorInteraction.PrimaryDown:
                _pressedLeft = true;
                _leftPressUV = MonitorInput.CursorUV;
                BuiltInLeftDown();
                OnLeftDown();
                
                RefreshCursor();//Switch mouse to: hand
                break;

            case MonitorInteraction.PrimaryUp:
                if (!_pressedLeft) break;   // ignore a release without a matching press
                OnLeftUp();
                if (_holdingLeft) { BuiltInLeftHoldEnd(); OnLeftHoldEnd(); }
                else              { BuiltInLeftClick();   OnLeftClick();   }
                _pressedLeft = false;
                _holdingLeft = false;
                
                RefreshCursor();//Switch mouse to: default
                break;

            case MonitorInteraction.SecondaryDown:
                _pressedRight = true;
                _rightPressUV = MonitorInput.CursorUV;
                OnRightDown();
                
                RefreshCursor();//Switch mouse to: hand
                break;

            case MonitorInteraction.SecondaryUp:
                if (!_pressedRight) break;
                OnRightUp();
                if (_holdingRight) OnRightHoldEnd();
                else { BuiltInRightClick(); OnRightClick(); }
                _pressedRight = false;
                _holdingRight = false;
                
                RefreshCursor();//Switch mouse to: default
                break;
        }
    }

    private void HandleKey(KeyCode key)
    {
        if (useDragSelect && useRightClick && selection != null) RunKeyBinding(key);
        OnKeyPressed(key);
    }

    //Built-in features
    private void BuiltInLeftDown()
    {
        _menuWasOpen = false;
        _menuConsumedPress = false;
        if (!useRightClick) return;

        var menu = minigameContextMenu.Instance;
        _menuWasOpen = menu != null && menu.Owner != null;
        _menuConsumedPress = menu != null && menu.HandleClick(CursorPosition());   // any left press also closes the menu
    }

    private void BuiltInLeftClick()
    {
        if (_hoveredObject != null && _hoveredObject.TryGetComponent<mainMenuCanLeftClick>(out var lc))
            lc.LeftClick();
        if (_menuWasOpen) return;   // this click only used or closed the menu
        if (useDragSelect && selection != null && _hoveredObject == null) selection.Clear();   // clicked empty space
    }

    private void BuiltInLeftHoldStart()
    {
        if (_menuConsumedPress) return;   // this press was used to click a menu option

        if (useDragObjects && _hoveredObject != null && _hoveredObject.TryGetComponent(out _dragScript))
        {
            _dragScript.StartDrag();

            _joint = _hoveredObject.AddComponent<TargetJoint2D>();
            _joint.autoConfigureTarget = false;
            _joint.anchor = _hoveredObject.transform.InverseTransformPoint(CursorPosition());   // the grabbed point
            _joint.target = CursorPosition();
            _joint.dampingRatio = dragDampingRatio;
            _joint.frequency = dragFrequency;
        }
        else if (useDragSelect && selection != null)
        {
            selection.Begin(CursorPosition());   // nothing draggable under the cursor
        }
    }

    private void BuiltInLeftHoldEnd()
    {
        if (useDragSelect && selection != null) selection.End();   // does nothing if no selection is in progress

        if (_joint != null) Destroy(_joint);
        if (_dragScript != null) _dragScript.StopDrag();
        _joint = null;
        _dragScript = null;
    }

    private void BuiltInRightClick()
    {
        if (!useRightClick) return;

        if (_hoveredObject != null && _hoveredObject.TryGetComponent<minigameCanRightClick>(out var rc))
            rc.RightClick(CursorPosition());
        else
            minigameContextMenu.Instance?.Hide();
    }

    private void RunKeyBinding(KeyCode key)
    {
        foreach (KeyOptionBinding binding in keyBindings)
        {
            if (binding.key != key) continue;
            TriggerOnSelected(binding.option);
            return;
        }
    }

    // runs a context menu option on every currently selected object that offers it, then deselects them
    private void TriggerOnSelected(int option)
    {
        foreach (minigameCanSelect s in selection.Selected)
        {
            if (s == null) continue;   // destroyed since it was selected
            if (s.TryGetComponent(out minigameCanRightClick rc)) rc.TriggerOption(option);
        }

        selection.Clear();
    }

    //Helpers for the minigames
    protected Vector2 CursorPosition() => _minigameCamera.ViewportToWorldPoint(MonitorInput.CursorUV);

    // The movement keys, with the axes the minigame doesn't use zeroed out
    public Vector2 MovementInput
    {
        get
        {
            Vector2 m = MonitorInput.Movement;
            return new Vector2(useHorizontalMovement ? m.x : 0f, useVerticalMovement ? m.y : 0f);
        }
    }

    public bool HorizontalMovement => useHorizontalMovement;
    public bool VerticalMovement => useVerticalMovement;
    public bool LeftHeld => _pressedLeft;     // the left button is down (from the press until the release)
    public bool RightHeld => _pressedRight;   // the same for the right button
    public bool MovementIgnoresTimeScale => movementIgnoresTimeScale;

    // Use this instead of Time.deltaTime in movement code, so the movement toggle takes effect
    public float MovementDeltaTime => movementIgnoresTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
    
    protected virtual void Update()
    {
        //Hovered object
        GameObject newHover = null;
        if (MonitorInput.CursorOnScreen)
        {
            int count = gameObject.scene.GetPhysicsScene2D().OverlapPoint(CursorPosition(), _filter, _hits);

            int bestLayer = int.MinValue;
            int bestOrder = int.MinValue;

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer sr = _hits[i].GetComponentInChildren<SpriteRenderer>();
                int layer = sr != null ? SortingLayer.GetLayerValueFromID(sr.sortingLayerID) : int.MinValue;
                int order = sr != null ? sr.sortingOrder : int.MinValue;

                if (newHover == null || layer > bestLayer || (layer == bestLayer && order > bestOrder))
                {
                    bestLayer = layer;
                    bestOrder = order;
                    newHover = _hits[i].gameObject;
                }
            }

            // a pressed button becomes a drag once the cursor has moved far enough
            if (_pressedLeft && !_holdingLeft &&
                Vector2.Distance(MonitorInput.CursorUV, _leftPressUV) > dragThreshold)
            {
                _holdingLeft = true;
                BuiltInLeftHoldStart();
                OnLeftHoldStart();
                
                RefreshCursor();//Switch mouse to: hand-click
            }
            if (_pressedRight && !_holdingRight &&
                Vector2.Distance(MonitorInput.CursorUV, _rightPressUV) > dragThreshold)
            {
                _holdingRight = true;
                OnRightHoldStart();
                
                RefreshCursor();//Switch mouse to: hand-click
            }
        }
        _hoveredObject = newHover;

        //The selection box and the dragged object follow the cursor
        if (useDragSelect && selection != null) selection.Drag(CursorPosition());   // does nothing unless a selection is in progress
        if (_joint != null) _joint.target = CursorPosition();

        //Cursor movement
        if (useCursor && _cursorObject != null) _cursorObject.transform.position = CursorPosition();

        MgUpdate();
    }

    private void FixedUpdate()
    {
        MgFixedUpdate();
    }

    //Minigame methods
    protected virtual void MgStart() {}
    protected virtual void MgUpdate() {}
    protected virtual void MgAwake() {}
    protected virtual void MgOnEnable() {}
    protected virtual void MgOnDisable() {}
    protected virtual void MgFixedUpdate() {}
}

[Serializable]
public struct KeyOptionBinding
{
    public KeyCode key;
    public int option;   // slot in the context menu that this key triggers
}