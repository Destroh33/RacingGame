using System.Collections;
using UnityEngine;
using TMPro;

public class RacingHUD : MonoBehaviour
{
    [Header("References")]
    public LapTimer          lapTimer;
    public MotorcyclePhysics physics;

    [Header("Speed")]
    public TextMeshProUGUI speedText;

    [Header("Timer")]
    public TextMeshProUGUI timerText;

    [Header("Sectors")]
    public TextMeshProUGUI sector1Text;
    public TextMeshProUGUI sector2Text;
    public TextMeshProUGUI sector3Text;

    [Header("Finish Screen")]
    public GameObject      finishPanel;
    public TextMeshProUGUI finishTimeText;
    public TextMeshProUGUI finishDeltaText;
    [SerializeField] LeaderboardManager leaderboardManager;
    [SerializeField] AudioSource        slamSource;
    [SerializeField] float              finishDelay   = 0f;
    [SerializeField] float              lineSlamDelay = 0.15f;

    [Header("Restart Prompt")]
    [SerializeField] TextMeshProUGUI restartText;
    [SerializeField] float           restartFadeSpeed = 1.5f;

    [Header("Player")]
    [SerializeField] TextMeshProUGUI usernameText;

    // Per-sector cache — captured when event fires, before bests update
    readonly float[] cachedSplits      = new float[3];
    readonly float[] cachedDeltas      = new float[3];
    readonly bool[]  sectorCrossed     = new bool[3];
    readonly bool[]  sectorHadPrevBest = new bool[3];

    LapTimer.State prevLapState;
    bool           crashPromptShown;
    Coroutine      restartPulse;

    static readonly string Green = "#00DD66";
    static readonly string Red   = "#FF3333";

    void OnEnable()
    {
        if (lapTimer != null)
            lapTimer.OnSectorComplete += HandleSectorComplete;
        if (finishPanel != null)
            finishPanel.SetActive(false);
        if (restartText != null)
            SetRestartAlpha(0f);

        if (usernameText != null)
            usernameText.text = PlayerPrefs.GetString("lb_username", "destroh3");
    }

    void OnDisable()
    {
        if (lapTimer != null)
            lapTimer.OnSectorComplete -= HandleSectorComplete;
    }

    void Update()
    {
        if (lapTimer == null) return;

        bool wasRunning = prevLapState == LapTimer.State.Running;
        bool nowIdle    = lapTimer.CurrentState == LapTimer.State.Idle;

        // New run started — clear cached sector data and hide restart prompt
        if (lapTimer.CurrentState == LapTimer.State.Running && prevLapState == LapTimer.State.Idle)
        {
            System.Array.Clear(sectorCrossed,     0, 3);
            System.Array.Clear(cachedSplits,      0, 3);
            System.Array.Clear(cachedDeltas,      0, 3);
            System.Array.Clear(sectorHadPrevBest, 0, 3);
            crashPromptShown = false;
            HideRestartPrompt();
        }

        // Crash — show restart prompt immediately
        if (wasRunning && nowIdle && physics != null && physics.IsCrashed && !crashPromptShown)
        {
            crashPromptShown = true;
            ShowRestartPrompt();
        }

        prevLapState = lapTimer.CurrentState;

        UpdateSpeed();
        UpdateTimer();
        UpdateSectors();
    }

    // ── Speed ────────────────────────────────────────────────────────────────

    void UpdateSpeed()
    {
        if (speedText == null || physics == null) return;
        float kmh = Mathf.Abs(physics.CurrentSpeed) * 3.6f;
        if (kmh < 1f) kmh = 0f;
        speedText.text = $"{Mathf.FloorToInt(kmh)} <size=60%>km/h</size>";
    }

    // ── Timer ────────────────────────────────────────────────────────────────

    void UpdateTimer()
    {
        if (timerText == null) return;

        switch (lapTimer.CurrentState)
        {
            case LapTimer.State.Idle:
                timerText.text = "--:--.---";
                break;

            case LapTimer.State.Running:
            case LapTimer.State.Finished:
                timerText.text = LapTimer.FormatTime(lapTimer.CurrentTime);
                break;
        }
    }

    // ── Sector rows ──────────────────────────────────────────────────────────

    void UpdateSectors()
    {
        UpdateSectorRow(sector1Text, 0, "S1");
        UpdateSectorRow(sector2Text, 1, "S2");
        UpdateSectorRow(sector3Text, 2, "FIN");
    }

    void UpdateSectorRow(TextMeshProUGUI text, int s, string label)
    {
        if (text == null) return;

        if (sectorCrossed[s])
        {
            // Always show the cached value — never recalculate so bests updating can't change it
            string timeStr = LapTimer.FormatTime(cachedSplits[s]);
            if (sectorHadPrevBest[s])
            {
                string col = cachedDeltas[s] <= 0f ? Green : Red;
                text.text  = $"{label}  {timeStr}  <color={col}>{LapTimer.FormatDelta(cachedDeltas[s])}</color>";
            }
            else
            {
                text.text = $"{label}  {timeStr}";
            }
        }
        else
        {
            bool isActive = lapTimer.CurrentState == LapTimer.State.Running && s == lapTimer.NextSector;
            if (isActive)
            {
                string timeStr = lapTimer.BestSplitsSet[s]
                    ? LapTimer.FormatTime(lapTimer.BestSplits[s])
                    : "--:--.---";
                text.text = $"<color=#FFD700>{label}  {timeStr}</color>";
            }
            else if (lapTimer.BestSplitsSet[s])
            {
                text.text = $"<color=#AAAAAA>{label}  {LapTimer.FormatTime(lapTimer.BestSplits[s])}</color>";
            }
            else
            {
                text.text = $"<color=#AAAAAA>{label}  --:--.---</color>";
            }
        }
    }

    // ── Events ───────────────────────────────────────────────────────────────

    void HandleSectorComplete(int sector, float split, float delta)
    {
        cachedSplits[sector]      = split;
        cachedDeltas[sector]      = delta;
        sectorCrossed[sector]     = true;
        sectorHadPrevBest[sector] = lapTimer.BestSplitsSet[sector];

        if (sector == 2)
            ShowFinishScreen(split, delta);
    }

    void ShowFinishScreen(float split, float delta)
    {
        if (finishPanel == null) return;
        finishPanel.SetActive(true);

        if (finishTimeText)  finishTimeText.text  = "";
        if (finishDeltaText) finishDeltaText.text = "";

        string oldBestRich = lapTimer.BestSplitsSet[2]
            ? $"Old Best:  {LapTimer.FormatTime(lapTimer.BestSplits[2])}"
            : "Old Best:  --:--.---";

        string thisRunRich;
        if (lapTimer.BestSplitsSet[2])
        {
            string col = delta <= 0f ? Green : Red;
            thisRunRich = $"This Run:  <color={col}>{LapTimer.FormatTime(split)}</color>";
        }
        else
        {
            thisRunRich = $"This Run:  {LapTimer.FormatTime(split)}";
        }

        StartCoroutine(RunFinishSequence(oldBestRich, thisRunRich));
    }

    IEnumerator RunFinishSequence(string oldBestRich, string thisRunRich)
    {
        if (finishDelay > 0f) yield return new WaitForSeconds(finishDelay);

        if (finishTimeText) finishTimeText.text = oldBestRich;
        PlaySlam();
        yield return new WaitForSeconds(lineSlamDelay);

        if (finishDeltaText) finishDeltaText.text = thisRunRich;
        PlaySlam();
        yield return new WaitForSeconds(lineSlamDelay);

        leaderboardManager?.SignalUIReady();
    }

    // Called by LeaderboardManager after rank is displayed
    public void ShowRestartPrompt()
    {
        if (restartText == null) return;
        if (restartPulse != null) StopCoroutine(restartPulse);
        restartPulse = StartCoroutine(PulseRestart());
    }

    void HideRestartPrompt()
    {
        if (restartPulse != null) { StopCoroutine(restartPulse); restartPulse = null; }
        SetRestartAlpha(0f);
    }

    IEnumerator PulseRestart()
    {
        // Fade in
        float alpha = 0f;
        while (alpha < 1f)
        {
            alpha = Mathf.MoveTowards(alpha, 1f, restartFadeSpeed * Time.deltaTime);
            SetRestartAlpha(alpha);
            yield return null;
        }

        // Pulse in/out indefinitely
        while (true)
        {
            alpha = Mathf.MoveTowards(alpha, 0f, restartFadeSpeed * Time.deltaTime);
            SetRestartAlpha(alpha);
            if (alpha <= 0f)
            {
                yield return new WaitForSeconds(0.1f);
                while (alpha < 1f)
                {
                    alpha = Mathf.MoveTowards(alpha, 1f, restartFadeSpeed * Time.deltaTime);
                    SetRestartAlpha(alpha);
                    yield return null;
                }
                yield return new WaitForSeconds(0.1f);
            }
            yield return null;
        }
    }

    void SetRestartAlpha(float alpha)
    {
        if (restartText == null) return;
        Color c = restartText.color;
        c.a = alpha;
        restartText.color = c;
    }

    void PlaySlam()
    {
        if (slamSource != null) slamSource.Play();
    }
}
