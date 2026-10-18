
using System.Collections.Generic;
using UnityEngine;

public class SafeZone : MonoBehaviour
{
    private readonly HashSet<PlayerInventory> playersInside =
        new HashSet<PlayerInventory>();

    public bool IsPlayerInside(PlayerInventory player)
    {
        return player != null && playersInside.Contains(player);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerInventory inventory =
            other.GetComponentInParent<PlayerInventory>();

        if (inventory == null)
            return;

        playersInside.Add(inventory);

        Debug.Log("SAFE ZONE: Player entered the safe zone.");
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerInventory inventory =
            other.GetComponentInParent<PlayerInventory>();

        if (inventory == null)
            return;

        playersInside.Remove(inventory);

        Debug.Log("SAFE ZONE: Player left the safe zone.");
    }

    private void OnDisable()
    {
        playersInside.Clear();
    }
}
