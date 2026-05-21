using UnityEngine;
using UnityEngine.UI;

public class CounterUIToggle : MonoBehaviour
{
    public Button closeButton;
    [HideInInspector][SerializeField] private RestaurantCounterInteractable ownerInteractable;

    public void BindOwner(RestaurantCounterInteractable owner)
    {
        ownerInteractable = owner;
    }

    private void Awake()
    {
        if (closeButton == null)
        {
            ResolveReference();
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void Close()
    {
        if (ownerInteractable != null)
        {
            ownerInteractable.CloseCounterWindow();
        }
    }

    private void ResolveReference()
    {
        closeButton = transform.Find("Button_Close").GetComponent<Button>();
    }
}
