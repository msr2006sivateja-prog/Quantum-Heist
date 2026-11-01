
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerTimelineRecorder : NetworkBehaviour
{
    [Header("Recording Settings")]
    [SerializeField] private float recordingDuration = 300f;
    [SerializeField] private float recordingInterval = 0.1f;
    [SerializeField] private bool recordingEnabled = true;

    private readonly Queue<TimelineFrame> frames =
        new Queue<TimelineFrame>();

    private static readonly Dictionary<ulong, PlayerTimelineRecorder>
        recorders = new Dictionary<ulong, PlayerTimelineRecorder>();

    private double nextRecordTime;

    public void SetRecordingEnabled(bool enabled)
    {
        if (!IsServer) return;

        recordingEnabled = enabled;
        Debug.Log($"TIMELINE: Recording {(enabled ? "enabled" : "paused")} for Player {OwnerClientId}");
    }

    public struct TimelineFrame
    {
        public double timestamp;
        public Vector3 position;
        public Quaternion rotation;

        public TimelineFrame(
            double timestamp,
            Vector3 position,
            Quaternion rotation)
        {
            this.timestamp = timestamp;
            this.position = position;
            this.rotation = rotation;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        recorders[OwnerClientId] = this;

        nextRecordTime = Time.timeAsDouble;

        Debug.Log(
            $"TIMELINE: Recording started for Player {OwnerClientId}");
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
            return;

        if (recorders.TryGetValue(
                OwnerClientId, out PlayerTimelineRecorder recorder)
            && recorder == this)
        {
            recorders.Remove(OwnerClientId);
        }

        frames.Clear();
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || !recordingEnabled) return;

        double currentTime = Time.timeAsDouble;

        if (currentTime < nextRecordTime) return;

        RecordFrame(currentTime);
        nextRecordTime = currentTime + recordingInterval;
    }

    private void RecordFrame(double timestamp)
    {
        frames.Enqueue(new TimelineFrame(
            timestamp,
            transform.position,
            transform.rotation
        ));

        while (frames.Count > 0)
        {
            TimelineFrame oldest = frames.Peek();

            if (timestamp - oldest.timestamp <= recordingDuration)
                break;

            frames.Dequeue();
        }

        Debug.Log(
            $"TIMELINE: Position = {transform.position} | Frames = {frames.Count}"
        );
    }

    public static bool TryGetRecorder(
        ulong playerId,
        out PlayerTimelineRecorder recorder)
    {
        return recorders.TryGetValue(playerId, out recorder);
    }

    public List<TimelineFrame> GetRecordedFrames()
    {
        return new List<TimelineFrame>(frames);
    }

    public double GetRecordedDuration()
    {
        if (frames.Count < 2)
            return 0;

        TimelineFrame[] recorded = frames.ToArray();

        return recorded[recorded.Length - 1].timestamp
             - recorded[0].timestamp;
    }

    public int GetFrameCount()
    {
        return frames.Count;
    }
}
