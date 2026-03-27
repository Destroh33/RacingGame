using System.Collections.Generic;
using UnityEngine;

public class GhostPlayback : MonoBehaviour
{
    [Header("References")]
    public Transform ghostBikeModel;   // BikeHolder equivalent on the ghost (lean/pitch)
    public Transform ghostRider;       // rider transform on the ghost

    public struct GhostFrame
    {
        public float      time;
        public Vector3    position;
        public Quaternion rotation;
        public Quaternion modelLocalRotation;
        public Quaternion riderLocalRotation;
    }

    List<GhostFrame> frames;
    float            playbackStart;
    int              frameIndex;
    bool             playing;

    public void StartPlayback(List<GhostFrame> recording)
    {
        frames        = recording;
        playbackStart = Time.time;
        frameIndex    = 0;
        playing       = frames != null && frames.Count > 1;
        gameObject.SetActive(playing);
    }

    public void StopPlayback()
    {
        playing = false;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!playing || frames == null) return;

        float elapsed = Time.time - playbackStart;

        // Advance to the correct pair of frames
        while (frameIndex < frames.Count - 2 && frames[frameIndex + 1].time <= elapsed)
            frameIndex++;

        if (frameIndex >= frames.Count - 1)
        {
            StopPlayback();
            return;
        }

        GhostFrame a = frames[frameIndex];
        GhostFrame b = frames[frameIndex + 1];
        float t = Mathf.InverseLerp(a.time, b.time, elapsed);

        transform.SetPositionAndRotation(
            Vector3.Lerp(a.position, b.position, t),
            Quaternion.Slerp(a.rotation, b.rotation, t)
        );

        if (ghostBikeModel != null)
            ghostBikeModel.localRotation = Quaternion.Slerp(a.modelLocalRotation, b.modelLocalRotation, t);

        if (ghostRider != null)
            ghostRider.localRotation = Quaternion.Slerp(a.riderLocalRotation, b.riderLocalRotation, t);
    }
}
