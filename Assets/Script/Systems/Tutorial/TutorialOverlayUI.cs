using TMPro;
using UnityEngine;

public class TutorialOverlayUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private RectTransform arrowRect;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private Camera worldCamera;

    [Header("Orbit (ไกล target)")]
    [Tooltip("offset คงที่จาก player (ไม่กระทบ bob mode) — ปรับตรงนี้เพื่อเลื่อน panel ออกจาก sprite")]
    [SerializeField] private Vector2 playerAnchorOffset = new Vector2(40f, 30f);
    [Tooltip("ระยะห่างจาก anchor point ไปหา target (canvas units)")]
    [SerializeField] private float orbitRadius = 120f;
    [Tooltip("ความเร็วตาม/สมูท")]
    [SerializeField] private float followSpeed = 14f;
    [Tooltip("ตำแหน่ง arrow ภายใน panel (orbit mode)")]
    [SerializeField] private Vector2 arrowOrbitLocalPos = new Vector2(60f, 0f);

    [Header("Bob (ใกล้ target)")]
    [Tooltip("ระยะ world units ที่ถือว่า 'ใกล้ถึง'")]
    [SerializeField] private float nearDistance = 3f;
    [Tooltip("canvas units เหนือ target")]
    [SerializeField] private float bobHeight = 80f;
    [Tooltip("ขยับขึ้นลง (canvas units)")]
    [SerializeField] private float bobAmplitude = 12f;
    [Tooltip("ความเร็วขยับขึ้นลง")]
    [SerializeField] private float bobSpeed = 3f;
    [Tooltip("ตำแหน่ง arrow ภายใน panel (bob mode) — ลบ Y = ใต้ข้อความ")]
    [SerializeField] private Vector2 arrowBobLocalPos = new Vector2(0f, -55f);

    private Transform currentTarget;
    private bool hasMessage;
    private bool rootVisible;
    private Canvas parentCanvas;
    private Transform playerTransform;
    private RectTransform rootRect;
    private bool initialized;

    private void Awake()
    {
        if (root != null)
        {
            root.SetActive(false);
            rootRect = root.GetComponent<RectTransform>();
        }

        parentCanvas = canvasRect != null
            ? canvasRect.GetComponentInParent<Canvas>()
            : GetComponentInParent<Canvas>();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null) { playerTransform = playerObj.transform; return; }
        PlayerMovement pm = FindObjectOfType<PlayerMovement>();
        if (pm != null) playerTransform = pm.transform;
    }

    private void LateUpdate()
    {
        if (!hasMessage || rootRect == null)
            return;

        // ซ่อน overlay ชั่วคราวเมื่อมี UI panel อื่นเปิดอยู่ (mailbox, journal, shop, dialogue ฯลฯ)
        bool uiOpen = UI_StateManager.Instance != null && UI_StateManager.Instance.interactWindowOpen;
        root.SetActive(rootVisible && !uiOpen);
        if (!rootVisible || uiOpen) return;

        if (playerTransform == null)
            FindPlayer();

        if (playerTransform == null)
            return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null || canvasRect == null)
            return;

        Canvas canvas = parentCanvas != null ? parentCanvas : GetComponentInParent<Canvas>();
        Camera uiCam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : cam;

        // root panel ไม่หมุนเลย — text อ่านได้ตลอด
        rootRect.localRotation = Quaternion.identity;

        // แปลงตำแหน่ง player → canvas local
        Vector3 playerScreen = cam.WorldToScreenPoint(playerTransform.position);
        if (playerScreen.z <= 0f) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, playerScreen, uiCam, out Vector2 playerLocal)) return;

        bool isNear = currentTarget != null &&
                      Vector3.Distance(playerTransform.position, currentTarget.position) <= nearDistance;

        if (isNear && currentTarget != null)
        {
            BobMode(cam, uiCam, playerLocal);
        }
        else
        {
            OrbitMode(cam, uiCam, playerLocal);
        }
    }

    private void OrbitMode(Camera cam, Camera uiCam, Vector2 playerLocal)
    {
        Vector2 direction = Vector2.right;

        if (currentTarget != null)
        {
            Vector3 targetScreen = cam.WorldToScreenPoint(currentTarget.position);
            if (targetScreen.z > 0f &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, targetScreen, uiCam, out Vector2 targetLocal))
            {
                Vector2 raw = targetLocal - playerLocal;
                if (raw.sqrMagnitude > 0.01f)
                    direction = raw.normalized;
            }
        }

        // anchor = player + offset คงที่, panel อยู่ที่ anchor + orbitRadius ในทิศทาง target
        Vector2 anchor = playerLocal + playerAnchorOffset;
        Vector2 orbitPos = anchor + direction * orbitRadius;

        if (!initialized)
        {
            rootRect.anchoredPosition = orbitPos;
            initialized = true;
        }
        else
        {
            rootRect.anchoredPosition = Vector2.Lerp(
                rootRect.anchoredPosition, orbitPos, followSpeed * Time.deltaTime);
        }

        // Arrow หมุนชี้ไปหา target, อยู่ที่ตำแหน่งที่กำหนดใน Inspector
        if (arrowRect != null)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            arrowRect.anchoredPosition = arrowOrbitLocalPos;
            arrowRect.localRotation = Quaternion.Lerp(
                arrowRect.localRotation,
                Quaternion.Euler(0f, 0f, angle),
                followSpeed * Time.deltaTime);
        }
    }

    private void BobMode(Camera cam, Camera uiCam, Vector2 playerLocal)
    {
        Vector3 targetScreen = cam.WorldToScreenPoint(currentTarget.position);
        if (targetScreen.z <= 0f) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, targetScreen, uiCam, out Vector2 targetLocal)) return;

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        Vector2 bobPos = targetLocal + new Vector2(0f, bobHeight + bob);

        rootRect.anchoredPosition = Vector2.Lerp(
            rootRect.anchoredPosition, bobPos, followSpeed * Time.deltaTime);

        // Arrow อยู่ใต้ข้อความ ชี้ลงหา target
        if (arrowRect != null)
        {
            arrowRect.anchoredPosition = Vector2.Lerp(
                arrowRect.anchoredPosition, arrowBobLocalPos, followSpeed * Time.deltaTime);
            arrowRect.localRotation = Quaternion.Lerp(
                arrowRect.localRotation,
                Quaternion.Euler(0f, 0f, 270f),
                followSpeed * Time.deltaTime);
        }
    }

    public void ShowMessage(string message, Transform target = null, float messageDuration = -1f)
    {
        if (this == null) return;

        hasMessage = !string.IsNullOrWhiteSpace(message);
        currentTarget = target;
        initialized = false;
        rootVisible = hasMessage;

        if (messageText != null)
            messageText.text = message ?? string.Empty;

        if (root != null)
            root.SetActive(rootVisible);

        if (arrowRect != null)
            arrowRect.gameObject.SetActive(hasMessage && currentTarget != null);

        CancelInvoke(nameof(HideMessagePanelOnly));
        if (hasMessage && messageDuration > 0)
        {
            Invoke(nameof(HideMessagePanelOnly), messageDuration);
        }
    }

    private void HideMessagePanelOnly()
    {
        if (this == null || root == null) return;

        rootVisible = false;
        root.SetActive(false);
    }

    public void Hide()
    {
        if (this == null) return;

        CancelInvoke(nameof(HideMessagePanelOnly));
        hasMessage = false;
        rootVisible = false;
        currentTarget = null;
        initialized = false;

        if (root != null)
            root.SetActive(false);

        if (arrowRect != null)
            arrowRect.gameObject.SetActive(false);
    }
}
