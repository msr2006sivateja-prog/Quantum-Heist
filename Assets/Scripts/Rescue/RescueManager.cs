
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

    public static bool ResponderReachedSafeZone =>
        responderReachedSafeZone;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            serverInstance = this;
            Debug.Log("RESCUE: Server manager spawned.");
        }

        Debug.Log(
            $"RESCUE: Manager spawned. IsServer={IsServer}, " +
            $"IsClient={IsClient}, IsOwner={IsOwner}, " +
            $"IsPlayerObject={NetworkObject.IsPlayerObject}"
        );
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

        // If this is a Player object, only its owner reads the keyboard.
        // If this is a scene object, every client can read its local keyboard.
        if (NetworkObject.IsPlayerObject && !IsOwner)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            Debug.Log("RESCUE INPUT: H pressed. Sending HELP request.");
            RequestHelpServerRpc();
        }

        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            Debug.Log("RESCUE INPUT: J pressed. Accepting HELP.");
            AcknowledgeHelpServerRpc();
        }

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {
            Debug.Log("RESCUE INPUT: K pressed. Cancelling HELP.");
            CancelHelpServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestHelpServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong requesterId = rpcParams.Receive.SenderClientId;

        Debug.Log($"RESCUE SERVER: HELP request received from {requesterId}.");

        if (lastHelpRequestTime.TryGetValue(
                requesterId, out float lastRequest))
        {
            if (Time.time - lastRequest < HelpCooldown)
            {
                Debug.LogWarning(
                    $"RESCUE: Player {requesterId} is on HELP cooldown."
                );
                return;
            }
        }

        if (activeRequesterId != NoRequester)
        {
            Debug.LogWarning(
                $"RESCUE: HELP request rejected. Player " +
                $"{activeRequesterId} already has an active request."
            );
            return;
        }

        activeRequesterId = requesterId;
        activeResponderId = NoRequester;
        responderReachedSafeZone = false;

        lastHelpRequestTime[requesterId] = Time.time;

        Debug.Log(
            $"RESCUE: HELP request accepted from Player {requesterId}."
        );

        NotifyHelpRequestedClientRpc(requesterId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AcknowledgeHelpServerRpc(
        ServerRpcParams rpcParams = default)
    {
        ulong responderId = rpcParams.Receive.SenderClientId;

        Debug.Log($"RESCUE SERVER: Accept request received from {responderId}.");

        if (activeRequesterId == NoRequester)
        {
            Debug.LogWarning("RESCUE: Nobody has requested HELP.");
            return;
        }

        if (responderId == activeRequesterId)
        {
            Debug.LogWarning("RESCUE: Requester cannot rescue themselves.");
            return;
        }

        if (activeResponderId != NoRequester)
        {
            Debug.LogWarning("RESCUE: A responder has already accepted.");
            return;
        }

        activeResponderId = responderId;
        responderReachedSafeZone = false;

        Debug.Log(
            $"RESCUE: Player {responderId} accepted Player " +
            $"{activeRequesterId}'s HELP request."
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

        Debug.Log($"RESCUE SERVER: Cancel request received from {requesterId}.");

        if (activeRequesterId != requesterId)
        {
            Debug.LogWarning(
                "RESCUE: Only the player who requested HELP can cancel it."
            );
            return;
        }

        ulong cancelledRequesterId = activeRequesterId;

        activeRequesterId = NoRequester;
        activeResponderId = NoRequester;
        responderReachedSafeZone = false;

        NotifyHelpCancelledClientRpc(cancelledRequesterId);
    }

    public static void NotifySafeZoneEntered(PlayerInventory inventory)
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

        if (playerId != activeResponderId)
            return;

        if (responderReachedSafeZone)
            return;

        if (QuantumReplayManager.Instance == null)
        {
            Debug.LogError(
                "RESCUE: QuantumReplayManager is missing from the scene."
            );
            return;
        }

        bool started =
            QuantumReplayManager.Instance.BeginHelpRescue(
                activeRequesterId,
                activeResponderId
            );

        if (!started)
        {
            Debug.LogError(
                "RESCUE: Quantum travel could not start. " +
                "Check the QuantumReplayManager Console errors."
            );
            return;
        }

        responderReachedSafeZone = true;

        Debug.Log(
            $"RESCUE: Quantum travel started. Rescued player: " +
            $"{activeRequesterId}, Rescuer: {activeResponderId}"
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
        PoliceAI[] policeOfficers = FindObjectsOfType<PoliceAI>();

        foreach (PoliceAI officer in policeOfficers)
        {
            if (officer == null)
                continue;

            float distanceToBank1 =
                Vector3.Distance(officer.transform.position, bank1Position);

            float distanceToBank2 =
                Vector3.Distance(officer.transform.position, bank2Position);

            if (distanceToBank1 <= radius ||
                distanceToBank2 <= radius)
            {
                officer.SetPaused(true);
            }
        }

        Debug.Log("RESCUE: Original-bank police paused.");
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
        else
        {
            Debug.LogError(
                "RESCUE UI: HelpNotificationUI.Instance is NULL on this client."
            );
        }
    }

    [ClientRpc]
    private void NotifyHelpAcknowledgedClientRpc(
        ulong requesterId,
        ulong responderId)
    {
        Debug.Log(
            $"RESCUE: Player {responderId} accepted " +
            $"Player {requesterId}'s HELP request."
        );

        if (HelpNotificationUI.Instance != null)
        {
            HelpNotificationUI.Instance.ShowPersistentStatus(
                $"PLAYER {responderId} ACCEPTED HELP - REACH THE SAFEZONE"
            );
        }
        else
        {
            Debug.LogError("RESCUE UI: HelpNotificationUI.Instance is NULL.");
        }
    }

    [ClientRpc]
    private void NotifySafeZoneReachedClientRpc(ulong responderId)
    {
        Debug.Log(
            $"RESCUE: Player {responderId} reached SafeZone. " +
            "Quantum travel started."
        );

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
        Debug.Log(
            $"RESCUE: Player {requesterId} cancelled their HELP request."
        );

        if (HelpNotificationUI.Instance != null)
            HelpNotificationUI.Instance.ClearStatus();
    }
}
