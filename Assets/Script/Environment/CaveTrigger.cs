using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CaveTrigger : MonoBehaviour
{
    [SerializeField] private EnvironmentState stateOnEnter = EnvironmentState.Cave;
    [SerializeField] private EnvironmentState stateOnExit = EnvironmentState.Outdoor;

    private void Awake()
    {
        // บังคับให้ Collider เป็น Trigger เสมอ
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        EnvironmentStateController.Instance?.SetState(stateOnEnter);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        EnvironmentStateController.Instance?.SetState(stateOnExit);
    }
}
