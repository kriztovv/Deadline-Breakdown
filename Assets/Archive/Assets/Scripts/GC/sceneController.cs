using UnityEngine;
using UnityEngine.SceneManagement;

public class sceneController : MonoBehaviour
{
    private string _currentGame;
    private bool _tutorial;
    
    void Start()
    {
        Cursor.visible = false;
    }

    public void StartMinigame(string sceneName)
    {
        if (_currentGame != null && _currentGame != "monitor") StopMinigame();
        _currentGame = sceneName;
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
    }

    public void StopMinigame()
    {
        if (_currentGame == null) return;
        
        SceneManager.UnloadSceneAsync(_currentGame);
        _currentGame = null;
        
        if (!_tutorial) _tutorial = true; 
        else ClassroomInput.Instance.CloseMinigame(true);
    }
}