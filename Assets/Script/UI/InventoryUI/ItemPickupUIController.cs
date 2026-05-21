using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemPickupUIController : MonoBehaviour
{
    public static ItemPickupUIController Instance { get; private set; }

    public GameObject popupPrefab;
    public int maxPopups = 5;
    public float popupDuration = 3f;

    private readonly Queue<GameObject> activePopups = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ShowItemPickup(Item item)
    {
        GameObject newPopup = Instantiate(popupPrefab, transform);
        newPopup.transform.Find("Text_Name").GetComponent<TMP_Text>().text = item.GetName();
        newPopup.transform.Find("Text_Amount").GetComponent<TMP_Text>().text = item.amount.ToString();

        newPopup.transform.Find("QualityIndicator").GetComponent<Image>().color = item.GetQualityColor();

        Image itemImage = newPopup.transform.Find("Image")?.GetComponent<Image>();
        if (itemImage != null)
        {
            itemImage.sprite = item.GetSprite();
        }

        activePopups.Enqueue(newPopup);
        if (activePopups.Count > maxPopups)
        {
            Destroy(activePopups.Dequeue());
        }

        StartCoroutine(FadeOutAndDestroy(newPopup));
    }

    private IEnumerator FadeOutAndDestroy(GameObject popup)
    {
        yield return new WaitForSeconds(popupDuration);
        if (popup == null) yield break;

        CanvasGroup canvasGroup = popup.GetComponent<CanvasGroup>();
        for (float timePassed = 0f; timePassed < 1f; timePassed += Time.deltaTime)
        {
            if (popup == null) yield break;
            canvasGroup.alpha = 1f - timePassed;
            yield return null;
        }

        Destroy(popup);
    }
}
