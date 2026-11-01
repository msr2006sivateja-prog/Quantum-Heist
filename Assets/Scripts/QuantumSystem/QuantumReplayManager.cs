
using UnityEngine;
using Unity.Netcode;

public class QuantumReplayManager : MonoBehaviour
{
    [Header("Bank Origins")]
    [SerializeField] private Transform bank1;
    [SerializeField] private Transform bank2;
    [SerializeField] private Transform tempBank;

    [Header("Ghost")]
    [SerializeField] private GameObject ghostPlayerPrefab;

    // Temporary test: replays the Host player's recording.
    [ContextMenu("Test Ghost Replay - Host Player")]
    public void TestHostReplay()
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("QUANTUM: Run this test on the Host.");
            return;
        }

        ulong playerId = NetworkManager.Singleton.LocalClientId;
        StartReplay(playerId);
    }

    public void StartReplay(ulong rescuedPlayerId)
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        if (bank1 == null || bank2 == null ||
            tempBank == null || ghostPlayerPrefab == null)
        {
            Debug.LogError("QUANTUM: Assign all bank references and Ghost prefab.");
            return;
        }

        if (!PlayerTimelineRecorder.TryGetRecorder(
                rescuedPlayerId, out PlayerTimelineRecorder recorder))
        {
            Debug.LogError("QUANTUM: No recorder found for this player.");
            return;
        }

        var frames = recorder.GetRecordedFrames();

        if (frames.Count < 2)
        {
            Debug.LogWarning("QUANTUM: Not enough recorded frames yet.");
            return;
        }

        // Find which original bank contains the rescued player.
        Transform sourceBank =
            Vector3.Distance(recorder.transform.position, bank1.position) <
            Vector3.Distance(recorder.transform.position, bank2.position)
                ? bank1
                : bank2;

        var firstFrame = frames[0];

        Vector3 localPosition =
            sourceBank.InverseTransformPoint(firstFrame.position);

        Quaternion localRotation =
            Quaternion.Inverse(sourceBank.rotation) * firstFrame.rotation;

        Vector3 spawnPosition = tempBank.TransformPoint(localPosition);
        Quaternion spawnRotation = tempBank.rotation * localRotation;

        GameObject ghost = Instantiate(
            ghostPlayerPrefab,
            spawnPosition,
            spawnRotation
        );

        NetworkObject networkObject =
            ghost.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError("QUANTUM: Ghost prefab needs a NetworkObject.");
            Destroy(ghost);
            return;
        }

        networkObject.Spawn();

        PlayerGhostReplay replay =
            ghost.GetComponent<PlayerGhostReplay>();

        if (replay == null)
        {
            Debug.LogError("QUANTUM: Ghost prefab needs PlayerGhostReplay.");
            networkObject.Despawn(true);
            return;
        }

        replay.BeginReplay(frames, sourceBank, tempBank);

        Debug.Log(
            $"QUANTUM: Ghost spawned for Player {rescuedPlayerId}. " +
            $"Frames: {frames.Count}"
        );
    }
}
