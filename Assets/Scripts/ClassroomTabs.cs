using UnityEngine;

public class ClassroomTabs : MonoBehaviour
{
    [SerializeField] private GameObject classroomPage;
    [SerializeField] private GameObject upgradesPage;

    private void Start() => ShowClassroom();

    public void ShowClassroom() { classroomPage.SetActive(true);  upgradesPage.SetActive(false); }
    public void ShowUpgrades()  { classroomPage.SetActive(false); upgradesPage.SetActive(true); }
}
