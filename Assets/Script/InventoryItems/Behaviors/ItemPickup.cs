using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    Transform player;
    [SerializeField] float baseSpeed = 5f;
    [SerializeField] float maxSpeedMultiplier = 3f;
    [SerializeField] float pickUpDistance = 2.5f;
    // [SerializeField] float despawnTime = 10f;

    private void Awake()
    {
        player = GameManager.instance.player.transform;
    }

    private void Update()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance > pickUpDistance) return;

        float normalizedDistanceFactor = 1f - (distance / pickUpDistance);
        normalizedDistanceFactor = Mathf.Clamp01(normalizedDistanceFactor);

        float currentSpeed = baseSpeed + (baseSpeed * maxSpeedMultiplier * normalizedDistanceFactor);

        // Move the item towards the player
        transform.position = Vector3.MoveTowards(
            transform.position,
            player.position,
            currentSpeed * Time.deltaTime
        );
    }
}
