using System.Collections;
using UnityEngine;

public class HitFlash : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Material flashMaterial; // Assign a material with "GUI/Text Shader" for solid white

    [Header("Settings")]
    [SerializeField] private float flashDuration = 0.12f;
    [SerializeField] private Color flashColor = new Color(1, 1, 1, 0.6f); // 60% opacity white

    private Material originalMaterial;
    private Color originalColor = Color.white;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalMaterial = spriteRenderer.material;
            originalColor = spriteRenderer.color;
        }
    }

    public void Flash()
    {
        if (spriteRenderer == null || flashMaterial == null) return;

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // 1. Swap to Flash Material (Solid White Shader)
        spriteRenderer.material = flashMaterial;
        spriteRenderer.color = flashColor;

        yield return new WaitForSeconds(flashDuration);

        // 2. Restore Original Material and Color
        spriteRenderer.material = originalMaterial;
        spriteRenderer.color = originalColor;

        flashCoroutine = null;
    }

    // Ensure state is restored if object is disabled mid-flash
    private void OnDisable()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
            if (spriteRenderer != null)
            {
                spriteRenderer.material = originalMaterial;
                spriteRenderer.color = originalColor;
            }
        }
    }
}
