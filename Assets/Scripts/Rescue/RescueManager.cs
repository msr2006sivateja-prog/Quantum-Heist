
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class RescueManager : NetworkBehaviour
{
    private const ulong NoRequester = ulong.MaxValue;
    private const float HelpCooldown = 5f;

    private static ulong activeRequesterId = NoRequester;

    private readonly Dictionary<ulong, float> lastHelpRequestTime =
        new Dictionary<ulong, float>();

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

        // Prevent repeated requests during the cooldown.
        if (lastHelpRequestTime.TryGetValue(
                requesterId, out float lastRequest))
        {
            if (Time.time - lastRequest < HelpCooldown)
                return;
        }

        // Only allow one active HELP request at a time.
        if (activeRequesterId != NoRequester)
            return;

        lastHelpRequestTime[requesterId] = Time.time;
        activeRequesterId = requesterId;

        NotifyHelpRequestedClientRpc(requesterId);
    }

    [ServerRpc]
    private void AcknowledgeHelpServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong responderId = rpcParams.Receive.SenderClientId;

        if (activeRequesterId == NoRequester)
            return;

        // A player cannot acknowledge their own request.
        if (responderId == activeRequesterId)
            return;

        ulong requesterId = activeRequesterId;
        activeRequesterId = NoRequester;

        NotifyHelpAcknowledgedClientRpc(requesterId, responderId);
    }

    [ServerRpc]
    private void CancelHelpServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong requesterId = rpcParams.Receive.SenderClientId;

        // Only the player who requested help can cancel it.
        if (activeRequesterId != requesterId)
            return;

        activeRequesterId = NoRequester;

        NotifyHelpCancelledClientRpc(requesterId);
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
            HelpNotificationUI.Instance.ShowMessage(
                $"PLAYER {responderId} ACCEPTED PLAYER {requesterId}'S HELP");
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
