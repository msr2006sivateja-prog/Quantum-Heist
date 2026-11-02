
using UnityEngine;
using Unity.Netcode;

public class BankKeyPickup : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
            return;

        PlayerInventory inventory =
            other.GetComponentInParent<PlayerInventory>();

        if (inventory == null)
            return;

        if (inventory.HasBankKey)
            return;

        inventory.CollectBankKey();

        Debug.Log(
            $"BANK KEY: Collected by Player {inventory.OwnerClientId}."
        );

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Debug.LogError(
                "BANK KEY: Pickup needs a spawned NetworkObject."
            );
        }
    }
}
