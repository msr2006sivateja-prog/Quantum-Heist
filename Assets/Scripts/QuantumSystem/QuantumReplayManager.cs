
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
    private bool rescueInProgress;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Called by the HELP system when the responder reaches SafeZone.
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
            Debug.LogError("QUANTUM: Could not find rescued player's object.");
            return false;
        }

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
                rescuerPlayerId, out var rescuerClient) ||
            rescuerClient.PlayerObject == null)
        {
            Debug.LogError("QUANTUM: Could not find rescuer's object.");
            return false;
        }

        if (!PlayerTimelineRecorder.TryGetRecorder(
                rescuedPlayerId, out PlayerTimelineRecorder recorder))
        {
            Debug.LogError("QUANTUM: Rescued player has no timeline recorder.");
            return false;
        }

        List<PlayerTimelineRecorder.TimelineFrame> frames =
            recorder.GetRecordedFrames();

        if (frames == null || frames.Count < 2)
        {
            Debug.LogWarning(
                "QUANTUM: Not enough recorded frames. " +
                "Let the player move for a little longer first."
            );
            return false;
        }

        Transform sourceBank =
            Vector3.Distance(recorder.transform.position, bank1.position) <
            Vector3.Distance(recorder.transform.position, bank2.position)
                ? bank1
                : bank2;

        // Spawn the ghost before moving the rescuer.
        if (!SpawnGhost(frames, sourceBank, rescuedPlayerId))
            return false;

        NetworkObject rescuedObject = rescuedClient.PlayerObject;
        NetworkObject rescuerObject = rescuerClient.PlayerObject;

        // Freeze the rescued player in their original dimension.
        PlayerController rescuedController =
            rescuedObject.GetComponent<PlayerController>();

        if (rescuedController != null)
            rescuedController.SetGameplayPaused(true);

        // Stop recording the rescuer while they are in tempBank.
        PlayerTimelineRecorder rescuerRecorder =
            rescuerObject.GetComponent<PlayerTimelineRecorder>();

        if (rescuerRecorder != null)
            rescuerRecorder.SetRecordingEnabled(false);

        // Save the rescuer's original location for the return stage.
        savedReturnPosition = rescuerObject.transform.position;
        savedReturnRotation = rescuerObject.transform.rotation;
        activeRescuerId = rescuerPlayerId;

        // Move the rescuer to the temporary bank.
        TeleportPlayer(
            rescuerObject,
            tempBank.position,
            tempBank.rotation
        );

        rescueInProgress = true;

        Debug.Log(
            $"QUANTUM: Rescuer {rescuerPlayerId} teleported to tempBank. " +
            $"Ghost replaying rescued player {rescuedPlayerId}."
        );

        return true;
    }

    private bool ValidateReferences()
    {
        if (bank1 == null || bank2 == null ||
            tempBank == null || ghostPlayerPrefab == null)
        {
            Debug.LogError(
                "QUANTUM: Assign bank1, bank2, tempBank and GhostPlayer prefab."
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
                "QUANTUM: GhostPlayer prefab needs NetworkObject " +
                "and PlayerGhostReplay."
            );

            Destroy(ghost);
            return false;
        }

        // Server spawns it so connected clients can observe it.
        networkObject.Spawn();

        replay.BeginReplay(frames, sourceBank, tempBank);

        Debug.Log(
            $"QUANTUM: Ghost spawned for Player {rescuedPlayerId}. " +
            $"Frames: {frames.Count}"
        );

        return true;
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

        foreach (CharacterController characterController in controllers)
            characterController.enabled = false;

        player.transform.SetPositionAndRotation(position, rotation);

        foreach (CharacterController characterController in controllers)
            characterController.enabled = true;

        Debug.Log(
            $"QUANTUM: Player {player.OwnerClientId} teleported to {position}"
        );
    }

    // Keep the old manual host test available.
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

        var frames = recorder.GetRecordedFrames();

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
