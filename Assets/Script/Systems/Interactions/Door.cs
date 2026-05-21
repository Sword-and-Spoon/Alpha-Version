using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : InteractableObject
{
    public override bool CanInteract() => true;

    public override void Interact()
    {
        return;
    }
}
