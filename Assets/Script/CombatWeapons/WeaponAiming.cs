using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAiming : MonoBehaviour
{
    private Camera cam;
    public Transform weaponPivot;

    private void Awake()
    {
        cam = Camera.main;
    }

    private void Update()
    {
        // Refresh camera if it's null or destroyed (after scene change)
        if (cam == null) cam = Camera.main;
        
        if (cam != null)
        {
            Aim();
        }
    }

    private void Aim()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 worldPos = cam.ScreenToWorldPoint(mousePos);
        Vector2 direction = (worldPos - weaponPivot.position).normalized;

        transform.right = -direction;
        Vector2 scale = transform.localScale;
        if (direction.x < 0)
        {
            scale.y = 1;
        }
        else if (direction.x > 0)
        {
            scale.y = -1;
        }
        transform.localScale = scale;
    }
}
