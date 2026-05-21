using System.Collections.Generic;
using UnityEngine;

public class PlayerSlotManager : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public Vector2 offset;
        public GameObject occupier;
        public bool IsOccupied => occupier != null;

        public Slot(Vector2 offset)
        {
            this.offset = offset;
            this.occupier = null;
        }

        public Vector3 GetWorldPosition(Transform playerTransform)
        {
            return playerTransform.position + (Vector3)offset;
        }
    }

    public List<Slot> slots = new List<Slot>();
    [SerializeField] private float slotOffset = 1.3f;

    // สร้าง Slot ให้อัตโนมัติแม้ยังไม่กดเริ่มเกม
    private void OnValidate()
    {
        if (slots == null || slots.Count != 4)
        {
            CreateSlots();
        }
        else
        {
            // อัปเดตระยะ Offset ถ้ามีการแก้ใน Inspector
            slots[0].offset = new Vector2(0, slotOffset);
            slots[1].offset = new Vector2(0, -slotOffset);
            slots[2].offset = new Vector2(-slotOffset, 0);
            slots[3].offset = new Vector2(slotOffset, 0);
        }
    }

    private void CreateSlots()
    {
        slots = new List<Slot>
        {
            new Slot(new Vector2(0, slotOffset)),    // Up
            new Slot(new Vector2(0, -slotOffset)),   // Down
            new Slot(new Vector2(-slotOffset, 0)),  // Left
            new Slot(new Vector2(slotOffset, 0))    // Right
        };
    }

    public Slot RequestSlot(GameObject enemy)
    {
        Slot bestSlot = null;
        float minDistance = float.MaxValue;

        foreach (var slot in slots)
        {
            if (!slot.IsOccupied)
            {
                float dist = Vector2.Distance(enemy.transform.position, slot.GetWorldPosition(transform));
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestSlot = slot;
                }
            }
        }

        if (bestSlot != null)
        {
            bestSlot.occupier = enemy;
        }

        return bestSlot;
    }

    public void ReleaseSlot(GameObject enemy)
    {
        foreach (var slot in slots)
        {
            if (slot.occupier == enemy)
            {
                slot.occupier = null;
                break;
            }
        }
    }

    // วาดจุด Slot ในหน้า Scene เพื่อการตั้งค่าที่ง่ายขึ้น
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (var slot in slots)
        {
            Gizmos.DrawWireSphere(transform.position + (Vector3)slot.offset, 0.2f);
            if (slot.IsOccupied)
            {
                Gizmos.DrawLine(transform.position + (Vector3)slot.offset, slot.occupier.transform.position);
            }
        }
    }
}
