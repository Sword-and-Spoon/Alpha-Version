using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InSceneTeleporter : MonoBehaviour
{
    [SerializeField] private Transform destination;
    [SerializeField] private float cooldown = 1f;

    private bool isOnCooldown;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isOnCooldown) return;
        if (!other.CompareTag("Player")) return;
        if (destination == null) return;

        Teleport(other.transform);
    }

    private void Teleport(Transform player)
    {
        player.position = destination.position;
        StartCoroutine(StartCooldown());
    }

    private IEnumerator StartCooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSecondsRealtime(cooldown);
        isOnCooldown = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (destination == null) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawSphere(destination.position, 0.25f);
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
}
