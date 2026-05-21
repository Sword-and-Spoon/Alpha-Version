using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Slot : MonoBehaviour, IPointerClickHandler
{
    public int index;
    public GameObject currentItem;
    private GameObject border;
    private System.Action<int> slotClickCallback;

    private void Awake()
    {
        border = transform.Find("Border").gameObject;
        border.SetActive(false);
    }

    public void Setup(int idx, System.Action<int> callback, bool clickable)
    {
        index = idx;
        slotClickCallback = callback;
    }

    public void SetHighlight(bool active)
    {
        if (border != null)
        {
            border.SetActive(active);
            if (active)
                border.transform.SetAsLastSibling(); // keep border in front
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (slotClickCallback != null)
            slotClickCallback(index);
    }
}
