
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerGhostReplay : NetworkBehaviour
{
    private List<PlayerTimelineRecorder.TimelineFrame> frames;
    private Transform sourceBank;
    private Transform temporaryBank;

    private double replayStartTime;
    private int currentFrame;
    private bool replaying;

    public void BeginReplay(
        List<PlayerTimelineRecorder.TimelineFrame> recordedFrames,
        Transform originalBank,
        Transform tempBank)
    {
        if (!IsServer ||
            recordedFrames == null ||
            recordedFrames.Count < 2)
            return;

        frames = recordedFrames;
        sourceBank = originalBank;
        temporaryBank = tempBank;

        currentFrame = 0;
        replayStartTime = Time.timeAsDouble;
        replaying = true;

        Debug.Log($"GHOST: Replay started. Frames: {frames.Count}");
    }

    private void Update()
    {
        if (!IsServer || !replaying)
            return;

        double elapsed = Time.timeAsDouble - replayStartTime;

        double targetTimestamp =
            frames[0].timestamp + elapsed;

        while (currentFrame < frames.Count - 2 &&
               frames[currentFrame + 1].timestamp <= targetTimestamp)
        {
            currentFrame++;
        }

        if (targetTimestamp >= frames[frames.Count - 1].timestamp)
        {
            ApplyFrame(frames[frames.Count - 1]);

            replaying = false;

            Debug.Log("GHOST: Replay completed.");

            if (QuantumReplayManager.Instance != null)
            {
                QuantumReplayManager.Instance.OnGhostReplayCompleted();
            }

            return;
        }

        var first = frames[currentFrame];
        var second = frames[currentFrame + 1];

        float duration =
            (float)(second.timestamp - first.timestamp);

        float t = duration > 0f
            ? Mathf.Clamp01(
                (float)(targetTimestamp - first.timestamp) / duration
            )
            : 0f;

        Vector3 localPosition = Vector3.Lerp(
            sourceBank.InverseTransformPoint(first.position),
            sourceBank.InverseTransformPoint(second.position),
            t
        );

        Quaternion firstRotation =
            Quaternion.Inverse(sourceBank.rotation) * first.rotation;

        Quaternion secondRotation =
            Quaternion.Inverse(sourceBank.rotation) * second.rotation;

        Quaternion localRotation =
            Quaternion.Slerp(firstRotation, secondRotation, t);

        transform.position =
            temporaryBank.TransformPoint(localPosition);

        transform.rotation =
            temporaryBank.rotation * localRotation;
    }

    private void ApplyFrame(
        PlayerTimelineRecorder.TimelineFrame frame)
    {
        Vector3 localPosition =
            sourceBank.InverseTransformPoint(frame.position);

        Quaternion localRotation =
            Quaternion.Inverse(sourceBank.rotation) * frame.rotation;

        transform.position =
            temporaryBank.TransformPoint(localPosition);

        transform.rotation =
            temporaryBank.rotation * localRotation;
    }
}
