
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class QuantumReplayManager : MonoBehaviour
{
    public static QuantumReplayManager Instance { get; private set; }

    [Header("Bank Origins")]
    [SerializeField] private Transform bank1;
    [SerializeField] private Transform bank2;
    [SerializeField] private Transform tempBank;

    [Header("Ghost")]
    [SerializeField] private GameObject ghostPlayerPrefab;

    public Vector3 Bank1Position =>
        bank1 != null ? bank1.position : Vector3.zero;

    public Vector3 Bank2Position =>
        bank2 != null ? bank2.position : Vector3.zero;

    private Vector3 savedReturnPosition;
    private Quaternion savedReturnRotation;

    private ulong activeRescuerId = ulong.MaxValue;
    private ulong activeRescuedId = ulong.MaxValue;

    private NetworkObject activeGhost;
    private bool rescueInProgress;

    private const float OriginalBankPoliceRadius = 150f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool BeginHelpRescue(
        ulong rescuedPlayerId,
        ulong rescuerPlayerId)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
            return false;

        if (rescueInProgress)
        {
            Debug.LogWarning("QUANTUM: A rescue is already in progress.");
            return false;
        }

        if (!ValidateReferences())
            return false;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            rescuedPlayerId, out var rescuedClient) ||
            rescuedClient.PlayerObject == null)
        {
            Debug.LogError("QUANTUM: Rescued player not found.");
            return false;
        }

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            rescuerPlayerId, out var rescuerClient) ||
            rescuerClient.PlayerObject == null)
        {
            Debug.LogError("QUANTUM: Rescuer not found.");
            return false;
        }

        if (!PlayerTimelineRecorder.TryGetRecorder(
            rescuedPlayerId, out PlayerTimelineRecorder recorder))
        {
            Debug.LogError("QUANTUM: Rescued player's recorder not found.");
            return false;
        }

        List<PlayerTimelineRecorder.TimelineFrame> frames =
            recorder.GetRecordedFrames();

        if (frames == null || frames.Count < 2)
        {
            Debug.LogWarning("QUANTUM: Not enough recorded frames.");
            return false;
        }

        Transform sourceBank =
            Vector3.Distance(recorder.transform.position, bank1.position) <
            Vector3.Distance(recorder.transform.position, bank2.position)
                ? bank1
                : bank2;

        if (!SpawnGhost(frames, sourceBank, rescuedPlayerId))
            return false;

        NetworkObject rescuedObject = rescuedClient.PlayerObject;
        NetworkObject rescuerObject = rescuerClient.PlayerObject;

        PlayerController rescuedController =
            rescuedObject.GetComponent<PlayerController>();

        if (rescuedController != null)
            rescuedController.SetGameplayPaused(true);

        PlayerTimelineRecorder rescuerRecorder =
            rescuerObject.GetComponent<PlayerTimelineRecorder>();

        if (rescuerRecorder != null)
            rescuerRecorder.SetRecordingEnabled(false);

        savedReturnPosition = rescuerObject.transform.position;
        savedReturnRotation = rescuerObject.transform.rotation;

        activeRescuerId = rescuerPlayerId;
        activeRescuedId = rescuedPlayerId;

        rescueInProgress = true;

        TeleportPlayer(
            rescuerObject,
            tempBank.position,
            tempBank.rotation
        );

        Debug.Log(
            $"QUANTUM: Rescuer {rescuerPlayerId} entered tempBank. " +
            $"Ghost replaying Player {rescuedPlayerId}."
        );

        return true;
    }

    private bool ValidateReferences()
    {
        if (bank1 == null || bank2 == null ||
            tempBank == null || ghostPlayerPrefab == null)
        {
            Debug.LogError(
                "QUANTUM: Assign bank1, bank2, tempBank and GhostPlayer."
            );
            return false;
        }

        return true;
    }

    private bool SpawnGhost(
        List<PlayerTimelineRecorder.TimelineFrame> frames,
        Transform sourceBank,
        ulong rescuedPlayerId)
    {
        var firstFrame = frames[0];

        Vector3 localPosition =
            sourceBank.InverseTransformPoint(firstFrame.position);

        Quaternion localRotation =
            Quaternion.Inverse(sourceBank.rotation) * firstFrame.rotation;

        Vector3 spawnPosition =
            tempBank.TransformPoint(localPosition);

        Quaternion spawnRotation =
            tempBank.rotation * localRotation;

        GameObject ghost = Instantiate(
            ghostPlayerPrefab,
            spawnPosition,
            spawnRotation
        );

        NetworkObject networkObject =
            ghost.GetComponent<NetworkObject>();

        PlayerGhostReplay replay =
            ghost.GetComponent<PlayerGhostReplay>();

        if (networkObject == null || replay == null)
        {
            Debug.LogError(
                "QUANTUM: GhostPlayer needs NetworkObject and PlayerGhostReplay."
            );

            Destroy(ghost);
            return false;
        }

        networkObject.Spawn();

        activeGhost = networkObject;

        replay.BeginReplay(frames, sourceBank, tempBank);

        Debug.Log(
            $"QUANTUM: Ghost spawned for Player {rescuedPlayerId}. " +
            $"Frames: {frames.Count}"
        );

        return true;
    }

    // Called by PlayerGhostReplay after the final frame.
    public void OnGhostReplayCompleted()
    {
        if (!NetworkManager.Singleton.IsServer || !rescueInProgress)
            return;

        Debug.Log("QUANTUM: Ghost replay finished. Returning rescuer.");

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            activeRescuerId, out var rescuerClient) ||
            rescuerClient.PlayerObject == null)
        {
            Debug.LogError("QUANTUM: Could not find rescuer to return.");
            return;
        }

        NetworkObject rescuerObject = rescuerClient.PlayerObject;

        TeleportPlayer(
            rescuerObject,
            savedReturnPosition,
            savedReturnRotation
        );

        PlayerTimelineRecorder rescuerRecorder =
            rescuerObject.GetComponent<PlayerTimelineRecorder>();

        if (rescuerRecorder != null)
            rescuerRecorder.SetRecordingEnabled(true);

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(
            activeRescuedId, out var rescuedClient) &&
            rescuedClient.PlayerObject != null)
        {
            PlayerController rescuedController =
                rescuedClient.PlayerObject.GetComponent<PlayerController>();

            if (rescuedController != null)
                rescuedController.SetGameplayPaused(false);
        }

        if (activeGhost != null && activeGhost.IsSpawned)
            activeGhost.Despawn(true);

        if (RescueManagerIsAvailable())
        {
            RescueManager manager = FindObjectOfType<RescueManager>();

            if (manager != null)
            {
                manager.ResumeOriginalPoliceClientRpc(
                    Bank1Position,
                    Bank2Position,
                    OriginalBankPoliceRadius
                );
            }
        }

        rescueInProgress = false;
        activeRescuerId = ulong.MaxValue;
        activeRescuedId = ulong.MaxValue;
        activeGhost = null;

        RescueManager.CompleteRescueOnServer();

        Debug.Log(
            "QUANTUM: Rescue session ended. Ready for another HELP request."
        );
    }

    private bool RescueManagerIsAvailable()
    {
        return FindObjectOfType<RescueManager>() != null;
    }

    private void TeleportPlayer(
        NetworkObject player,
        Vector3 position,
        Quaternion rotation)
    {
        if (player == null)
            return;

        CharacterController[] controllers =
            player.GetComponentsInChildren<CharacterController>(true);

        foreach (CharacterController cc in controllers)
            cc.enabled = false;

        player.transform.SetPositionAndRotation(position, rotation);

        foreach (CharacterController cc in controllers)
            cc.enabled = true;

        Debug.Log(
            $"QUANTUM: Player {player.OwnerClientId} teleported to {position}"
        );
    }

    [ContextMenu("Test Ghost Replay - Host Player")]
    public void TestHostReplay()
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("QUANTUM: Run this test on the Host.");
            return;
        }

        ulong hostId = NetworkManager.Singleton.LocalClientId;

        if (!PlayerTimelineRecorder.TryGetRecorder(
            hostId, out PlayerTimelineRecorder recorder))
        {
            Debug.LogWarning("QUANTUM: Host recorder not found.");
            return;
        }

        List<PlayerTimelineRecorder.TimelineFrame> frames =
            recorder.GetRecordedFrames();

        if (frames.Count < 2 || !ValidateReferences())
            return;

        Transform sourceBank =
            Vector3.Distance(recorder.transform.position, bank1.position) <
            Vector3.Distance(recorder.transform.position, bank2.position)
                ? bank1
                : bank2;

        SpawnGhost(frames, sourceBank, hostId);
    }
}
