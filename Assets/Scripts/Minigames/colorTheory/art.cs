using System.Collections.Generic;
using UnityEngine;

public class art : minigameController
{
    [SerializeField] private paintSpawner spawner;   // found automatically if it is on the same object

    protected override void MgStart()
    {
        if (spawner == null) spawner = GetComponent<paintSpawner>();
    }

    protected override void MgOnDisable()
    {
        if (spawner != null) spawner.CancelDrag();
    }

    protected override void OnLeftDown() => spawner.PointerDown(CursorPosition());

    protected override void OnLeftUp() => spawner.PointerUp(CursorPosition());

    protected override void MgUpdate() => spawner.PointerMove(CursorPosition());
}