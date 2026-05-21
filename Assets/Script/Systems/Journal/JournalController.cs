using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JournalController : MonoBehaviour
{
    [SerializeField] private GameObject journalCanvas;

    public void Toggle()
    {
        if (!UI_StateManager.Instance.CanOpenJournal()) return;

        bool open = !UI_StateManager.Instance.journalOpen;
        journalCanvas.SetActive(open);
        Time.timeScale = open ? 0f : 1f;
        UI_StateManager.Instance.journalOpen = open;
    }
}
