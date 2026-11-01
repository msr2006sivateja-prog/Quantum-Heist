
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
            activeRequesterId,
            responderId
        );
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

    // Called by SafeZone when a player enters its trigger.
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

        // Only the player who accepted HELP can start the rescue.
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
            $"RESCUE: Quantum travel started. " +
            $"Rescued player: {activeRequesterId}, " +
            $"Rescuer: {activeResponderId}"
        );

        serverInstance.NotifySafeZoneReachedClientRpc(playerId);

        // Pause the original banks' police on every connected client.
        QuantumReplayManager manager =
            QuantumReplayManager.Instance;

        serverInstance.PauseOriginalPoliceClientRpc(
            manager.Bank1Position,
            manager.Bank2Position,
            OriginalBankPoliceRadius
        );
    }

    [ClientRpc]
    private void PauseOriginalPoliceClientRpc(
        Vector3 bank1Position,
        Vector3 bank2Position,
        float radius)
    {
        PoliceAI[] policeOfficers =
            FindObjectsOfType<PoliceAI>();

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

        Debug.Log("RESCUE: Police in the original banks paused.");
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
