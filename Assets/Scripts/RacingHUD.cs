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

    // Cached at the moment the finish event fires (before bests update)
    bool  finishCached;
    float cachedFinishSplit;
    float cachedFinishDelta;
    bool  finishHadPrevBest;

    static readonly string Green = "#00DD66";
    static readonly string Red   = "#FF3333";

    void OnEnable()
    {
        if (lapTimer != null)
            lapTimer.OnSectorComplete += HandleSectorComplete;
        if (finishPanel != null)
            finishPanel.SetActive(false);
    }

    void OnDisable()
    {
        if (lapTimer != null)
            lapTimer.OnSectorComplete -= HandleSectorComplete;
    }

    void Update()
    {
        UpdateSpeed();
        UpdateTimer();
        UpdateSectors();
    }

    // ── Speed ────────────────────────────────────────────────────────────────

    void UpdateSpeed()
    {
        if (speedText == null || physics == null) return;
        float kmh = Mathf.Abs(physics.CurrentSpeed) * 3.6f;
        speedText.text = $"{kmh:0} <size=60%>km/h</size>";
    }

    // ── Timer ────────────────────────────────────────────────────────────────

    void UpdateTimer()
    {
        if (timerText == null || lapTimer == null) return;

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
        if (lapTimer == null) return;

        UpdateSectorRow(sector1Text, 0, "S1");
        UpdateSectorRow(sector2Text, 1, "S2");

        if (lapTimer.CurrentState == LapTimer.State.Finished && finishCached)
            UpdateFinishedFINRow();
        else
            UpdateSectorRow(sector3Text, 2, "FIN");
    }

    void UpdateFinishedFINRow()
    {
        if (sector3Text == null) return;
        string timeStr = LapTimer.FormatTime(cachedFinishSplit);
        if (finishHadPrevBest)
        {
            string col       = cachedFinishDelta <= 0f ? Green : Red;
            sector3Text.text = $"FIN  {timeStr}  <color={col}>{LapTimer.FormatDelta(cachedFinishDelta)}</color>";
        }
        else
        {
            sector3Text.text = $"FIN  {timeStr}";
        }
    }

    void UpdateSectorRow(TextMeshProUGUI text, int s, string label)
    {
        if (text == null || lapTimer == null) return;

        bool running  = lapTimer.CurrentState != LapTimer.State.Idle;
        bool crossed  = running && lapTimer.CurrentSplits[s] > 0f;

        if (crossed)
        {
            string timeStr = LapTimer.FormatTime(lapTimer.CurrentSplits[s]);

            if (lapTimer.BestSplitsSet[s])
            {
                float  d   = lapTimer.CurrentSplits[s] - lapTimer.BestSplits[s];
                string col = d <= 0f ? Green : Red;
                text.text  = $"{label}  {timeStr}  <color={col}>{LapTimer.FormatDelta(d)}</color>";
            }
            else
            {
                text.text = $"{label}  {timeStr}";
            }
        }
        else if (lapTimer.BestSplitsSet[s])
        {
            string dim = lapTimer.CurrentState == LapTimer.State.Running && s == lapTimer.NextSector
                ? "FFFFFF" : "AAAAAA";
            text.text = $"<color=#{dim}>{label}  {LapTimer.FormatTime(lapTimer.BestSplits[s])}</color>";
        }
        else
        {
            text.text = $"<color=#AAAAAA>{label}  --:--.---</color>";
        }
    }

    void HandleSectorComplete(int sector, float split, float delta)
    {
        if (sector == 2)
        {
            // Capture before bests update
            finishCached      = true;
            cachedFinishSplit = split;
            cachedFinishDelta = delta;
            finishHadPrevBest = lapTimer.BestSplitsSet[2];

            ShowFinishScreen(split, delta);
        }
    }

    void ShowFinishScreen(float split, float delta)
    {
        if (finishPanel == null) return;

        finishPanel.SetActive(true);

        // Top: previous best (bests haven't updated yet when this fires)
        if (finishTimeText != null)
        {
            finishTimeText.text = lapTimer.BestSplitsSet[2]
                ? $"Old Best:  {LapTimer.FormatTime(lapTimer.BestSplits[2])}"
                : "Old Best:  --:--.---";
        }

        // Bottom: this run's time colored green/red if there was a previous best
        if (finishDeltaText != null)
        {
            if (lapTimer.BestSplitsSet[2])
            {
                string col           = delta <= 0f ? Green : Red;
                finishDeltaText.text = $"This Run:  <color={col}>{LapTimer.FormatTime(split)}</color>";
            }
            else
            {
                finishDeltaText.text = $"This Run:  {LapTimer.FormatTime(split)}";
            }
        }
    }

}
