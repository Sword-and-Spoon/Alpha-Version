using UnityEngine;
using UnityEngine.EventSystems;

public class DropZone : MonoBehaviour, IDropHandler
{
    public string side; // Dr or Cr

    public void OnDrop(PointerEventData eventData)
    {
        if (JournalManager.Instance == null)
        {
            Debug.LogError("DropZone: JournalManager.Instance is null. Ensure a JournalManager exists in the scene.");
            return;
        }

        JournalManager.Instance.HandleDrop(eventData, side);
    }
}
