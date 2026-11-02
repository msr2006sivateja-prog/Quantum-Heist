
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class RescueManager : NetworkBehaviour
{
    private const ulong NoRequester = ulong.MaxValue;
    private const float HelpCooldown = 5f;
    private const float OriginalBankPoliceRadius = 150f;

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

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            serverInstance = this;
            Debug.Log("RESCUE: Server manager ready.");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (serverInstance == this)
            serverInstance = null;
    }

    private void Update()
    {
        if (!IsClient || !IsSpawned)
            return;

        if (NetworkObject.IsPlayerObject && !IsOwner)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            Debug.Log("RESCUE INPUT: H pressed.");
            RequestHelpServerRpc();
        }

        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            Debug.Log("RESCUE INPUT: J pressed.");
            AcknowledgeHelpServerRpc();
        }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            Debug.Log("RESCUE INPUT: K pressed.");
            CancelHelpServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestHelpServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong requesterId = rpcParams.Receive.SenderClientId;

        Debug.Log($"RESCUE: Player {requesterId} requested HELP.");

        if (lastHelpRequestTime.TryGetValue(
            requesterId, out float lastRequest))
        {
            if (Time.time - lastRequest < HelpCooldown)
            {
                Debug.LogWarning(
                    $"RESCUE: Player {requesterId} is on cooldown."
                );
                return;
            }
        }

        if (activeRequesterId != NoRequester)
        {
            Debug.LogWarning(
                $"RESCUE: Player {activeRequesterId} already has an active request."
            );
            return;
        }

        activeRequesterId = requesterId;
        activeResponderId = NoRequester;
        responderReachedSafeZone = false;

        lastHelpRequestTime[requesterId] = Time.time;

        Debug.Log($"RESCUE: HELP request registered for Player {requesterId}.");

        NotifyHelpRequestedClientRpc(requesterId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AcknowledgeHelpServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong responderId = rpcParams.Receive.SenderClientId;

        Debug.Log($"RESCUE: Player {responderId} wants to accept HELP.");

        if (activeRequesterId == NoRequester)
        {
            Debug.LogWarning("RESCUE: There is no active HELP request.");
            return;
        }

        if (responderId == activeRequesterId)
        {
            Debug.LogWarning("RESCUE: Requester cannot rescue themselves.");
            return;
        }

        if (activeResponderId != NoRequester)
        {
            Debug.LogWarning(
                $"RESCUE: Player {activeResponderId} already accepted."
            );
            return;
        }

        activeResponderId = responderId;
        responderReachedSafeZone = false;

        Debug.Log(
            $"RESCUE: Player {responderId} accepted Player " +
            $"{activeRequesterId}'s request."
        );

        NotifyHelpAcknowledgedClientRpc(
            activeRequesterId,
            responderId
        );
    }

    [ServerRpc(RequireOwnership = false)]
    private void CancelHelpServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong requesterId = rpcParams.Receive.SenderClientId;

        if (activeRequesterId != requesterId)
        {
            Debug.LogWarning(
                "RESCUE: Only the requester can cancel HELP."
            );
            return;
        }

        ResetHelpState();
        NotifyHelpCancelledClientRpc(requesterId);
    }

    private static void ResetHelpState()
    {
        activeRequesterId = NoRequester;
        activeResponderId = NoRequester;
        responderReachedSafeZone = false;

        Debug.Log("RESCUE: HELP state has been reset.");
    }

    // Called by QuantumReplayManager when the ghost finishes replaying.
    public static void CompleteRescueOnServer()
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
            return;

        if (serverInstance == null)
        {
            Debug.LogError("RESCUE: Server manager instance is missing.");
            return;
        }

        ulong completedRequester = activeRequesterId;
        ulong completedResponder = activeResponderId;

        ResetHelpState();

        serverInstance.NotifyRescueCompletedClientRpc(
            completedRequester,
            completedResponder
        );
    }

    public static void NotifySafeZoneEntered(PlayerInventory inventory)
    {
        if (inventory == null ||
            NetworkManager.Singleton == null ||
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

        if (playerId != activeResponderId ||
            responderReachedSafeZone)
            return;

        if (QuantumReplayManager.Instance == null)
        {
            Debug.LogError("RESCUE: QuantumReplayManager is missing.");
            return;
        }

        bool started =
            QuantumReplayManager.Instance.BeginHelpRescue(
                activeRequesterId,
                activeResponderId
            );

        if (!started)
        {
            Debug.LogError("RESCUE: Quantum travel failed to start.");
            return;
        }

        responderReachedSafeZone = true;

        Debug.Log(
            $"RESCUE: Quantum travel started. Rescued={activeRequesterId}, " +
            $"Rescuer={activeResponderId}"
        );

        if (serverInstance != null)
        {
            serverInstance.NotifySafeZoneReachedClientRpc(playerId);

            QuantumReplayManager manager =
                QuantumReplayManager.Instance;

            serverInstance.PauseOriginalPoliceClientRpc(
                manager.Bank1Position,
                manager.Bank2Position,
                OriginalBankPoliceRadius
            );
        }
    }

    [ClientRpc]
    private void PauseOriginalPoliceClientRpc(
        Vector3 bank1Position,
        Vector3 bank2Position,
        float radius)
    {
        PoliceAI[] officers = FindObjectsOfType<PoliceAI>();

        foreach (PoliceAI officer in officers)
        {
            if (officer == null)
                continue;

            float d1 = Vector3.Distance(
                officer.transform.position, bank1Position);

            float d2 = Vector3.Distance(
                officer.transform.position, bank2Position);

            if (d1 <= radius || d2 <= radius)
                officer.SetPaused(true);
        }
    }

    [ClientRpc]
    public void ResumeOriginalPoliceClientRpc(
        Vector3 bank1Position,
        Vector3 bank2Position,
        float radius)
    {
        PoliceAI[] officers = FindObjectsOfType<PoliceAI>();

        foreach (PoliceAI officer in officers)
        {
            if (officer == null)
                continue;

            float d1 = Vector3.Distance(
                officer.transform.position, bank1Position);

            float d2 = Vector3.Distance(
                officer.transform.position, bank2Position);

            if (d1 <= radius || d2 <= radius)
                officer.SetPaused(false);
        }

        Debug.Log("RESCUE: Original-bank police resumed.");
    }

    [ClientRpc]
    private void NotifyHelpRequestedClientRpc(ulong requesterId)
    {
        Debug.Log($"RESCUE: Player {requesterId} requested HELP!");

        if (HelpNotificationUI.Instance != null)
        {
            HelpNotificationUI.Instance.ShowPersistentStatus(
                $"PLAYER {requesterId} NEEDS HELP - WAITING FOR RESCUE"
            );
        }
    }

    [ClientRpc]
    private void NotifyHelpAcknowledgedClientRpc(
        ulong requesterId, ulong responderId)
    {
        if (HelpNotificationUI.Instance != null)
        {
            HelpNotificationUI.Instance.ShowPersistentStatus(
                $"PLAYER {responderId} ACCEPTED HELP - REACH THE SAFEZONE"
            );
        }
    }

    [ClientRpc]
    private void NotifySafeZoneReachedClientRpc(ulong responderId)
    {
        if (HelpNotificationUI.Instance != null)
        {
            HelpNotificationUI.Instance.ShowPersistentStatus(
                $"PLAYER {responderId} ENTERED THE TEMPORARY BANK"
            );
        }
    }

    [ClientRpc]
    private void NotifyHelpCancelledClientRpc(ulong requesterId)
    {
        if (HelpNotificationUI.Instance != null)
            HelpNotificationUI.Instance.ClearStatus();
    }

    [ClientRpc]
    private void NotifyRescueCompletedClientRpc(
        ulong requesterId, ulong responderId)
    {
        Debug.Log(
            $"RESCUE: Completed. Requester={requesterId}, " +
            $"Responder={responderId}. Ready for another request."
        );

        if (HelpNotificationUI.Instance != null)
        {
            HelpNotificationUI.Instance.ShowMessage(
                "RESCUE REPLAY COMPLETED - HELP SYSTEM READY"
            );
        }
    }
}
