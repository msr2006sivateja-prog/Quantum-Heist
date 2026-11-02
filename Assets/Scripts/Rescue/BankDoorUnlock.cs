
using UnityEngine;
using Unity.Netcode;

public class BankDoorUnlock : NetworkBehaviour
{
    [Header("Door Visual")]
    [SerializeField] private GameObject doorVisual;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 4f;

    [Header("Rescue Settings")]
    [SerializeField] private bool isTemporaryBankDoor;

    private readonly NetworkVariable<bool> isUnlocked =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public bool IsUnlocked => isUnlocked.Value;

    public override void OnNetworkSpawn()
    {
        isUnlocked.OnValueChanged += OnUnlockChanged;
        ApplyDoorState(isUnlocked.Value);
    }

    public override void OnNetworkDespawn()
    {
        isUnlocked.OnValueChanged -= OnUnlockChanged;
    }

    public void UnlockFromRescue()
    {
        if (!IsServer || isUnlocked.Value)
            return;

        isUnlocked.Value = true;

        Debug.Log("BANK DOOR: Unlocked through HELP rescue.");
    }

    public void RequestUnlock()
    {
        if (!IsClient || IsUnlocked)
            return;

        UnlockDoorRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UnlockDoorRpc(RpcParams rpcParams = default)
    {
        if (isUnlocked.Value)
            return;

        ulong clientId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            clientId, out var client))
            return;

        NetworkObject player = client.PlayerObject;

        if (player == null)
            return;

        float distance = Vector3.Distance(
            player.transform.position,
            transform.position
        );

        if (distance > interactionDistance)
        {
            Debug.LogWarning("DOOR: Player is too far away.");
            return;
        }

        PlayerInventory inventory =
            player.GetComponentInChildren<PlayerInventory>();

        // The same Bank Key works in every dimension.
        if (inventory == null || !inventory.HasBankKey)
        {
            Debug.Log("DOOR: Player does not have the Bank Key.");
            return;
        }

        isUnlocked.Value = true;

        // Only the TempBank door triggers the original bank door.
        if (isTemporaryBankDoor &&
            QuantumReplayManager.Instance != null)
        {
            QuantumReplayManager.Instance
                .UnlockOriginalBankDoorForActiveRescue();
        }

        Debug.Log("DOOR: Unlocked! Door will disappear for everyone.");
    }

    private void OnUnlockChanged(bool oldValue, bool newValue)
    {
        ApplyDoorState(newValue);
    }

    private void ApplyDoorState(bool unlocked)
    {
        if (doorVisual != null)
        {
            doorVisual.SetActive(!unlocked);
        }
    }
}
