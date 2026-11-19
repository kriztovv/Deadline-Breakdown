using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

public class DeskMonitorSeat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform viewPoint;            // empty in front of the monitor, facing it
    [SerializeField] private minigameInputController monitorInput;
    [SerializeField] private GameObject prompt;              // optional "Press E" UI object
    [SerializeField] private GameObject crosshair;
    [SerializeField] private GameObject tutorial;
    [SerializeField] private GameObject heldText;
    [SerializeField] private GameObject controller;
    [SerializeField] private AudioClip sitSound;
    
    [Header("Disabled while at the monitor")]
    [SerializeField] private Behaviour[] disableWhileSeated; // movement/look script, CharacterController, PickupController, etc.

    [Header("Settings")]
    [SerializeField] private float interactRange = 2.5f;
    [SerializeField] private float transitionTime = 0.8f;
    

    private enum State { Away, Seated }
    private State state = State.Away;
    private bool busy;

    private Transform originalParent;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    
    private sceneController sceneController;
    private ScreenPopController effect;

    public static bool firstBoot = true;
    
    private readonly List<Behaviour> disabled = new List<Behaviour>();
    

    private void Start()
    {
        firstBoot = true;
        effect = controller.GetComponent<ScreenPopController>();
        sceneController = FindFirstObjectByType<sceneController>();
        if (prompt != null) prompt.SetActive(false);
        sceneController.StartMinigame("mainMenu");

        StartCoroutine(EnterNextFrame());
    }
    
    private IEnumerator EnterNextFrame()
    {
        yield return null; // let other scripts finish their Start first
        yield return Enter();
    }

    private void Update()
    {
        if (Keyboard.current == null || busy) return;
        
        if (AssignmentManager.Dead)
        {
            if (prompt != null) prompt.SetActive(false);
            return;   // no entering, no Escape exit while dead
        }

        if (state == State.Away)
        {
            Vector3 a = player.position, b = viewPoint.position;
            a.y = 0f; b.y = 0f;
            bool near = Vector3.Distance(a, b) < interactRange;
            
            if (prompt != null) prompt.SetActive(near);

            if (near && Keyboard.current.eKey.wasPressedThisFrame)
                StartCoroutine(Enter());
        }
        else
        {
            bool minigameOpen = ClassroomInput.Instance != null && ClassroomInput.Instance.MinigameOpen;
            if (!minigameOpen && Keyboard.current.escapeKey.wasPressedThisFrame && !firstBoot)
                StartCoroutine(Exit());
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (viewPoint == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(viewPoint.position, interactRange);
    }
    
    private void DisablePlayer()
    {
        disabled.Clear();
        foreach (Behaviour b in player.GetComponentsInChildren<Behaviour>())
        {
            if (b == this || !b.enabled) continue;
            if (b is Camera || b is AudioListener || b is AudioSource) continue;
            string ns = b.GetType().Namespace;
            if (ns != null && ns.StartsWith("UnityEngine.Rendering")) continue; // keeps post-processing on the camera
            disabled.Add(b);
            b.enabled = false;
        }
        foreach (Behaviour b in disableWhileSeated)
            if (b != null && b.enabled) { disabled.Add(b); b.enabled = false; }
    }

    private void EnablePlayer()
    {
        foreach (Behaviour b in disabled) if (b != null) b.enabled = true;
        disabled.Clear();
    }

    private IEnumerator Enter()
    {
        SFXManager.instance.PlaySFX(sitSound, transform, 0.3f);
        busy = true;
        if (prompt != null) prompt.SetActive(false);

        DisablePlayer();
        crosshair.SetActive(false);
        tutorial.SetActive(false);
        heldText.SetActive(false);

        effect.Trigger();

        originalParent = playerCamera.parent;
        originalLocalPos = playerCamera.localPosition;
        originalLocalRot = playerCamera.localRotation;
        playerCamera.SetParent(null, true);

        yield return MoveCamera(playerCamera.position, playerCamera.rotation,
                                viewPoint.position, viewPoint.rotation);

        // the virtual cursor follows the real mouse, so the real one must not be locked
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;

        monitorInput.StartInput();
        state = State.Seated;
        
        busy = false;
    }

    private IEnumerator Exit()
    {
        busy = true;
        monitorInput.LeaveOn();
        crosshair.SetActive(true);
        tutorial.SetActive(true);
        heldText.SetActive(true);
     
        SFXManager.instance.PlaySFX(sitSound, transform, 0.3f);
        effect.Trigger();

        Vector3 homePos = originalParent.TransformPoint(originalLocalPos);
        Quaternion homeRot = originalParent.rotation * originalLocalRot;
        yield return MoveCamera(playerCamera.position, playerCamera.rotation, homePos, homeRot);

        playerCamera.SetParent(originalParent, true);
        playerCamera.localPosition = originalLocalPos;
        playerCamera.localRotation = originalLocalRot;

        EnablePlayer();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        state = State.Away;
        busy = false;
    }

    private IEnumerator MoveCamera(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / transitionTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            playerCamera.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k),
                                                Quaternion.Slerp(fromRot, toRot, k));
            yield return null;
        }
        playerCamera.SetPositionAndRotation(toPos, toRot);
    }
}