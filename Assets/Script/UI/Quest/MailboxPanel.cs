using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Panel สำหรับแสดงจดหมายจาก Mailbox
/// </summary>
public class MailboxPanel : MonoBehaviour
{
    private static MailboxPanel _instance;
    private int _openedFrame = -1;

    public static MailboxPanel Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<MailboxPanel>(true);
            return _instance;
        }
    }

    [Header("UI References")]
    [SerializeField] private TMP_Text senderText;
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private Button closeButton;

    private Action onCloseCallback;

    private void Awake()
    {
        _instance = this;
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        if (Keyboard.current == null || !gameObject.activeSelf) return;
        if (Time.frameCount == _openedFrame) return;

        // กด F หรือ ESC เพื่อปิดจดหมาย
        if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame)
        {
            Close();
        }
    }

    public void ShowLetter(string sender, string content, Action onClose = null)
    {
        if (senderText != null) senderText.text = sender;
        if (contentText != null) contentText.text = content;

        onCloseCallback = onClose;
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

        var cb = onCloseCallback;
        onCloseCallback = null;
        cb?.Invoke();

        TryRecoverInteractionLock();
    }

    private void TryRecoverInteractionLock()
    {
        var uiState = UI_StateManager.Instance;
        if (uiState == null) return;

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
