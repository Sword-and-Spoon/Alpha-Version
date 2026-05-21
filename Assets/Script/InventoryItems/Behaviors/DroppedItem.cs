using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    [HideInInspector] public bool canBeCollected = false;
    private Vector3 startPos;
    private Vector3 targetPos;
    private float height = 1f;  // how high it hops visually
    private float dropDuration = .65f; // how long the whole hop lasts

    void Start()
    {
        startPos = transform.position;

        // Choose a random nearby landing spot (X/Y only)
        Vector2 randomOffset = Random.insideUnitCircle * .5f; // 0.5f
        targetPos = startPos + new Vector3(randomOffset.x, randomOffset.y, 0);

        // Start the fake “hop” animation
        StartCoroutine(HopToGround());
    }

    private IEnumerator HopToGround()
    {
        float elapsed = 0f;
        Vector3 randomRotation = new Vector3(0, 0, Random.Range(0f, 360f));

        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dropDuration;

            // Arc curve: goes up, then back down (using a parabola)
            float heightOffset = Mathf.Sin(t * Mathf.PI) * height;

            // Interpolate position between start and target, plus the height offset
            transform.position = Vector3.Lerp(startPos, targetPos, t) + new Vector3(0, heightOffset, 0);

            yield return null;
        }

        // Snap to final position and apply random static angle
        transform.position = targetPos;
        // transform.rotation = Quaternion.Euler(randomRotation);
        canBeCollected = true;
    }
}
