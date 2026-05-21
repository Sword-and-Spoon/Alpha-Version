using TMPro;
using UnityEngine;

public class JournalTotalFooterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text debitTotalText;
    [SerializeField] private TMP_Text creditTotalText;
    [SerializeField] private float preferredHeight = 58f;

    public float PreferredHeight => preferredHeight;

    private void Reset()
    {
        AutoBindReferences();
    }

    private void OnValidate()
    {
        AutoBindReferences();
    }

    public void SetTotals(string debitTotal, string creditTotal)
    {
        AutoBindReferences();

        if (labelText != null)
        {
            labelText.text = "TOTAL";
        }

        if (debitTotalText != null)
        {
            debitTotalText.text = debitTotal;
        }

        if (creditTotalText != null)
        {
            creditTotalText.text = creditTotal;
        }
    }

    public void ApplyTextTemplate(TMP_Text template)
    {
        if (template == null)
        {
            return;
        }

        AutoBindReferences();
        ApplyTemplate(labelText, template);
        ApplyTemplate(debitTotalText, template);
        ApplyTemplate(creditTotalText, template);
    }

    private void AutoBindReferences()
    {
        if (labelText == null)
        {
            labelText = FindText("Text_TotalLabel");
        }

        if (debitTotalText == null)
        {
            debitTotalText = FindText("Text_TotalDebit");
        }

        if (creditTotalText == null)
        {
            creditTotalText = FindText("Text_TotalCredit");
        }
    }

    private TMP_Text FindText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private void ApplyTemplate(TMP_Text target, TMP_Text template)
    {
        if (target == null)
        {
            return;
        }

        target.font = template.font;
        target.fontSharedMaterial = template.fontSharedMaterial;
    }
}
