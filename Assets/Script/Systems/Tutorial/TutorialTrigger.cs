using UnityEngine;

public class TutorialTrigger : MonoBehaviour
{
    [SerializeField] private TutorialTriggerType triggerType;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private string requiredTag = "Player";

    public TutorialTriggerType TriggerType => triggerType;

    private bool hasTriggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if (!other.CompareTag(requiredTag))
        {
            return;
        }

        TriggerTutorial();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        if (!other.CompareTag(requiredTag))
        {
            return;
        }

        TriggerTutorial();
    }

    private void TriggerTutorial()
    {
        if (hasTriggered && triggerOnce)
        {
            Debug.Log($"[TutorialTrigger] {name} already triggered, skipping.");
            return;
        }

        if (TutorialManager.Instance == null)
        {
            Debug.LogWarning($"[TutorialTrigger] {name} fired but TutorialManager.Instance is null!");
            return;
        }

        Debug.Log($"[TutorialTrigger] {name} firing triggerType={triggerType}, currentStep={TutorialManager.Instance.CurrentStep}");
        bool consumed = TutorialManager.Instance.HandleWorldTrigger(triggerType);
        if (!consumed)
        {
            return;
        }

        hasTriggered = true;
        if (!triggerOnce)
        {
            return;
        }

        Collider2D col2D = GetComponent<Collider2D>();
        if (col2D != null)
        {
            col2D.enabled = false;
        }

        Collider col3D = GetComponent<Collider>();
        if (col3D != null)
        {
            col3D.enabled = false;
        }
    }
}
