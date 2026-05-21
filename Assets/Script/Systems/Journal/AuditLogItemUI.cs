using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum AuditLogTone
{
    Neutral,
    Success,
    Error,
    Warning
}

public class AuditLogItemUI : MonoBehaviour
{
    [SerializeField] private Graphic background;
    [SerializeField] private TMP_Text label;

    [Header("Tone Colors")]
    [SerializeField] private Color neutralBackground = new Color(0.52f, 0.26f, 0.15f, 1f);
    [SerializeField] private Color successBackground = new Color(0.16f, 0.38f, 0.22f, 1f);
    [SerializeField] private Color errorBackground = new Color(0.55f, 0.14f, 0.12f, 1f);
    [SerializeField] private Color warningBackground = new Color(0.55f, 0.35f, 0.12f, 1f);
    [SerializeField] private Color neutralText = new Color(1f, 0.96f, 0.86f, 1f);
    [SerializeField] private Color successText = new Color(0.88f, 1f, 0.82f, 1f);
    [SerializeField] private Color errorText = new Color(1f, 0.9f, 0.82f, 1f);
    [SerializeField] private Color warningText = new Color(1f, 0.96f, 0.78f, 1f);

    public void SetMessage(string message, AuditLogTone tone)
    {
        ResolveReferences();

        if (label != null)
        {
            label.text = message;
            label.color = GetTextColor(tone);
        }

        if (background != null)
        {
            background.color = GetBackgroundColor(tone);
        }
    }

    private void ResolveReferences()
    {
        if (background == null)
        {
            background = GetComponent<Graphic>();
        }

        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private Color GetBackgroundColor(AuditLogTone tone)
    {
        switch (tone)
        {
            case AuditLogTone.Success:
                return successBackground;
            case AuditLogTone.Error:
                return errorBackground;
            case AuditLogTone.Warning:
                return warningBackground;
            default:
                return neutralBackground;
        }
    }

    private Color GetTextColor(AuditLogTone tone)
    {
        switch (tone)
        {
            case AuditLogTone.Success:
                return successText;
            case AuditLogTone.Error:
                return errorText;
            case AuditLogTone.Warning:
                return warningText;
            default:
                return neutralText;
        }
    }
}
