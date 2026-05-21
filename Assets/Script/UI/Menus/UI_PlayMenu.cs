using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panel "Pick a save slot":
// - Slot rows appear only if a save exists; hidden rows take no space (VerticalLayoutGroup).
// - "New Game" row is always visible at the bottom.
public class UI_PlayMenu : MonoBehaviour
{
    [Header("Slot Rows (index 0 = slot1, 1 = slot2, 2 = slot3)")]
    [Tooltip("Root GameObject of each slot row — will be shown/hidden")]
    [SerializeField] private GameObject[] slotRows;
    [SerializeField] private TMP_Text[] slotLabels;
    [SerializeField] private Button[] slotButtons;

    [Header("New Game Row")]
    [SerializeField] private Button newGameButton;

    [Header("Close Button")]
    [SerializeField] private Button closeButton;

    [Header("References")]
    [SerializeField] private UI_MainMenu mainMenu;

    private void OnEnable()
    {
        WireButtons();
        Refresh();
    }

    private void WireButtons()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null) continue;
            int slot = i + 1;
            slotButtons[i].onClick.RemoveAllListeners();
            slotButtons[i].onClick.AddListener(() => LoadSlot(slot));
        }

        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveAllListeners();
            newGameButton.onClick.AddListener(OnNewGame);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    public void Refresh()
    {
        if (SaveManager.Instance == null) return;

        for (int i = 0; i < slotRows.Length; i++)
        {
            if (slotRows[i] == null) continue;
            int slot = i + 1;
            var info = SaveManager.Instance.GetSlotInfo(slot);

            // Show row only when save exists — VerticalLayoutGroup ignores inactive children
            slotRows[i].SetActive(info.exists);

            if (info.exists && slotLabels != null && i < slotLabels.Length && slotLabels[i] != null)
            {
                slotLabels[i].text =
                    $"<b>Slot {slot}</b>\n" +
                    $"Day {info.date}  Year {info.year}  {info.hour:D2}:{info.minutes:D2}" +
                    $"     ${info.money}";
            }
        }
    }

    private void LoadSlot(int slot)
    {
        if (SaveManager.Instance == null) return;
        SaveManager.Instance.LoadAndApply(slot, success => { if (success) Close(); });
    }

    private void OnNewGame()
    {
        if (mainMenu != null) mainMenu.StartNewGame();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        if (mainMenu != null) mainMenu.OnPlayPanelClosed();
    }
}
