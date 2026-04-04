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

    // Per-sector cache — captured when event fires, before bests update
    readonly float[] cachedSplits      = new float[3];
    readonly float[] cachedDeltas      = new float[3];
    readonly bool[]  sectorCrossed     = new bool[3];
    readonly bool[]  sectorHadPrevBest = new bool[3];

    LapTimer.State prevLapState;

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
        if (lapTimer == null) return;

        // New run started — clear cached sector data
        if (lapTimer.CurrentState == LapTimer.State.Running && prevLapState == LapTimer.State.Idle)
        {
            System.Array.Clear(sectorCrossed,     0, 3);
            System.Array.Clear(cachedSplits,      0, 3);
            System.Array.Clear(cachedDeltas,      0, 3);
            System.Array.Clear(sectorHadPrevBest, 0, 3);
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

    void PlaySlam()
    {
        if (slamSource != null) slamSource.Play();
    }
}
