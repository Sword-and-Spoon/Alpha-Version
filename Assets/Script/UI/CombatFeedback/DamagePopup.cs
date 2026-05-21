using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TMP_Text textMesh;
    private float disappearTimer;
    private Color textColor;
    private static int sortingOrder;

    [SerializeField] private float moveYSpeed = 1f;
    [SerializeField] private float disappearSpeed = 3f;
    [SerializeField] private float startDisappearTime = 0.5f;

    private void Awake()
    {
        textMesh = GetComponent<TMP_Text>();
    }

    public void Setup(string text, Color color, float sizeScale = 1f)
    {
        textMesh.SetText(text);
        textMesh.color = color;
        textColor = color;
        transform.localScale = Vector3.one * sizeScale;
        disappearTimer = startDisappearTime;

        sortingOrder++;
        Renderer meshRenderer = GetComponent<Renderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = sortingOrder;
        }

        // Reset alpha
        textColor.a = 1f;
        textMesh.color = textColor;
    }

    private void Update()
    {
        // 1. Move Up
        transform.position += new Vector3(0, moveYSpeed) * Time.deltaTime;

        // 2. Fade Out
        disappearTimer -= Time.deltaTime;
        if (disappearTimer < 0)
        {
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;
            if (textColor.a < 0)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
