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

    [Header("Delta Flash")]
    public TextMeshProUGUI deltaText;
    public float           deltaDisplayDuration = 3f;

    float deltaTimer;

    static readonly string Green = "#00DD66";
    static readonly string Red   = "#FF3333";

    void OnEnable()
    {
        if (lapTimer != null)
            lapTimer.OnSectorComplete += HandleSectorComplete;
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
        TickDeltaFlash();
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
                timerText.text = lapTimer.BestSplitsSet[2]
                    ? $"Best  {LapTimer.FormatTime(lapTimer.BestSplits[2])}"
                    : "--:--.---";
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
        if (text == null || lapTimer == null) return;

        bool running  = lapTimer.CurrentState != LapTimer.State.Idle;
        bool crossed  = running && lapTimer.CurrentSplits[s] > 0f;

        if (crossed)
        {
            // Show this run's split and delta vs best
            string timeStr = LapTimer.FormatTime(lapTimer.CurrentSplits[s]);

            if (lapTimer.BestSplitsSet[s])
            {
                float  d     = lapTimer.CurrentSplits[s] - lapTimer.BestSplits[s];
                string col   = d <= 0f ? Green : Red;
                text.text    = $"{label}  {timeStr}  <color={col}>{LapTimer.FormatDelta(d)}</color>";
            }
            else
            {
                text.text = $"{label}  {timeStr}";
            }
        }
        else if (lapTimer.BestSplitsSet[s])
        {
            // Waiting to cross — show best as reference
            string dim = lapTimer.CurrentState == LapTimer.State.Running && s == lapTimer.NextSector
                ? "FFFFFF" : "888888";
            text.text = $"<color=#{dim}>{label}  {LapTimer.FormatTime(lapTimer.BestSplits[s])}</color>";
        }
        else
        {
            text.text = $"<color=#555555>{label}  --:--.---</color>";
        }
    }

    // ── Delta flash ──────────────────────────────────────────────────────────

    void HandleSectorComplete(int sector, float split, float delta)
    {
        if (deltaText == null) return;

        string label = sector == 2 ? "FINISH" : $"SECTOR {sector + 1}";

        if (lapTimer.BestSplitsSet[sector] && sector < 2)
        {
            // Comparing against previous best (bests update only on finish)
            string col    = delta <= 0f ? Green : Red;
            deltaText.text = $"{label}\n{LapTimer.FormatTime(split)}\n<color={col}>{LapTimer.FormatDelta(delta)}</color>";
        }
        else if (sector == 2 && lapTimer.BestSplitsSet[2])
        {
            string col    = delta <= 0f ? Green : Red;
            deltaText.text = $"{label}\n{LapTimer.FormatTime(split)}\n<color={col}>{LapTimer.FormatDelta(delta)}</color>";
        }
        else
        {
            deltaText.text = $"{label}\n{LapTimer.FormatTime(split)}";
        }

        deltaText.alpha = 1f;
        deltaTimer      = deltaDisplayDuration;
    }

    void TickDeltaFlash()
    {
        if (deltaText == null || deltaTimer <= 0f) return;
        deltaTimer     -= Time.deltaTime;
        deltaText.alpha = deltaTimer < 0.5f ? deltaTimer / 0.5f : 1f;
        if (deltaTimer <= 0f) deltaText.alpha = 0f;
    }
}
