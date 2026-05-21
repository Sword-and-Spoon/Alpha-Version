using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UI_StateManager : MonoBehaviour
{
    public static UI_StateManager Instance;

    public bool menuOpen;
    public bool interactWindowOpen; 
    public bool journalOpen;
    public bool questLogOpen; // เพิ่มสถานะหน้าต่างเควส

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool CanOpenMenu()
    {
        return !interactWindowOpen && !journalOpen && !questLogOpen;
    }

    public bool CanOpenJournal()
    {
        return !interactWindowOpen && !menuOpen && !questLogOpen;
    }

    public bool CanOpenQuestLog()
    {
        return !interactWindowOpen && !menuOpen && !journalOpen;
    }

    public bool CanOpenInteractWindow()
    {
        return !menuOpen && !journalOpen && !questLogOpen;
    }
}
