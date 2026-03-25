using UnityEngine;

public class LapTimer : MonoBehaviour
{
    [Header("References")]
    public MotorcyclePhysics physics;
    public MotorcycleInput   input;

    public enum State { Idle, Running, Finished }
    public State CurrentState { get; private set; } = State.Idle;

    // Static so best times survive scene reloads within the same session
    static float[] s_bestSplits    = new float[3];
    static bool[]  s_bestSplitsSet = new bool[3];

    float[] currentSplits   = new float[3];
    float   runStartTime;
    int     nextSector;         // 0 = waiting for S1, 1 = S2, 2 = Finish

    // Read by HUD
    public float   CurrentTime    => CurrentState == State.Running ? Time.time - runStartTime : (CurrentState == State.Finished ? currentSplits[2] : 0f);
    public float[] CurrentSplits  => currentSplits;
    public float[] BestSplits     => s_bestSplits;
    public bool[]  BestSplitsSet  => s_bestSplitsSet;
    public int     NextSector     => nextSector;

    // Events: (sectorIndex, splitTime, delta)
    public event System.Action<int, float, float> OnSectorComplete;

    void Update()
    {
        if (physics == null || input == null) return;

        // Invalidate run on crash or manual reset
        if (CurrentState == State.Running && (physics.IsCrashed || input.ResetBike))
        {
            CurrentState = State.Idle;
            return;
        }

        // Start new run from Idle or Finished on first grounded input
        if (CurrentState != State.Running)
        {
            bool anyInput = input.Throttle > 0.05f || input.FrontBrake > 0.05f || input.RearBrake > 0.05f;
            if (physics.IsGrounded && anyInput && !physics.IsCrashed)
                StartRun();
        }
    }

    void StartRun()
    {
        runStartTime  = Time.time;
        nextSector    = 0;
        CurrentState  = State.Running;
        System.Array.Clear(currentSplits, 0, 3);
    }

    public void OnCheckpointHit(int sectorIndex)
    {
        if (CurrentState != State.Running) return;
        if (sectorIndex != nextSector) return;   // must hit in order

        float split = Time.time - runStartTime;
        currentSplits[sectorIndex] = split;

        float delta = s_bestSplitsSet[sectorIndex]
            ? split - s_bestSplits[sectorIndex]
            : 0f;

        OnSectorComplete?.Invoke(sectorIndex, split, delta);

        if (sectorIndex == 2)
        {
            // Update bests with this completed run
            for (int i = 0; i < 3; i++)
            {
                if (!s_bestSplitsSet[i] || currentSplits[i] < s_bestSplits[i])
                {
                    s_bestSplits[i]    = currentSplits[i];
                    s_bestSplitsSet[i] = true;
                }
            }
            CurrentState = State.Finished;
        }
        else
        {
            nextSector++;
        }
    }

    public static string FormatTime(float t)
    {
        int   m = (int)(t / 60f);
        float s = t % 60f;
        return $"{m}:{s:00.000}";
    }

    public static string FormatDelta(float d)
    {
        return d >= 0f ? $"+{d:0.000}" : $"{d:0.000}";
    }
}
