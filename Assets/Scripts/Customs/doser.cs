using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Doser", menuName = "Customs/Doser")]
public class Doser : ScriptableObject
{
    public int value;
    public int delay;
    public int duration;
    public List<float> curve;
    [System.NonSerialized] public Queue<float> CurveQueue;
    public float uncertainty;
    public List<string> special;
    public Sprite icon;
    
    public void SetQueue()
    {
        CurveQueue = new Queue<float>();
        foreach (float curvePart in curve) CurveQueue.Enqueue(curvePart);
    }
    
    public string doserName;
    public string description;
}