using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JournalTable : InteractableObject
{
    public static event Action OnJournalTableOpened;

    [SerializeField] private GameObject journalUI;

    public override bool CanInteract() => true;

    public override void Interact()
    {
        if (journalUI == null) ResolveUI();

        if (journalUI == null)
        {
            Debug.LogError($"[JournalTable] Cannot find Journal UI (JournalCanvas) in the scene! Please assign it in the inspector on {name}.");
            return;
        }

        journalUI.GetComponent<JournalCanvasToggle>().BindOwner(this);

        bool wasOpen = journalUI.activeSelf;
        openUI(journalUI);

        bool isOpen = journalUI.activeSelf;
        if (!wasOpen && isOpen)
        {
            OnJournalTableOpened?.Invoke();
        }
    }

    private void ResolveUI()
    {
        if (journalUI != null) return;

        // 1. ลองหาจาก JournalManager (หาทั้งที่ active และ inactive)
        JournalManager jm = FindObjectOfType<JournalManager>(true);
        if (jm != null)
        {
            Canvas c = jm.GetComponentInParent<Canvas>(true);
            if (c != null)
            {
                journalUI = c.gameObject;
                Debug.Log($"[JournalTable] Auto-resolved journalUI via JournalManager: {journalUI.name}");
                return;
            }
        }

        // 2. ลองหาจากชื่อ "JournalCanvas" (หาทั้งที่ active และ inactive)
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            // เช็คชื่อ และเช็คว่าเป็น object ใน scene (ไม่ใช่ใน Assets/Project)
            if (obj.name == "JournalCanvas" && obj.hideFlags == HideFlags.None)
            {
                journalUI = obj;
                Debug.Log($"[JournalTable] Auto-resolved journalUI via name search: {journalUI.name}");
                return;
            }
        }
    }
}
