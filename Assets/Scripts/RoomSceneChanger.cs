using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Randomly swaps the room scene while the player and items stay in the main scene.
/// The next room is loaded in the background (inactive) while the transition builds up,
/// then switched in at the transition's peak. Call ChangeRoom() to trigger one manually.
/// </summary>
public class RoomSceneChanger : MonoBehaviour
{
    [Header("Rooms")]
    [Tooltip("Scene names of all rooms. They must be added to File > Build Profiles > Scene List.")]
    [SerializeField] string[] roomScenes;

    [Header("Random timing")]
    [SerializeField] bool autoChange = true;
    [Tooltip("Seconds between room changes, picked at random from this range.")]
    [SerializeField] Vector2 intervalRange = new Vector2(30f, 90f);
    [Tooltip("Off: the timer follows game time, so slowed time slows it too.")]
    [SerializeField] bool unscaledTimer = false;

    /// <summary>Set to false to pause room changes (e.g. in your death sequence, or during a cutscene).</summary>
    public bool AllowChanges { get; set; } = true;

    /// <summary>Fired once the new room is active and the old one is gone, while the screen is still covered.
    /// Use it to reposition the player or re-seed your pill spawn points.</summary>
    public event System.Action<Scene> RoomChanged;

    public Scene CurrentRoom { get; private set; }
    public bool IsChanging { get; private set; }

    float timer;
    float missingTime;
    bool warnedMissing;

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start()
    {
        // The room may already be loaded by now...
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && IsRoom(s.name)) { CurrentRoom = s; break; }
        }
        ScheduleNext();
    }

    // ...or it may still be loading (e.g. another script starts it in Awake), so also listen for it.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsChanging) return;   // rooms we load ourselves are handled in SwapRooms
        if (IsRoom(scene.name)) CurrentRoom = scene;
    }

    void Update()
    {
        if (!autoChange || !AllowChanges || IsChanging) return;

        // Wait until a listed room scene has actually loaded; warn once if it never does.
        if (!CurrentRoom.IsValid())
        {
            missingTime += Time.unscaledDeltaTime;
            if (missingTime > 5f && !warnedMissing)
            {
                warnedMissing = true;
                Debug.LogWarning("RoomSceneChanger: still no listed room scene loaded after 5 s. " +
                                 "Check that the names in 'Room Scenes' match the loaded scene exactly.");
            }
            return;
        }

        timer -= unscaledTimer ? Time.unscaledDeltaTime : Time.deltaTime;
        if (timer <= 0f)
        {
            if (!ChangeRoom()) timer = 1f;   // couldn't start (e.g. transition busy): retry shortly
        }
    }

    /// <summary>Starts a room change now. Returns false if one can't start.</summary>
    [ContextMenu("Change Room Now")]
    public bool ChangeRoom()
    {
        if (IsChanging || roomScenes == null || roomScenes.Length < 2 || !CurrentRoom.IsValid()) return false;
        if (SceneTransition.Instance != null && SceneTransition.Instance.IsPlaying) return false;

        string next = PickNextRoom();
        if (next == null) return false;

        StartCoroutine(ChangeRoutine(next));
        return true;
    }

    IEnumerator ChangeRoutine(string nextName)
    {
        IsChanging = true;

        // 1) Start loading the next room in the background. It stays inactive until we allow it.
        AsyncOperation load = SceneManager.LoadSceneAsync(nextName, LoadSceneMode.Additive);
        load.allowSceneActivation = false;

        // 2) Play the transition. SwapRooms() runs at its peak.
        Coroutine playing = SceneTransition.Instance != null
            ? SceneTransition.Instance.Play(() => SwapRooms(load, nextName))
            : null;

        if (playing != null)
            yield return playing;
        else
            yield return SwapRooms(load, nextName);   // no transition available: swap immediately

        IsChanging = false;
        ScheduleNext();
    }

    // =====================================================================================
    //  THE SPOT: this runs at the peak of the transition, while the screen is covered.
    //  Put anything that must happen "during the cut" here (or hook RoomChanged).
    // =====================================================================================
    IEnumerator SwapRooms(AsyncOperation load, string nextName)
    {
        // Wait until the new room has finished loading. With activation blocked it stops at 0.9.
        while (load.progress < 0.9f) yield return null;

        Scene oldRoom = CurrentRoom;

        // Hide the old room right away so the two rooms never show at the same time.
        if (oldRoom.IsValid() && oldRoom.isLoaded)
            foreach (GameObject go in oldRoom.GetRootGameObjects()) go.SetActive(false);

        // Switch the new room on.
        load.allowSceneActivation = true;
        while (!load.isDone) yield return null;

        Scene newRoom = SceneManager.GetSceneByName(nextName);
        SceneManager.SetActiveScene(newRoom);   // lighting settings and new objects follow the active scene
        CurrentRoom = newRoom;

        // Remove the old room.
        if (oldRoom.IsValid() && oldRoom.isLoaded)
            yield return SceneManager.UnloadSceneAsync(oldRoom);

        RoomChanged?.Invoke(newRoom);
    }

    string PickNextRoom()
    {
        var pool = new List<string>();
        foreach (string n in roomScenes)
            if (!string.IsNullOrEmpty(n) && n != CurrentRoom.name) pool.Add(n);   // never the same room twice in a row
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }

    bool IsRoom(string sceneName)
    {
        if (roomScenes == null) return false;
        foreach (string n in roomScenes) if (n == sceneName) return true;
        return false;
    }

    void ScheduleNext() => timer = Random.Range(intervalRange.x, intervalRange.y);
}