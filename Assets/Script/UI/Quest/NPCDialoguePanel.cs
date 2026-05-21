using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Panel สำหรับแสดง NPC dialogue — รองรับทั้งแบบ OK อย่างเดียว และแบบมีตัวเลือก Accept/Decline
/// </summary>
public class NPCDialoguePanel : MonoBehaviour
{
    private static NPCDialoguePanel _instance;
    public static int LastEscapeCloseFrame { get; private set; } = -1;
    private int _openedFrame = -1;

    public static NPCDialoguePanel Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<NPCDialoguePanel>(true);
            return _instance;
        }
    }

    [Header("UI References")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text messageText;

    [Header("Button Groups")]
    [SerializeField] private GameObject okGroup;
    [SerializeField] private GameObject choiceGroup;

    [Header("Button Labels")]
    [SerializeField] private TMP_Text okButtonText;
    [SerializeField] private TMP_Text acceptButtonText;
    [SerializeField] private TMP_Text declineButtonText;

    private Action onOKCallback;
    private Action onAcceptCallback;
    private Action onDeclineCallback;

    private void Awake()
    {
        _instance = this;

        BindIfMissingHandler(okGroup?.GetComponentInChildren<Button>(true), OnOKClicked, nameof(OnOKClicked));

        if (choiceGroup != null)
        {
            var btns = choiceGroup.GetComponentsInChildren<Button>(true);
            if (btns.Length >= 1) BindIfMissingHandler(btns[0], OnAcceptClicked, nameof(OnAcceptClicked));
            if (btns.Length >= 2) BindIfMissingHandler(btns[1], OnDeclineClicked, nameof(OnDeclineClicked));
        }
    }

    private static void BindIfMissingHandler(Button button, UnityEngine.Events.UnityAction handler, string handlerName)
    {
        if (button == null || handler == null || string.IsNullOrEmpty(handlerName)) return;

        int persistentCount = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < persistentCount; i++)
        {
            if (button.onClick.GetPersistentMethodName(i) == handlerName)
                return;
        }

        button.onClick.AddListener(handler);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        // ป้องกันการปิดในเฟรมเดียวกับที่เปิด (Input เดียวกัน)
        if (Time.frameCount == _openedFrame) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            LastEscapeCloseFrame = Time.frameCount;
            ForceClose();
        }
        else if (Keyboard.current.fKey.wasPressedThisFrame && okGroup != null && okGroup.activeSelf)
        {
            OnOKClicked();
        }
    }

    public void ForceClose()
    {
        var cb = onDeclineCallback ?? onOKCallback;

        gameObject.SetActive(false);
        ClearCallbacks();

        if (cb != null) cb.Invoke();

        TryRecoverInteractionLock();
    }

    public void ShowDialogue(Sprite portrait, string npcName, string message, Action onOK = null, string okLabel = null)
    {
        SetupContent(portrait, npcName, message);
        okGroup.SetActive(true);
        choiceGroup.SetActive(false);
        onOKCallback = onOK;
        onAcceptCallback = null;
        onDeclineCallback = null;
        if (okButtonText != null) okButtonText.text = string.IsNullOrEmpty(okLabel) ? "OK" : okLabel;
        Show();
    }

    public void ShowChoice(Sprite portrait, string npcName, string message, Action onAccept, Action onDecline,
                           string acceptLabel = null, string declineLabel = null)
    {
        SetupContent(portrait, npcName, message);
        okGroup.SetActive(false);
        choiceGroup.SetActive(true);
        onOKCallback = null;
        onAcceptCallback = onAccept;
        onDeclineCallback = onDecline;
        if (acceptButtonText != null) acceptButtonText.text = string.IsNullOrEmpty(acceptLabel) ? "OK" : acceptLabel;
        if (declineButtonText != null) declineButtonText.text = string.IsNullOrEmpty(declineLabel) ? "NO" : declineLabel;
        Show();
    }

    private void Show()
    {
        _openedFrame = Time.frameCount;

        // Ensure parent Canvas is active and has correct scale
        Canvas parentCanvas = GetComponentInParent<Canvas>(true);
        if (parentCanvas != null)
        {
            parentCanvas.gameObject.SetActive(true);
            parentCanvas.transform.localScale = Vector3.one;
        }

        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
        ClearCallbacks();
        TryRecoverInteractionLock();
    }

    public void OnOKClicked()
    {
        if (okGroup == null || !okGroup.activeSelf) return;

        gameObject.SetActive(false);
        var cb = onOKCallback;
        ClearCallbacks();
        cb?.Invoke();

        TryRecoverInteractionLock();
    }

    public void OnAcceptClicked()
    {
        if (choiceGroup == null || !choiceGroup.activeSelf) return;

        var cb = onAcceptCallback;
        ClearCallbacks();
        cb?.Invoke();
    }

    public void OnDeclineClicked()
    {
        if (choiceGroup == null || !choiceGroup.activeSelf) return;

        gameObject.SetActive(false);
        var cb = onDeclineCallback;
        ClearCallbacks();
        cb?.Invoke();

        TryRecoverInteractionLock();
    }

    private void SetupContent(Sprite portrait, string npcName, string message)
    {
        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.gameObject.SetActive(portrait != null);
        }
        if (npcNameText != null) npcNameText.text = npcName ?? "";
        if (messageText != null) messageText.text = message ?? "";
    }

    private void ClearCallbacks()
    {
        onOKCallback = null;
        onAcceptCallback = null;
        onDeclineCallback = null;
    }

    private void TryRecoverInteractionLock()
    {
        var uiState = UI_StateManager.Instance;
        if (uiState == null) return;

        // ถ้าหน้าต่างปิดอยู่ แต่สถานะยังค้าง ให้ Reset
        if (!gameObject.activeSelf)
        {
            if (uiState.interactWindowOpen || Mathf.Approximately(Time.timeScale, 0f))
            {
                uiState.interactWindowOpen = false;
                Time.timeScale = 1f;
            }
        }
    }
}
