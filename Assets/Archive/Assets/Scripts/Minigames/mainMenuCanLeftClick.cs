using System.Collections;
using TMPro;
using UnityEngine;

public class mainMenuCanLeftClick : MonoBehaviour
{
    [SerializeField] private GameObject menu;
    [SerializeField] private GameObject tutText;
    [SerializeField] private GameObject playHitbox;
    private sceneController _sceneController;
    [SerializeField] private GameObject credits;

    private int _tutState = 0;
    //freaking honest
    
    private void Start()
    {
        _sceneController = GameObject.Find("controller").GetComponent<sceneController>();
    }
    
    public void LeftClick()
    {
        Debug.Log("Left clicked");
        switch (gameObject.name)
        {
            case "play":
                TutAdvance();
                playHitbox.layer = LayerMask.NameToLayer("Default");
                DeskMonitorSeat.firstBoot = false;
                break;
            case "credits":
                credits.SetActive(true);
                playHitbox.SetActive(false);
                menu.SetActive(false);
                break;
            case "creditsClose":
                menu.SetActive(true);
                playHitbox.SetActive(true);
                gameObject.SetActive(false);
                break;
            case "quit":
                Application.Quit();
                break;
        }
    }

    public void TutAdvance()
    {
        switch (_tutState)
        {
            case 0:
                _tutState = 1;
                menu.SetActive(false);
                tutText.SetActive(true);
                StartCoroutine(Wait());
                break;
            case 1:
                _tutState = 2;
                StopCoroutine(Wait());
                _sceneController.StartMinigame("monitor");
                if (AssignmentManager.Instance != null) AssignmentManager.Instance.Begin();
                if (GameStats.Instance != null) GameStats.Instance.Begin();
                if (UpgradeManager.Instance != null) UpgradeManager.Instance.Begin();
                break;
        }
    }

    private IEnumerator Wait()
    {
        yield return new WaitForSeconds(2.5f);
        TextMeshPro text = tutText.transform.Find("text").GetComponent<TextMeshPro>();
        text.text = "You likely won't be able to send in your assignments in time at your normal pace...";
        yield return new WaitForSeconds(5f);
        text.text = "Maybe you could somehow <color=red><i>overclock</i></color>?";
        yield return new WaitForSeconds(4f);
        text.text = "Go find some <color=red>pills<color=white> around your room. They could help you, but be careful.";
    }
}