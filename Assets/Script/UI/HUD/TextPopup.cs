using UnityEngine;
using TMPro;

public class TextPopup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text textMesh;
    [SerializeField] private Transform moveTarget;
    [SerializeField] private GameObject deactivateTarget;

    [Header("Animation")]
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float lifeTime = 0.9f;
    [SerializeField] private float fadeDuration = 0.3f;

    private Color textColor;
    private float timer;
    private float activeMoveSpeed;
    private float activeFadeDuration;

    private void Awake()
    {
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TMP_Text>(true);
        }

        if (deactivateTarget == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            deactivateTarget = parentCanvas != null ? parentCanvas.gameObject : gameObject;
        }

        if (moveTarget == null)
        {
            moveTarget = deactivateTarget != null ? deactivateTarget.transform : transform;
        }

        activeMoveSpeed = moveSpeed;
        activeFadeDuration = fadeDuration;
    }

    public void Setup(
        string text,
        Color color,
        float sizeScale = 1f,
        float moveSpeedOverride = -1f,
        float lifeTimeOverride = -1f,
        float fadeDurationOverride = -1f)
    {
        if (textMesh == null)
        {
            textMesh = GetComponentInChildren<TMP_Text>(true);
        }

        if (textMesh == null)
        {
            Debug.LogWarning("[TextPopup] TMP_Text not found on popup prefab.");
            DeactivatePopup();
            return;
        }

        textMesh.text = text;
        textColor = color;
        textColor.a = 1f;
        textMesh.color = textColor;

        if (moveTarget == null)
        {
            moveTarget = transform;
        }

        activeMoveSpeed = moveSpeedOverride > 0f ? moveSpeedOverride : moveSpeed;
        float activeLifeTime = lifeTimeOverride > 0f ? lifeTimeOverride : lifeTime;
        activeFadeDuration = fadeDurationOverride > 0f ? fadeDurationOverride : fadeDuration;
        activeFadeDuration = Mathf.Min(activeFadeDuration, activeLifeTime);

        moveTarget.localScale = Vector3.one * Mathf.Max(0.1f, sizeScale);
        timer = Mathf.Max(0.05f, activeLifeTime);
    }

    private void Update()
    {
        if (moveTarget != null)
        {
            moveTarget.position += Vector3.up * (activeMoveSpeed * Time.deltaTime);
        }

        timer -= Time.deltaTime;
        if (textMesh != null && timer <= Mathf.Max(0.01f, activeFadeDuration))
        {
            float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, activeFadeDuration));
            textColor.a = t;
            textMesh.color = textColor;
        }

        if (timer <= 0f)
        {
            DeactivatePopup();
        }
    }

    private void DeactivatePopup()
    {
        if (deactivateTarget != null)
        {
            deactivateTarget.SetActive(false);
            return;
        }

        gameObject.SetActive(false);
    }
}
