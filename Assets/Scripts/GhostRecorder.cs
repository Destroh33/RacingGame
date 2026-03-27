using System.Collections.Generic;
using UnityEngine;

public class GhostRecorder : MonoBehaviour
{
    [Header("References")]
    public LapTimer          lapTimer;
    public MotorcyclePhysics physics;     // bikeModel pulled from here automatically
    public RiderLean         riderLean;   // the RiderLean component on the player rider
    public GhostPlayback     ghostPlayback;

    [Header("Recording")]
    [Tooltip("Samples per second — 30 is plenty")]
    public float sampleRate = 30f;

    List<GhostPlayback.GhostFrame> currentRecording = new();
    static List<GhostPlayback.GhostFrame> s_bestRecording;
    static float   s_bestLapTime = float.MaxValue;
    bool           recording;
    float          recordStart;
    float          nextSampleTime;
    LapTimer.State prevState;

    void OnEnable()
    {
        if (lapTimer != null) lapTimer.OnSectorComplete += OnSectorComplete;
    }

    void OnDisable()
    {
        if (lapTimer != null) lapTimer.OnSectorComplete -= OnSectorComplete;
    }

    void Update()
    {
        if (lapTimer == null) return;

        LapTimer.State state = lapTimer.CurrentState;

        // Run just started
        if (prevState != LapTimer.State.Running && state == LapTimer.State.Running)
            BeginRecording();

        // Run invalidated (crash / reset) before finish
        if (prevState == LapTimer.State.Running && state == LapTimer.State.Idle)
        {
            recording = false;
            ghostPlayback?.StopPlayback();
        }

        prevState = state;

        if (recording && Time.time >= nextSampleTime)
        {
            Sample();
            nextSampleTime += 1f / sampleRate;
        }
    }

    void BeginRecording()
    {
        recording      = true;
        recordStart    = Time.time;
        nextSampleTime = Time.time;
        currentRecording.Clear();

        if (s_bestRecording != null && s_bestRecording.Count > 1)
            ghostPlayback?.StartPlayback(s_bestRecording);
        else
            ghostPlayback?.StopPlayback();
    }

    void Sample()
    {
        Transform model    = physics    != null ? physics.bikeModel : null;
        Quaternion riderRot = riderLean != null
            ? Quaternion.Euler(0f, 0f, -riderLean.CurrentLean)
            : Quaternion.identity;

        //Debug.Log($"[GhostRecorder] riderLean={riderLean?.name ?? "NULL"} riderRot={riderRot.eulerAngles}");
        currentRecording.Add(new GhostPlayback.GhostFrame
        {
            time                = Time.time - recordStart,
            position            = transform.position,
            rotation            = transform.rotation,
            modelLocalRotation  = model != null ? model.localRotation : Quaternion.identity,
            riderLocalRotation  = riderRot
        });
    }

    void OnSectorComplete(int sector, float splitTime, float delta)
    {
        if (sector != 2) return;   // only care about finish line

        recording = false;

        if (splitTime < s_bestLapTime)
        {
            s_bestLapTime    = splitTime;
            s_bestRecording  = new List<GhostPlayback.GhostFrame>(currentRecording);
        }
    }
}
