
using UnityEngine;
using Unity.Netcode;

public class PlayerInventory : NetworkBehaviour
{
    private readonly NetworkVariable<bool> hasBankKey =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public bool HasBankKey => hasBankKey.Value;

    public void CollectBankKey()
    {
        if (!IsServer)
            return;

        if (hasBankKey.Value)
            return;

        hasBankKey.Value = true;

        Debug.Log(
            $"INVENTORY: Bank Key collected by Player {OwnerClientId}."
        );
    }
}
