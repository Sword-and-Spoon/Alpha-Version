using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerTeleport : MonoBehaviour
{
    private GameObject currentTeleporter;
    // Start is called before the first frame update
    // Update is called once per frame
    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        // Use new Input System when enabled
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
#else
        // Fallback to old Input Manager
        if (Input.GetKeyDown(KeyCode.F))
#endif
        {
            if (currentTeleporter != null) {
                currentTeleporter.GetComponent<Teleporter>().HandleTeleport(gameObject);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Teleporter"))
        {
            currentTeleporter = collision.gameObject;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
         if (collision.CompareTag("Teleporter"))
        {
            if (collision.gameObject == currentTeleporter)
            {
                currentTeleporter = null;
            }
        }

    }
}
