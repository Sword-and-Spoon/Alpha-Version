using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnterSceneInteract : InteractableObject
{
    public override bool CanInteract() => true;

    public override void Interact()
    {
        LoadAnotherScene loadScene = gameObject.GetComponent<LoadAnotherScene>();
        if (loadScene != null)
        {
            loadScene.LoadSceneManually();
        }
        else
        {
            Debug.LogWarning("LoadAnotherScene component not found on " + gameObject.name);
        }
    }
}
