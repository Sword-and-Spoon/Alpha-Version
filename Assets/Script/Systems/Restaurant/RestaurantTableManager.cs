using System;
using System.Collections.Generic;
using UnityEngine;

public class RestaurantTableManager : MonoBehaviour
{
    [SerializeField] private List<RestaurantSeat> seats = new();
    [SerializeField] private bool autoCollectSeatsFromChildren = true;

    [HideInInspector] [SerializeField] private bool autoCreateSeatComponentsFromChairObjects = true;
    [HideInInspector] [SerializeField] private string chairNamePrefix = "Chair";

    public int TotalSeatCount => seats.Count;

    private void Awake()
    {
        RefreshSeats();
    }

    public void RefreshSeats()
    {
        if (autoCollectSeatsFromChildren)
        {
            seats.Clear();

            if (autoCreateSeatComponentsFromChairObjects)
            {
                EnsureSeatComponentsOnChairObjects();
            }

            RestaurantSeat[] foundSeats = GetComponentsInChildren<RestaurantSeat>(true);
            for (int i = 0; i < foundSeats.Length; i++)
            {
                RestaurantSeat seat = foundSeats[i];
                if (seat != null && !seats.Contains(seat))
                {
                    seats.Add(seat);
                }
            }
        }
        else
        {
            for (int i = seats.Count - 1; i >= 0; i--)
            {
                if (seats[i] == null)
                {
                    seats.RemoveAt(i);
                }
            }
        }
    }

    public void CollectAvailableSeats(List<RestaurantSeat> output)
    {
        if (output == null)
        {
            return;
        }

        for (int i = 0; i < seats.Count; i++)
        {
            RestaurantSeat seat = seats[i];
            if (seat == null)
            {
                continue;
            }

            if (!seat.IsAvailable)
            {
                continue;
            }

            if (!output.Contains(seat))
            {
                output.Add(seat);
            }
        }
    }

    public bool TryReserveSeat(RestaurantCustomerAI customer, out RestaurantSeat reservedSeat)
    {
        reservedSeat = null;
        if (customer == null) return false;

        List<RestaurantSeat> available = new List<RestaurantSeat>();
        CollectAvailableSeats(available);

        if (available.Count == 0)
        {
            return false;
        }

        RestaurantSeat candidate = available[UnityEngine.Random.Range(0, available.Count)];
        if (candidate != null && candidate.TryReserve(customer))
        {
            reservedSeat = candidate;
            return true;
        }

        return false;
    }

    public void ReleaseSeat(RestaurantCustomerAI customer, RestaurantSeat seat)
    {
        if (seat == null) return;
        seat.Release(customer);
    }

    private void EnsureSeatComponentsOnChairObjects()
    {
        if (string.IsNullOrWhiteSpace(chairNamePrefix))
        {
            return;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
            {
                continue;
            }

            if (!child.name.StartsWith(chairNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            RestaurantSeat seat = child.GetComponent<RestaurantSeat>();
            if (seat == null)
            {
                child.gameObject.AddComponent<RestaurantSeat>();
            }
        }
    }
}