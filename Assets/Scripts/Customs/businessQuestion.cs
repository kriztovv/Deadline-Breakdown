using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBusinessQuestion", menuName = "Overdose/Business Question")]
public class businessQuestion : ScriptableObject
{
    [TextArea(2, 4)] public string questionText;
    public businessAnswer[] answers = new businessAnswer[4];   // top, middle, bottom, default (no keys held)

    private void OnValidate()
    {
        if (answers == null || answers.Length != 4) Array.Resize(ref answers, 4);   // always exactly four
    }
}

[Serializable]
public struct businessAnswer
{
    [TextArea(1, 3)] public string text;
    public bool isCorrect;   // several answers can be correct
}