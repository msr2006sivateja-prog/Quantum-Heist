
using UnityEngine;
using Unity.Netcode;

public class BankLockerUnlock : NetworkBehaviour
{
    [Header("Objects to Hide")]
    [SerializeField] private GameObject lockerVisual;
    [SerializeField] private GameObject additionalCylinder;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;

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
        ApplyLockerState(isUnlocked.Value);
    }

    public override void OnNetworkDespawn()
    {
        isUnlocked.OnValueChanged -= OnUnlockChanged;
    }

    public void RequestUnlock()
    {
        if (!IsClient || IsUnlocked)
            return;

        UnlockLockerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UnlockLockerRpc(RpcParams rpcParams = default)
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
            return;

        PlayerInventory inventory =
            player.GetComponentInChildren<PlayerInventory>();

        if (inventory == null || !inventory.HasBankKey)
        {
            Debug.Log("LOCKER: Bank Key required.");
            return;
        }

        isUnlocked.Value = true;

        Debug.Log("LOCKER: Unlocked! Locker and cylinder disappeared.");
    }

    private void OnUnlockChanged(bool oldValue, bool newValue)
    {
        ApplyLockerState(newValue);
    }

    private void ApplyLockerState(bool unlocked)
    {
        if (lockerVisual != null)
            lockerVisual.SetActive(!unlocked);

        if (additionalCylinder != null)
            additionalCylinder.SetActive(!unlocked);
    }
}
