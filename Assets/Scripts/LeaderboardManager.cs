using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

public class LeaderboardManager : MonoBehaviour
{
    [Header("Supabase")]
    [SerializeField] string supabaseReference;
    [SerializeField] string supabasePublishableKey;

    [Header("References")]
    [SerializeField] LapTimer lapTimer;
    [SerializeField] TextMeshProUGUI rankText;
    [SerializeField] AudioSource slamSource;
    [SerializeField] RacingHUD racingHUD;

    const string TABLE          = "leaderboard";
    const string PREF_PLAYER_ID = "lb_player_id";
    const string PREF_USERNAME  = "lb_username";
    const string ScrambleDigits = "0123456789";
    const float  ChangeInterval = 0.04f;
    const float  MinScramble    = 0.15f;

    string playerId;
    string supabaseUrl;

    bool rankFetched;
    bool uiReady;
    int  fetchedRank;

    void Awake()
    {
        supabaseUrl = $"https://{supabaseReference}.supabase.co";

        if (!PlayerPrefs.HasKey(PREF_PLAYER_ID))
            PlayerPrefs.SetString(PREF_PLAYER_ID, System.Guid.NewGuid().ToString("N"));
        playerId = PlayerPrefs.GetString(PREF_PLAYER_ID);
    }

    void OnEnable()  { if (lapTimer) lapTimer.OnSectorComplete += OnSectorComplete; }
    void OnDisable() { if (lapTimer) lapTimer.OnSectorComplete -= OnSectorComplete; }

    void OnSectorComplete(int sector, float split, float delta)
    {
        if (sector != 2) return;
        StartCoroutine(SubmitAndFetchRank(Mathf.RoundToInt(split * 1000f)));
    }

    IEnumerator SubmitAndFetchRank(int timeMs)
    {
        rankFetched = false;
        uiReady     = false;
        fetchedRank = 0;
        if (rankText) rankText.text = "";

        string username = PlayerPrefs.GetString(PREF_USERNAME, "destroh3");
        string json = $"{{\"player_id\":\"{playerId}\",\"time_ms\":{timeMs},\"username\":\"{username}\"}}";
        using (var req = new UnityWebRequest($"{supabaseUrl}/rest/v1/{TABLE}", "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            SetHeaders(req);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Leaderboard submit error: {req.error} | {req.downloadHandler.text}");
                fetchedRank = -1;
                rankFetched = true;
                TryShowRank();
                yield break;
            }
        }

        string countUrl = $"{supabaseUrl}/rest/v1/{TABLE}?time_ms=lt.{timeMs}&select=player_id";
        using (var countReq = UnityWebRequest.Get(countUrl))
        {
            SetHeaders(countReq);
            countReq.SetRequestHeader("Prefer", "count=exact");
            yield return countReq.SendWebRequest();

            fetchedRank = countReq.result == UnityWebRequest.Result.Success
                ? ParseRankFromHeader(countReq.GetResponseHeader("Content-Range")) + 1
                : -1;
        }

        rankFetched = true;
        TryShowRank();
    }

    // Called by RacingHUD after the two slam lines appear
    public void SignalUIReady()
    {
        uiReady = true;
        TryShowRank();
    }

    void TryShowRank()
    {
        if (uiReady)
            StartCoroutine(ScrambleRank());
    }

    IEnumerator ScrambleRank()
    {
        if (rankText == null) yield break;

        const string prefix = "Global Rank: #";
        // Show prefix immediately with placeholder digits
        rankText.text = prefix + "?";
        if (slamSource != null) slamSource.Play();

        float elapsed    = 0f;
        float nextChange = 0f;

        // Scramble for at least MinScramble, then keep going until rank is fetched
        while (elapsed < MinScramble || !rankFetched)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= nextChange)
            {
                nextChange += ChangeInterval;
                // Scramble 2 placeholder digits
                string digits = "" + ScrambleDigits[Random.Range(0, ScrambleDigits.Length)]
                                   + ScrambleDigits[Random.Range(0, ScrambleDigits.Length)];
                rankText.text = prefix + digits;
            }
            yield return null;
        }

        // Settle on final text
        if (fetchedRank > 0)
            rankText.text = prefix + fetchedRank;
        else
            rankText.text = "Rank unavailable";

        if (racingHUD != null) racingHUD.ShowRestartPrompt();
    }

    void SetHeaders(UnityWebRequest req)
    {
        req.SetRequestHeader("apikey",       supabasePublishableKey);
        req.SetRequestHeader("Content-Type", "application/json");
    }

    static int ParseRankFromHeader(string contentRange)
    {
        if (string.IsNullOrEmpty(contentRange)) return 0;
        int slash = contentRange.LastIndexOf('/');
        if (slash >= 0 && int.TryParse(contentRange.Substring(slash + 1), out int count))
            return count;
        return 0;
    }
}
