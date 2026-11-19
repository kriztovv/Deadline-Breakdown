using System;
using System.Linq;
using UnityEngine;

public class minigameInputController : MonoBehaviour
{
    [SerializeField] public bool atMonitor;
    [SerializeField] private Camera realCamera;
    [SerializeField] private KeyCode[] reservedKeys;

    [Header("Cursor outside the monitor (assumes the built-in Quad)")]
    [SerializeField] private bool flipU;
    [SerializeField] private bool flipV;

    private static readonly KeyCode[] AllKeys =
        ((KeyCode[])Enum.GetValues(typeof(KeyCode))).Distinct().ToArray();

    private bool _leftDown, _rightDown;   // presses that were sent to the minigame and haven't been released yet
    private Transform _monitor;           // the monitor object, remembered from the last raycast hit

    private void ForwardKeys()
    {
        if (!Input.anyKeyDown) return;   // cheap early exit: the loop only runs on frames where something was pressed

        foreach (KeyCode key in AllKeys)
        {
            if (key == KeyCode.None || key >= KeyCode.Mouse0) continue;   // skip mouse and joystick buttons
            if (Array.IndexOf(reservedKeys, key) >= 0) continue;
            if (Input.GetKeyDown(key)) MonitorInput.FireKey(key);
        }
    }

    public void LeaveOn()
    {
        ReleaseHeldButtons();   // the minigame must not be left thinking a button is still down
        atMonitor = false;
        MonitorInput.Clear();
        Debug.Log("Monitor now listening for input");
    }

    public void StartInput()
    {
        MonitorInput.Clear();
        _leftDown = _rightDown = false;
        atMonitor = true;
        Debug.Log("Monitor input disabled");
    }

    private void OnDisable() => ReleaseHeldButtons();

    private void ReleaseHeldButtons()
    {
        if (_leftDown)  { _leftDown = false;  MonitorInput.FireInput(MonitorInteraction.PrimaryUp); }
        if (_rightDown) { _rightDown = false; MonitorInput.FireInput(MonitorInteraction.SecondaryUp); }
    }

    private void Update()
    {
        if (!atMonitor) return;

        MonitorInput.Movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        ForwardKeys();

        Ray ray = realCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 10f) && hit.transform.CompareTag("Monitor"))
        {
            _monitor = hit.transform;
            MonitorInput.CursorOnScreen = true;
            MonitorInput.CursorUV = hit.textureCoord;

            // Presses only count while the cursor is over the monitor
            if (Input.GetMouseButtonDown(0)) { _leftDown = true;  MonitorInput.FireInput(MonitorInteraction.PrimaryDown); }
            if (Input.GetMouseButtonDown(1)) { _rightDown = true; MonitorInput.FireInput(MonitorInteraction.SecondaryDown); }
        }
        else
        {
            MonitorInput.CursorOnScreen = false;
            ClampToMonitor(ray);   // the cursor sticks to the nearest edge instead of freezing
        }

        // Releases count everywhere, for every press that was sent, even outside the monitor.
        // Checking "is no longer held" (instead of the release event) also catches a release that was missed, e.g. when the window loses focus
        if (_leftDown && !Input.GetMouseButton(0))  { _leftDown = false;  MonitorInput.FireInput(MonitorInteraction.PrimaryUp); }
        if (_rightDown && !Input.GetMouseButton(1)) { _rightDown = false; MonitorInput.FireInput(MonitorInteraction.SecondaryUp); }
    }

    // Where the mouse points on the monitor's plane, clamped to the screen's edges
    private void ClampToMonitor(Ray ray)
    {
        if (_monitor == null) return;   // never pointed at the monitor yet

        Plane plane = new Plane(_monitor.forward, _monitor.position);
        if (!plane.Raycast(ray, out float distance)) return;   // looking away from the monitor's plane: keep the last position

        // The built-in Quad spans -0.5 to 0.5 in its local space, which maps to UV 0 to 1
        Vector3 local = _monitor.InverseTransformPoint(ray.GetPoint(distance));
        float u = Mathf.Clamp01(local.x + 0.5f);
        float v = Mathf.Clamp01(local.y + 0.5f);
        if (flipU) u = 1f - u;
        if (flipV) v = 1f - v;

        MonitorInput.CursorUV = new Vector2(u, v);
    }
}