
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class RescueManager : NetworkBehaviour
{
    private const ulong NoRequester = ulong.MaxValue;
    private const float HelpCooldown = 5f;

    private static ulong activeRequesterId = NoRequester;
    private static ulong activeResponderId = NoRequester;
    private static bool responderReachedSafeZone;

    private static RescueManager serverInstance;

    private readonly Dictionary<ulong, float> lastHelpRequestTime =
        new Dictionary<ulong, float>();

    public static bool IsRescueActive =>
        activeRequesterId != NoRequester;

    public static ulong ActiveRequesterId => activeRequesterId;
    public static ulong ActiveResponderId => activeResponderId;

    public static bool ResponderReachedSafeZone =>
        responderReachedSafeZone;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            serverInstance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (serverInstance == this)
            serverInstance = null;
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.hKey.wasPressedThisFrame)
            RequestHelpServerRpc();

        if (Keyboard.current.jKey.wasPressedThisFrame)
            AcknowledgeHelpServerRpc();

        if (Keyboard.current.kKey.wasPressedThisFrame)
            CancelHelpServerRpc();
    }

    [ServerRpc]
    private void RequestHelpServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong requesterId = rpcParams.Receive.SenderClientId;

        if (lastHelpRequestTime.TryGetValue(
                requesterId, out float lastRequest))
        {
            if (Time.time - lastRequest < HelpCooldown)
                return;
        }

        if (activeRequesterId != NoRequester)
            return;

        activeRequesterId = requesterId;
        activeResponderId = NoRequester;
        responderReachedSafeZone = false;

        lastHelpRequestTime[requesterId] = Time.time;

        NotifyHelpRequestedClientRpc(requesterId);
    }

    [ServerRpc]
    private void AcknowledgeHelpServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong responderId = rpcParams.Receive.SenderClientId;

        if (activeRequesterId == NoRequester)
            return;

        if (responderId == activeRequesterId)
            return;

        if (activeResponderId != NoRequester)
            return;

        activeResponderId = responderId;
        responderReachedSafeZone = false;

        NotifyHelpAcknowledgedClientRpc(
            activeRequesterId, responderId);
    }

    [ServerRpc]
    private void CancelHelpServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong requesterId = rpcParams.Receive.SenderClientId;

        if (activeRequesterId != requesterId)
            return;

        ulong cancelledRequesterId = activeRequesterId;

        activeRequesterId = NoRequester;
        activeResponderId = NoRequester;
        responderReachedSafeZone = false;

        NotifyHelpCancelledClientRpc(cancelledRequesterId);
    }

    // Called when a player enters the SafeZone trigger.
    public static void NotifySafeZoneEntered(
        PlayerInventory inventory)
    {
        if (inventory == null)
            return;

        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
            return;

        NetworkObject playerObject =
            inventory.GetComponentInParent<NetworkObject>();

        if (playerObject == null)
            return;

        ulong playerId = playerObject.OwnerClientId;

        if (activeRequesterId == NoRequester ||
            activeResponderId == NoRequester)
            return;

        // Only the player who accepted HELP can activate this.
        if (playerId != activeResponderId)
            return;

        if (responderReachedSafeZone)
            return;

        responderReachedSafeZone = true;

        Debug.Log(
            $"RESCUE: Responder {playerId} reached the SafeZone.");

        if (serverInstance != null)
        {
            serverInstance.NotifySafeZoneReachedClientRpc(playerId);
        }
    }

    [ClientRpc]
    private void NotifyHelpRequestedClientRpc(ulong requesterId)
    {
        Debug.Log($"RESCUE: Player {requesterId} requested HELP!");

        if (HelpNotificationUI.Instance != null)
        {
            HelpNotificationUI.Instance.ShowPersistentStatus(
                $"PLAYER {requesterId} NEEDS HELP - WAITING FOR RESCUE");
        }
    }

    [ClientRpc]
    private void NotifyHelpAcknowledgedClientRpc(
        ulong requesterId, ulong responderId)
    {
        Debug.Log(
            $"RESCUE: Player {responderId} accepted " +
            $"Player {requesterId}'s HELP request.");

        if (HelpNotificationUI.Instance != null)
        {
            HelpNotificationUI.Instance.ShowPersistentStatus(
                $"PLAYER {responderId} ACCEPTED HELP - REACH THE SAFEZONE");
        }
    }

    [ClientRpc]
    private void NotifySafeZoneReachedClientRpc(ulong responderId)
    {
        Debug.Log(
            $"RESCUE: Player {responderId} reached the SafeZone. " +
            "Ready for timeline integration.");

        if (HelpNotificationUI.Instance != null)
        {
            HelpNotificationUI.Instance.ShowPersistentStatus(
                $"PLAYER {responderId} REACHED THE SAFEZONE");
        }
    }

    [ClientRpc]
    private void NotifyHelpCancelledClientRpc(ulong requesterId)
    {
        Debug.Log(
            $"RESCUE: Player {requesterId} cancelled their HELP request.");

        if (HelpNotificationUI.Instance != null)
            HelpNotificationUI.Instance.ClearStatus();
    }
}
