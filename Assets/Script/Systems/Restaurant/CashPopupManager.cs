using System.Collections.Generic;
using UnityEngine;

public class CashPopupManager : MonoBehaviour
{
    public static CashPopupManager Instance { get; private set; }

    [SerializeField] private TextPopup popupPrefab;
    [SerializeField] private int initialPoolSize = 10;
    [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.55f, 0f);
    [SerializeField] private Color incomeColor = new Color(1f, 0.84f, 0.1f);
    [HideInInspector][SerializeField] private Color infoColor = new Color(1f, 0.42f, 0.2f);
    [HideInInspector][SerializeField] private float infoSizeScale = 0.85f;
    [HideInInspector][SerializeField] private float infoMoveSpeed = 0.45f;
    [HideInInspector][SerializeField] private float infoLifeTime = 1.7f;
    [HideInInspector][SerializeField] private float infoFadeDuration = 0.5f;

    private readonly List<TextPopup> popupPool = new List<TextPopup>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad(gameObject);

        for (int i = 0; i < Mathf.Max(1, initialPoolSize); i++)
        {
            CreatePopupInstance();
        }
    }

    public static void ShowSalePopup(Vector3 worldPosition, int amount)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[CashPopupManager] No Instance in scene. Add CashPopupManager to a scene object.");
            return;
        }

        Instance.CreateCashPopup(worldPosition, amount);
    }

    public static void ShowInfoPopup(Vector3 worldPosition, string message)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[CashPopupManager] No Instance in scene. Add CashPopupManager to a scene object.");
            return;
        }

        Instance.CreateInfoPopup(worldPosition, message);
    }

    public void CreateCashPopup(Vector3 worldPosition, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        TextPopup popup = GetAvailablePopup();
        if (popup == null)
        {
            Debug.LogWarning("[CashPopupManager] popupPrefab is missing or could not be created.");
            return;
        }

        popup.transform.position = worldPosition + popupOffset;
        popup.gameObject.SetActive(true);
        popup.Setup($"+${amount:N0}", incomeColor, 1f);
    }

    public void CreateInfoPopup(Vector3 worldPosition, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        TextPopup popup = GetAvailablePopup();
        if (popup == null)
        {
            Debug.LogWarning("[CashPopupManager] popupPrefab is missing or could not be created.");
            return;
        }

        popup.transform.position = worldPosition + popupOffset;
        popup.gameObject.SetActive(true);
        popup.Setup(
            message,
            infoColor,
            infoSizeScale,
            infoMoveSpeed,
            infoLifeTime,
            infoFadeDuration);
    }

    private TextPopup GetAvailablePopup()
    {
        for (int i = 0; i < popupPool.Count; i++)
        {
            TextPopup popup = popupPool[i];
            if (popup != null && !popup.gameObject.activeInHierarchy)
            {
                return popup;
            }
        }

        return CreatePopupInstance();
    }

    private TextPopup CreatePopupInstance()
    {
        if (popupPrefab == null)
        {
            return null;
        }

        TextPopup popup = Instantiate(popupPrefab, transform);
        popup.gameObject.SetActive(false);
        popupPool.Add(popup);
        return popup;
    }
}
