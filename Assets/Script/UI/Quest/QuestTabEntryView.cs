using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestTabEntryView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image npcPortraitImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text dueDateText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject selectionArrow;

    private Action onClick;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

    public void Bind(string title, string description, string dueText, Color dueColor, Sprite portrait, Action onPressed)
    {
        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description;
        if (dueDateText != null)
        {
            dueDateText.text = dueText;
            dueDateText.color = dueColor;
        }
        if (npcPortraitImage != null)
        {
            npcPortraitImage.sprite = portrait;
            npcPortraitImage.gameObject.SetActive(true);
        }
        if (selectionArrow != null)
            selectionArrow.SetActive(false);
        onClick = onPressed;
    }

    public void SetSelected(bool selected, Color selectedColor, Color normalColor)
    {
        if (backgroundImage != null)
            backgroundImage.color = selected ? selectedColor : normalColor;
        if (selectionArrow != null)
            selectionArrow.SetActive(selected);
    }

    private void HandleClick() => onClick?.Invoke();
}
