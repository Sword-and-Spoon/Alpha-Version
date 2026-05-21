using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class JournalGuideButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private GameObject guideBookPrefab;

    private JournalGuideBookUI guideBookInstance;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.RemoveListener(OpenGuideBook);
            button.onClick.AddListener(OpenGuideBook);
        }
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OpenGuideBook);
        }
    }

    private void OpenGuideBook()
    {
        EnsureGuideBookInstance();

        if (guideBookInstance != null)
        {
            guideBookInstance.Open();
        }
    }

    private void EnsureGuideBookInstance()
    {
        if (guideBookInstance != null)
        {
            return;
        }

        GameObject prefab = guideBookPrefab != null
            ? guideBookPrefab
            : Resources.Load<GameObject>("UI/JournalGuideBook");

        if (prefab == null)
        {
            Debug.LogError("[JournalGuideButton] Missing JournalGuideBook prefab. Assign it on GuideButton or place it at Resources/UI/JournalGuideBook.prefab.");
            return;
        }

        Canvas targetCanvas = GetComponentInParent<Canvas>();
        Transform parent = targetCanvas != null ? targetCanvas.transform : transform.root;
        GameObject guideObject = Instantiate(prefab, parent, false);
        guideObject.name = "JournalGuideBook";
        guideObject.transform.SetAsLastSibling();

        guideBookInstance = guideObject.GetComponent<JournalGuideBookUI>();
        if (guideBookInstance == null)
        {
            Debug.LogError("[JournalGuideButton] JournalGuideBook prefab is missing JournalGuideBookUI.");
        }
    }
}
