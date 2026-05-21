using System.Collections.Generic;
using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance { get; private set; }

    [SerializeField] private DamagePopup popupPrefab;
    [SerializeField] private int initialPoolSize = 20;

    [Header("Styles")]
    public Color normalColor = Color.white;
    public Color criticalColor = new Color(1f, 0.8f, 0f); // เหลืองส้ม
    public Color playerHitColor = Color.red;
    public Color healColor = Color.green;
    public Color missColor = Color.gray;

    private List<DamagePopup> popupPool = new List<DamagePopup>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize Pool
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewPopupInstance();
        }
    }

    private void CreateNewPopupInstance()
    {
        DamagePopup popup = Instantiate(popupPrefab, transform);
        popup.gameObject.SetActive(false);
        popupPool.Add(popup);
    }

    public void CreatePopup(Vector3 position, string text, DamageType type)
    {
        DamagePopup popup = GetAvailablePopup();
        if (popup == null) return;

        Color targetColor = normalColor;
        float scale = 1f;

        switch (type)
        {
            case DamageType.Normal:
                targetColor = normalColor;
                break;
            case DamageType.Critical:
                targetColor = criticalColor;
                text = text + "!";
                break;
            case DamageType.PlayerHit:
                targetColor = playerHitColor;
                text = "- " + text + " HP";
                break;
            case DamageType.Heal:
                targetColor = healColor;
                text = "+ " + text;
                break;
            case DamageType.Miss:
                targetColor = missColor;
                text = "MISS";
                break;
        }

        popup.transform.position = position + new Vector3(0, 0.5f, 0);
        popup.gameObject.SetActive(true);
        popup.Setup(text, targetColor, scale);
    }

    private DamagePopup GetAvailablePopup()
    {
        foreach (var popup in popupPool)
        {
            if (!popup.gameObject.activeInHierarchy) return popup;
        }

        // ถ้า Pool เต็ม ให้ขยายเพิ่ม
        DamagePopup newPopup = Instantiate(popupPrefab, transform);
        popupPool.Add(newPopup);
        return newPopup;
    }
}
