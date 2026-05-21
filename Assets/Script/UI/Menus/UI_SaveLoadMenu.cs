using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SaveLoadMode { Save, Load }

public class UI_SaveLoadMenu : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private SaveLoadMode mode = SaveLoadMode.Save;

    [Header("Slot Buttons (index 0 = autosave, 1..N = manual)")]
    [SerializeField] private Button[] slotButtons;
    [SerializeField] private TMP_Text[] slotLabels;

    [Header("Optional")]
    [SerializeField] private Button closeButton;

    private void OnEnable()
    {
        WireButtons();
        Refresh();
    }

    private void WireButtons()
    {
        if (slotButtons == null) return;
        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null) continue;
            int slot = i;
            slotButtons[i].onClick.RemoveAllListeners();
            slotButtons[i].onClick.AddListener(() => OnSlotClicked(slot));
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
    }

    public void SetMode(SaveLoadMode newMode)
    {
        mode = newMode;
        Refresh();
    }

    public void Refresh()
    {
        if (SaveManager.Instance == null || slotLabels == null) return;

        int count = Mathf.Min(slotLabels.Length, SaveManager.MAX_MANUAL_SLOTS + 1);
        for (int i = 0; i < count; i++)
        {
            if (slotLabels[i] == null) continue;
            var info = SaveManager.Instance.GetSlotInfo(i);
            string prefix = i == SaveManager.AUTOSAVE_SLOT ? "Autosave" : $"Slot {i}";
            if (info.exists)
            {
                slotLabels[i].text = $"{prefix}\nScene: {info.sceneName}\nMoney: {info.money}\nDay {info.date} Y{info.year} {info.hour:D2}:{info.minutes:D2}";
            }
            else
            {
                slotLabels[i].text = $"{prefix}\n(Empty)";
            }
        }
    }

    private void OnSlotClicked(int slot)
    {
        if (SaveManager.Instance == null) return;

        if (mode == SaveLoadMode.Save)
        {
            if (slot == SaveManager.AUTOSAVE_SLOT)
            {
                Debug.LogWarning("[UI_SaveLoadMenu] Autosave slot is reserved for autosave.");
                return;
            }
            bool ok = SaveManager.Instance.Save(slot);
            if (ok) Refresh();
        }
        else
        {
            SaveManager.Instance.LoadAndApply(slot, success =>
            {
                if (success) Close();
            });
        }
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
