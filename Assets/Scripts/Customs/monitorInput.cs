using System;
using UnityEngine;

public static class MonitorInput
{
    public static Vector2 Movement;
    public static Vector2 CursorUV;
    public static bool CursorOnScreen;

    public static event Action<MonitorInteraction> OnInteraction;
    public static void FireInput(MonitorInteraction interaction) => OnInteraction?.Invoke(interaction);

    public static event Action<KeyCode> OnKeyDown;
    public static void FireKey(KeyCode key) => OnKeyDown?.Invoke(key);

    public static void Clear()
    {
        CursorUV = default;
        Movement = default;
        CursorOnScreen = false;
    }
}

public enum MonitorInteraction
{
    PrimaryDown,
    PrimaryUp,
    SecondaryDown,
    SecondaryUp
}