
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class RescueManager : NetworkBehaviour
{
    private static ulong activeRequesterId = ulong.MaxValue;

    private readonly Dictionary<ulong, float> lastHelpRequestTime
        = new Dictionary<ulong, float>();

    private const float HelpCooldown = 5f;

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

        lastHelpRequestTime[requesterId] = Time.time;
        activeRequesterId = requesterId;

        NotifyHelpRequestedClientRpc(requesterId);
    }

    [ServerRpc]
    private void AcknowledgeHelpServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong responderId = rpcParams.Receive.SenderClientId;

        if (activeRequesterId == ulong.MaxValue)
            return;

        if (responderId == activeRequesterId)
            return;

        ulong requesterId = activeRequesterId;
        activeRequesterId = ulong.MaxValue;

        NotifyHelpAcknowledgedClientRpc(
            requesterId, responderId);
    }

    [ClientRpc]
    private void NotifyHelpRequestedClientRpc(ulong requesterId)
    {
        Debug.Log($"RESCUE: Player {requesterId} requested HELP!");

        if (HelpNotificationUI.Instance != null)
            HelpNotificationUI.Instance.ShowHelpRequest(requesterId);
    }

    [ClientRpc]
    private void NotifyHelpAcknowledgedClientRpc(
        ulong requesterId, ulong responderId)
    {
        Debug.Log(
            $"RESCUE: Player {responderId} acknowledged " +
            $"Player {requesterId}'s HELP request.");

        if (HelpNotificationUI.Instance != null)
        {
            HelpNotificationUI.Instance.ShowMessage(
                $"PLAYER {responderId} ACCEPTED PLAYER {requesterId}'S HELP");
        }
    }
}
