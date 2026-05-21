using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System;
using System.Collections;

public class PointerController : MonoBehaviour
{
    [SerializeField] private InputActionReference pressSpace;
    public RectTransform pointA;
    public RectTransform pointB;

    [Header("Pointer")]
    public RectTransform pointerRect;
    public RectTransform safeZone1;
    public RectTransform safeZone2;
    public RectTransform safeZone3;

    // Use a high speed for World Position UI movement (e.g., 100-500)
    public float moveSpeed = 400f;

    private GameObject durationIndicator;

    [Header("Quick Time UI")]
    public GameObject quickTimeContainer;
    public TMP_Text feedbackText;
    [SerializeField] private float quickTimeCloseDelay = 1f;
    public float QuickTimeCloseDelay => quickTimeCloseDelay;

    private RectTransform pointerTransform;
    private float direction = 1f; // 1 towards B, -1 towards A
    private bool pointerRunning;
    private Action<int> quickTimeCallback;

    // remember unclamped widths so we can recalc each round
    private Vector2 baseZone1Size;
    private Vector2 baseZone2Size;
    private Vector2 baseZone3Size;

    private void Awake()
    {
        pointerTransform = pointerRect != null ? pointerRect : GetComponent<RectTransform>();
        durationIndicator = transform.Find("DurationIndicator").gameObject;

        // capture base sizes for safe zones
        if (safeZone1 != null) baseZone1Size = safeZone1.sizeDelta;
        if (safeZone2 != null) baseZone2Size = safeZone2.sizeDelta;
        if (safeZone3 != null) baseZone3Size = safeZone3.sizeDelta;
    }

    void Start()
    {
        if (quickTimeContainer != null)
            quickTimeContainer.SetActive(false);
    }

    public void BeginQuickTime(Action<int> onComplete)
    {
        Time.timeScale = 0f;
        quickTimeCallback = onComplete;

        if (quickTimeContainer != null)
        {
            quickTimeContainer.SetActive(true);
            // Forces UI to calculate positions before we read them
            Canvas.ForceUpdateCanvases();
        }

        if (pointerTransform != null && pointA != null && pointB != null)
        {
            // place pointer at A and start moving toward B
            pointerTransform.position = pointA.position;
            direction = 1f;
        }

        RandomizeZones();
        pointerRunning = true;

        pressSpace.action.Enable();
        pressSpace.action.performed += OnSpacePressed; // uses world-based success checks

        if (feedbackText != null) feedbackText.text = "";
    }

    // Using LateUpdate to ensure UI positions are settled before moving
    void LateUpdate()
    {
        if (!pointerRunning || pointerTransform == null) return;

        // advance pointer toward current destination based on direction
        Vector3 dest = (direction > 0f) ? pointB.position : pointA.position;
        pointerTransform.position = Vector3.MoveTowards(
            pointerTransform.position,
            dest,
            moveSpeed * Time.unscaledDeltaTime
        );

        // reverse direction when we hit either point
        if (pointA != null && Vector3.Distance(pointerTransform.position, pointA.position) < 0.1f)
        {
            direction = 1f;
        }
        else if (pointB != null && Vector3.Distance(pointerTransform.position, pointB.position) < 0.1f)
        {
            direction = -1f;
        }
    }

    private void OnSpacePressed(InputAction.CallbackContext context)
    {
        CheckSuccess();
    }

    public void CheckSuccess()
    {
        if (!pointerRunning) return;

        int zone = 0;
        // Check zones in order of priority (Green -> Orange -> Red)
        if (IsInZone(pointerTransform, safeZone1)) zone = 1;
        else if (IsInZone(pointerTransform, safeZone2)) zone = 2;
        else if (IsInZone(pointerTransform, safeZone3)) zone = 3;

        string feedback = zone == 1 ? "Best!" : zone == 2 ? "Good" : zone == 3 ? "Poor" : "Miss";
        Color color = zone == 1 ? Color.green : zone == 2 ? new Color(1f, 0.6f, 0f) : zone == 3 ? Color.red : Color.gray;

        if (feedbackText != null) StartCoroutine(ShowFeedbackRoutine(feedback, color));

        pointerRunning = false;
        pressSpace.action.performed -= OnSpacePressed;

        // Return result to CookingSession
        quickTimeCallback?.Invoke(zone);

        if (quickTimeContainer != null) StartCoroutine(HideUI());
    }

    private bool IsInZone(RectTransform ptr, RectTransform zone)
    {
        if (zone == null || ptr == null) return false;
        // Convert world position to screen point for accurate UI collision
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, ptr.position);
        return RectTransformUtility.RectangleContainsScreenPoint(zone, screenPoint, null);
    }

    public void RandomizeZones()
    {
        if (quickTimeContainer == null) return;
        RectTransform parent = quickTimeContainer.GetComponent<RectTransform>();
        if (parent == null) return;

        // compute total width of all three zones
        float totalWidth = 0f;
        if (safeZone1 != null) totalWidth += safeZone1.rect.width;
        if (safeZone2 != null) totalWidth += safeZone2.rect.width;
        if (safeZone3 != null) totalWidth += safeZone3.rect.width;

        // if the zones are wider than the parent we can't move them; bail out
        float available = parent.rect.width - totalWidth;
        float offset = 0f;
        if (available > 0f)
        {
            // choose offset so that the group remains inside the bar
            float range = available * 0.5f;
            offset = UnityEngine.Random.Range(-range, range);
        }

        void Shift(RectTransform zone)
        {
            if (zone == null) return;
            Vector2 ap = zone.anchoredPosition;
            ap.x += offset;
            // clamp each zone inside parent bounds just in case
            float half = zone.rect.width * 0.5f;
            float minX = -parent.rect.width * 0.5f + half;
            float maxX = parent.rect.width * 0.5f - half;
            ap.x = Mathf.Clamp(ap.x, minX, maxX);
            zone.anchoredPosition = ap;
        }

        Shift(safeZone1);
        Shift(safeZone2);
        Shift(safeZone3);
    }

    private IEnumerator ShowFeedbackRoutine(string text, Color color)
    {
        if (feedbackText == null) yield break;
        feedbackText.text = text;
        feedbackText.color = color;
        yield return new WaitForSecondsRealtime(1f);
        feedbackText.text = "";
    }

    private IEnumerator HideUI()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, quickTimeCloseDelay));
        quickTimeContainer.SetActive(false);
        durationIndicator.SetActive(true);
        Time.timeScale = 1f;
    }

    public void ConfigureZonesForDifficulty(float factor)
    {
        factor = Mathf.Clamp01(factor);
        // apply shrink based on original captured size, not current
        if (safeZone1 != null) safeZone1.sizeDelta = new Vector2(baseZone1Size.x * (1 - factor), baseZone1Size.y);
        if (safeZone2 != null) safeZone2.sizeDelta = new Vector2(baseZone2Size.x * (1 - factor), baseZone2Size.y);
        if (safeZone3 != null) safeZone3.sizeDelta = new Vector2(baseZone3Size.x * (1 - factor), baseZone3Size.y);
    }
}