using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Supabase")]
    [SerializeField] string supabaseReference;
    [SerializeField] string supabasePublishableKey;

    [Header("UI")]
    [SerializeField] TMP_InputField        usernameInput;
    [SerializeField] Button                playButton;
    [SerializeField] TextMeshProUGUI       leaderboardText;
    [SerializeField] string                gameSceneName = "Game";

    [Header("Music")]
    [SerializeField] AudioClip musicClip;
    [SerializeField] float     musicVolume = 1f;

    const string PREF_USERNAME  = "lb_username";
    const string PREF_PLAYER_ID = "lb_player_id";
    const string TABLE          = "leaderboard";

    string supabaseUrl;

    void Awake()
    {
        supabaseUrl = $"https://{supabaseReference}.supabase.co";

        if (!PlayerPrefs.HasKey(PREF_PLAYER_ID))
            PlayerPrefs.SetString(PREF_PLAYER_ID, System.Guid.NewGuid().ToString("N"));
    }

    void Start()
    {
        if (MusicManager.Instance != null && musicClip != null)
            MusicManager.Instance.Play(musicClip, musicVolume);

        if (PlayerPrefs.HasKey(PREF_USERNAME))
            usernameInput.text = PlayerPrefs.GetString(PREF_USERNAME);

        playButton.onClick.AddListener(OnPlayClicked);
        StartCoroutine(FetchLeaderboard());
    }

    void OnPlayClicked()
    {
        StartCoroutine(ResolveUsernameAndPlay());
    }

    IEnumerator ResolveUsernameAndPlay()
    {
        playButton.interactable = false;

        string input = SanitizeUsername(usernameInput.text);
        if (string.IsNullOrEmpty(input))
        {
            string guest = "";
            yield return StartCoroutine(GenerateGuestUsername(result => guest = result));
            input = guest;
        }

        PlayerPrefs.SetString(PREF_USERNAME, input);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameSceneName);
    }

    static string SanitizeUsername(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        var sb = new StringBuilder();
        foreach (char c in raw)
        {
            // Allow letters, digits, underscores, hyphens only
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                sb.Append(c);
        }

        string result = sb.ToString().Trim();
        if (result.Length > 15) result = result.Substring(0, 15);
        return result;
    }

    IEnumerator GenerateGuestUsername(System.Action<string> callback)
    {
        while (true)
        {
            string candidate = "Guest" + Random.Range(1000, 9999);
            string url = $"{supabaseUrl}/rest/v1/{TABLE}?username=eq.{candidate}&select=player_id&limit=1";

            using (var req = UnityWebRequest.Get(url))
            {
                req.SetRequestHeader("apikey", supabasePublishableKey);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success && req.downloadHandler.text == "[]")
                {
                    callback(candidate);
                    yield break;
                }
            }
        }
    }

    IEnumerator FetchLeaderboard()
    {
        if (leaderboardText) leaderboardText.text = BuildLoadingTable();

        string url = $"{supabaseUrl}/rest/v1/{TABLE}?select=username,time_ms&order=time_ms.asc&limit=10";
        using (var req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("apikey", supabasePublishableKey);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (leaderboardText) leaderboardText.text = "Failed to load leaderboard";
                yield break;
            }

            var entries = ParseEntries(req.downloadHandler.text);
            if (leaderboardText)
            {
                leaderboardText.text = entries.Count == 0
                    ? "No times yet!"
                    : BuildTable(entries);
            }
        }
    }

    static string BuildLoadingTable()
    {
        const string Y  = "<color=#FFD700>";
        const string W  = "<color=#FFFFFF>";
        const string E  = "</color>";
        const int RankW = 5;
        const int NameW = 17;
        const int TimeW = 10;

        string Pad(string s, int w) { s = " " + s + " "; return s.Length > w ? s.Substring(0, w) : s.PadRight(w); }

        string sep     = $"{Y}+{new string('-', RankW)}+{new string('-', NameW)}+{new string('-', TimeW)}+{E}";
        string MakeRow(string rank, string name, string time) =>
            $"{Y}|{W}{Pad(rank, RankW)}{Y}|{W}{Pad(name, NameW)}{Y}|{W}{Pad(time, TimeW)}{Y}|{E}";

        var sb = new StringBuilder();
        sb.AppendLine(sep);
        sb.AppendLine(MakeRow("#", "PLAYER", "TIME"));
        sb.AppendLine(sep);
        for (int i = 0; i < 10; i++)
            sb.AppendLine(MakeRow($"#{i + 1}", "...", "..."));
        sb.AppendLine(sep);
        return sb.ToString().TrimEnd();
    }

    static string BuildTable(List<LeaderboardEntry> entries)
    {
        const string Y  = "<color=#FFD700>";
        const string W  = "<color=#FFFFFF>";
        const string E  = "</color>";

        // Column inner widths in characters (excluding pipes)
        const int RankW = 5;   //  " #10 "
        const int NameW = 17;  //  " username        "
        const int TimeW = 10;  //  " 0:47.101 "

        string Pad(string s, int w)
        {
            s = " " + s + " ";
            if (s.Length > w) s = s.Substring(0, w);
            return s.PadRight(w);
        }

        string TruncateName(string n)
        {
            if (n.Length > 15) n = n.Substring(0, 12) + "...";
            return n;
        }

        string sep     = $"{Y}+{new string('-', RankW)}+{new string('-', NameW)}+{new string('-', TimeW)}+{E}";
        string MakeRow(string rank, string name, string time) =>
            $"{Y}|{W}{Pad(rank, RankW)}{Y}|{W}{Pad(name, NameW)}{Y}|{W}{Pad(time, TimeW)}{Y}|{E}";

        var sb = new StringBuilder();
        sb.AppendLine(sep);
        sb.AppendLine(MakeRow("#", "PLAYER", "TIME"));
        sb.AppendLine(sep);

        for (int i = 0; i < entries.Count; i++)
        {
            string rank = $"#{i + 1}";
            string name = TruncateName(entries[i].username);
            float  secs = entries[i].time_ms / 1000f;
            string time = LapTimer.FormatTime(secs);
            sb.AppendLine(MakeRow(rank, name, time));
        }

        sb.AppendLine(sep);
        return sb.ToString().TrimEnd();
    }

    List<LeaderboardEntry> ParseEntries(string json)
    {
        var list = new List<LeaderboardEntry>();
        try
        {
            var wrapper = JsonUtility.FromJson<EntryList>("{\"items\":" + json + "}");
            if (wrapper?.items != null)
                list.AddRange(wrapper.items);
        }
        catch { }
        return list;
    }

    [System.Serializable] class LeaderboardEntry { public string username; public int time_ms; }
    [System.Serializable] class EntryList { public LeaderboardEntry[] items; }
}
