using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class JournalCanvasToggle : MonoBehaviour
{
    public Button closeButton;
    [HideInInspector][SerializeField] private JournalTable ownerInteractable;

    public void BindOwner(JournalTable owner)
    {
        ownerInteractable = owner;
    }

    public void Awake()
    {
        if (closeButton == null)
        {
            ResolveReference();
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(() => ownerInteractable.Interact());
            closeButton.onClick.AddListener(() => ownerInteractable.Interact());
        }
    }

    private void ResolveReference()
    {
        closeButton = transform.Find("Button_Close").GetComponent<Button>();
    }
}
