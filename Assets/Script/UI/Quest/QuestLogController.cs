using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class QuestLogController : MonoBehaviour
{
    public static QuestLogController Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject questLogPanel;
    [SerializeField] private Transform contentParent; // Parent ของรายการเควส (เช่น ใน ScrollView)
    [SerializeField] private GameObject questEntryPrefab; // Prefab สำหรับ 1 แถวเควส
    [SerializeField] private TMP_Text emptyText; // ข้อความ "No Active Quests"

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (questLogPanel != null) questLogPanel.SetActive(false);
    }

    private void OnEnable()
    {
        ARQuestManager.OnQuestCompleted += HandleARQuestCompleted;
        ARQuestManager.OnQuestProgressChanged += HandleARQuestProgressChanged;
        APQuestManager.OnAPQuestRepaid  += HandleAPQuestRepaid;
        SceneManager.sceneLoaded        += OnSceneLoaded;
    }

    private void OnDisable()
    {
        ARQuestManager.OnQuestCompleted -= HandleARQuestCompleted;
        ARQuestManager.OnQuestProgressChanged -= HandleARQuestProgressChanged;
        APQuestManager.OnAPQuestRepaid  -= HandleAPQuestRepaid;
        SceneManager.sceneLoaded        -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (questLogPanel != null && questLogPanel.activeSelf) RefreshList();
    }

    private void HandleARQuestCompleted(string _)
    {
        if (questLogPanel != null && questLogPanel.activeSelf) RefreshList();
    }

    private void HandleARQuestProgressChanged(ARQuestData _)
    {
        if (questLogPanel != null && questLogPanel.activeSelf) RefreshList();
    }

    private void HandleAPQuestRepaid(APQuestData _)
    {
        if (questLogPanel != null && questLogPanel.activeSelf) RefreshList();
    }

    public void Toggle()
    {
        if (questLogPanel == null) return;
        if (questLogPanel.activeSelf)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (!UI_StateManager.Instance.CanOpenQuestLog()) return;

        questLogPanel.SetActive(true);
        UI_StateManager.Instance.questLogOpen = true;
        Time.timeScale = 0f;

        RefreshList();
    }

    public void Close()
    {
        questLogPanel.SetActive(false);
        UI_StateManager.Instance.questLogOpen = false;
        Time.timeScale = 1f;
    }

    public void RefreshList()
    {
        foreach (Transform child in contentParent) Destroy(child.gameObject);

        var arQuests = ARQuestManager.Instance?.GetPendingQuests();
        var apDebts  = APQuestManager.Instance?.GetActiveDebts();

        bool hasAny = (arQuests != null && arQuests.Count > 0) || (apDebts != null && apDebts.Count > 0);
        if (emptyText != null) emptyText.gameObject.SetActive(!hasAny);
        if (!hasAny) return;

        if (questEntryPrefab == null)
        {
            Debug.LogError("[QuestLogController] questEntryPrefab not assigned in Inspector");
            return;
        }

        // AR — money NPCs owe the player
        if (arQuests != null)
        {
            foreach (var quest in arQuests)
            {
                QuestLogEntry entry = Instantiate(questEntryPrefab, contentParent).GetComponent<QuestLogEntry>();
                entry?.Setup(quest);
            }
        }

        // AP — money the player owes vendors
        if (apDebts != null)
        {
            foreach (var debt in apDebts)
            {
                QuestLogEntry entry = Instantiate(questEntryPrefab, contentParent).GetComponent<QuestLogEntry>();
                entry?.SetupAP(debt);
            }
        }
    }
}
