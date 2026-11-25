using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class ClassroomInput : MonoBehaviour
{
    public static ClassroomInput Instance { get; private set; }

    [Tooltip("Parent of the classroom camera, cursor, background and all cards")]
    [SerializeField] private GameObject classroomRoot;
    [Tooltip("The camera that renders the classroom to the monitor")]
    [SerializeField] private Camera classroomCamera;
    [SerializeField] private Transform cursorObject;   // the classroom's "cursor" sprite

    private sceneController sceneController;
    private SpriteRenderer cursorRenderer;

    private SubjectCard hovered, pressed;
    private bool minigameOpen;
    private string openSubject;
    
    private ClassroomClickable hoveredC, pressedC;
    
    private CursorSkin cursorSkin;
    
    private string openScene;
    
    private void Awake() => Instance = this;
    public bool MinigameOpen => minigameOpen;
    
    [SerializeField] private float minPlaySeconds = 10f;
    private float openedAt;
    private bool playCounted;
    
    public bool InMinigame => minigameOpen;
    
    private int gradeAtOpen;
    private SubjectCard openCard;
    
    public string OpenSubject => openSubject;

    private void Start()
    {
        sceneController = FindFirstObjectByType<sceneController>();
        if (cursorObject != null) cursorRenderer = cursorObject.GetComponent<SpriteRenderer>();
        if (cursorObject != null) cursorObject.TryGetComponent(out cursorSkin);
        
    }
    
    [SerializeField] private KeyCode quitKey = KeyCode.Escape;

    private void OnEnable()
    {
        MonitorInput.OnInteraction += OnInteraction;
        MonitorInput.OnKeyDown += OnKey;
    }

    private void OnDisable()
    {
        MonitorInput.OnInteraction -= OnInteraction;
        MonitorInput.OnKeyDown -= OnKey;
    }

    private void OnKey(KeyCode key)
    {
        if (minigameOpen && key == quitKey)
            CloseMinigame(false);   // quit without completing
    }

    private Vector2 CursorWorld() => classroomCamera.ViewportToWorldPoint(MonitorInput.CursorUV);

    private SubjectCard CardUnderCursor()
    {
        if (!MonitorInput.CursorOnScreen) return null;
        Collider2D col = classroomRoot.scene.GetPhysicsScene2D().OverlapPoint(CursorWorld());
        return col != null ? col.GetComponentInParent<SubjectCard>() : null;
    }

    private void Update()
    {
        if (minigameOpen && !playCounted && Time.unscaledTime - openedAt >= minPlaySeconds)
        {
            playCounted = true;
            GameStats.Instance.AddPlay(openSubject);
        }
        
        if (minigameOpen) return;

        if (cursorRenderer != null)
        {
            cursorRenderer.enabled = MonitorInput.CursorOnScreen;
            if (MonitorInput.CursorOnScreen)
                cursorObject.position = CursorWorld();
        }
        
        ClassroomClickable c = ClickableUnderCursor();
        if (c != hoveredC)
        {
            if (hoveredC != null) hoveredC.SetHovered(false);
            hoveredC = c;
        }
        if (hoveredC != null) hoveredC.SetHovered(hoveredC.Interactable);

        SubjectCard card = CardUnderCursor();
        if (card != hovered)
        {
            if (hovered != null) hovered.SetHovered(false);
            hovered = card;
        }
        if (hovered != null) hovered.SetHovered(hovered.CanOpen);   // re-checked each frame, so a new assignment lights up while you hover

        RefreshCursor();
    }

    private void RefreshCursor()
    {
        if (cursorSkin == null) return;

        bool overCard = hovered != null && hovered.CanOpen;
        bool overBtn = hoveredC != null && hoveredC.Interactable;
        bool down = (overCard && pressed == hovered) || (overBtn && pressedC == hoveredC);

        cursorSkin.Set(!(overCard || overBtn) ? CursorSkin.Mode.Default
            : down ? CursorSkin.Mode.HandClick : CursorSkin.Mode.Hand);
    }

    private void OnInteraction(MonitorInteraction i)
    {
        if (minigameOpen) return;

        if (i == MonitorInteraction.PrimaryDown)
        {
            pressed = CardUnderCursor();
            pressedC = ClickableUnderCursor();
            
        }
        else if (i == MonitorInteraction.PrimaryUp)
        {
            SubjectCard card = CardUnderCursor();
            if (card != null && card == pressed && card.CanOpen) Open(card);
            
            ClassroomClickable c = ClickableUnderCursor();
            if (c != null && c == pressedC && c.Interactable) c.Click();
            pressedC = null;
            
            pressed = null;
        }
    }

    private void Open(SubjectCard card)
    {
        minigameOpen = true;
        openedAt = Time.unscaledTime;
        playCounted = false;
        
        openSubject = card.SubjectId;
        openScene = card.SceneName;
        
        openCard = card;
        gradeAtOpen = AssignmentManager.Instance.GradeIndex(openSubject);
        
        if (cursorSkin != null) cursorSkin.Set(CursorSkin.Mode.Default);
        if (hovered != null) hovered.SetHovered(false);
        hovered = null;

        classroomRoot.SetActive(false);
        sceneController.StartMinigame(openScene);
        StartCoroutine(ConfigureMinigame());
    }

    private IEnumerator ConfigureMinigame()
    {
        Scene scene = SceneManager.GetSceneByName(openScene);
        yield return new WaitUntil(() => scene.isLoaded);
        yield return null;   // one more frame, so Awake/OnEnable of everything in it have run

        int c = GameStats.Instance.Completions(openSubject);
        switch (openScene)
        {
            case "bugfixing":
                Debug.Log("Starting bugfixing. Completions: " + c);
                bugfix bugfixScript = FindIn<bugfix>(scene);
                if (c == 0) bugfixScript.tutorial = true; 
                bugfixScript.spawnInterval = (float)(0.4 - c * 0.05);
                bugfixScript.maxBugs = 10 + c * 2;
                bugfixScript.StartGame();
                break;
            case "pressBombardment":
                Debug.Log("Starting press bombardment. Completions: " + c);
                pressBombardment pressScript = FindIn<pressBombardment>(scene);
                pressScript.startReadTime = (float)(2 - c * 0.25);
                pressScript.startAnswerTime = (float)(4 - c * 0.25);
                pressScript.StartGame();
                break;
            case "investit":
                Debug.Log("Starting invest-it. Completions: " + c);
                minigameSpawner spawnScript = FindIn<minigameSpawner>(scene); 
                investit investitScript = FindIn<investit>(scene);
                if (c == 0) investitScript.tutorial = true;
                spawnScript.startInterval = (float)(0.4 - c / 10f);
                investitScript.requiredMoney = 10 +  c * 2;
                investitScript.StartGame();
                break;
            case "dashingLad":
                Debug.Log("Starting dashing lad. Completions: " + c);
                dashingLad ladScript = FindIn<dashingLad>(scene);
                ladScript.segmentCount = 20 + c;
                ladScript.startSpeed = 10 + c * 2;
                ladScript.StartGame();
                break;
            case "colorTheory":
                Debug.Log("Starting color theory. Completions: " + c);
                paintSpawner artScript = FindIn<paintSpawner>(scene);
                if (c == 0) artScript.tutorial = true;
                artScript.startPairs = 2 + c;
                artScript.roundsToWin = 3 + c;
                artScript.StartGame();
                break;
            case "mobliterator":
                Debug.Log("Starting mobliterator. Completions: " + c);
                mobliterator chemScript = FindIn<mobliterator>(scene);
                if (c == 0) chemScript.tutorial = true;
                chemScript.moleculeCount = 30 + 2 * c;
                chemScript.StartGame();
                break;
            case "geography":
                Debug.Log("Starting geography. Completions: " + c);
                geography geoScript = FindIn<geography>(scene);
                geoScript.spawnDelay = (float)(1 - c * 0.05);
                geoScript.projectileSpeed = (float)(4 + c * 0.2);
                geoScript.StartGame();
                break;
        }
        
    }

    private static T FindIn<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);   // true = include inactive
            if (found != null) return found;
        }
        Debug.LogError($"{typeof(T).Name} not found in scene '{scene.name}'");
        return null;
    }
    
    public void CloseMinigame(bool completed)
    {
        if (!minigameOpen) return;        // also swallows the call coming back from StopMinigame()
        minigameOpen = false;

        if (completed)
        {
            AssignmentManager.Instance.Complete(openSubject);
            if (!playCounted) { playCounted = true; GameStats.Instance.AddPlay(openSubject); }
            GameStats.Instance.AddCompletion(openSubject);
        }
        MonitorInput.Clear();
        StartCoroutine(CloseRoutine());
    }

    private IEnumerator CloseRoutine()
    {
        if (SceneManager.GetSceneByName(openScene).isLoaded)
        {
            sceneController.StopMinigame();
            // wait until the scene is really gone, so its camera and cube never flash on the monitor
            yield return new WaitUntil(() => !SceneManager.GetSceneByName(openScene).isLoaded);
        }
        else
        {
            Debug.LogWarning($"Scene '{openScene}' is not loaded; check the card's Scene Name.");
        }
        classroomRoot.SetActive(true);
        var mgr = AssignmentManager.Instance;
        int oldIdx = mgr.GradeIndex(openSubject);          // still the real, unchanged grade
        int newIdx = mgr.PendingIndex(openSubject);
        int delta = oldIdx - newIdx;                       // positive = improved

        float wait = 0f;
        if (delta != 0 && openCard != null)
            wait = openCard.PlayGradeChange(delta, mgr.LetterFor(oldIdx), mgr.LetterFor(newIdx));   // starts on the same frame, so no flash

        mgr.ApplyQueued();                                 // the manager now holds the new grades, but the card is holding the old letter

        if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
        mgr.ResolvePendingFail();                          // the banner, if a subject reached F
    }
    
    private ClassroomClickable ClickableUnderCursor()
    {
        if (!MonitorInput.CursorOnScreen) return null;
        Collider2D col = classroomRoot.scene.GetPhysicsScene2D().OverlapPoint(CursorWorld());
        return col != null ? col.GetComponentInParent<ClassroomClickable>() : null;
    }
}