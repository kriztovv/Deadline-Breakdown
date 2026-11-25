using UnityEngine;
using UnityEngine.SceneManagement;

public class sceneController : MonoBehaviour
{
    private string _currentGame;   // the classroom ("monitor") or a minigame

    void Start()
    {
        Cursor.visible = false;
    }

    public void StartMinigame(string sceneName)
    {
        bool keepLoaded = _currentGame == "monitor" || _currentGame == "RoomVisual";
        if (_currentGame != null && !keepLoaded) StopMinigame();

        _currentGame = sceneName;
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    }

    public void StopMinigame()
    {
        if (_currentGame == null) return;

        SceneManager.UnloadSceneAsync(_currentGame);
        _currentGame = null;

        if (ClassroomInput.Instance != null) ClassroomInput.Instance.CloseMinigame(true);
    }

    // unload the main menu scene (called when the first pill is eaten)
    public void CloseMainMenu()
    {
        Scene menu = SceneManager.GetSceneByName("mainMenu");
        if (menu.isLoaded) SceneManager.UnloadSceneAsync(menu);
    }
}