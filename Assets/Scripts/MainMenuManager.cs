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

        string input = usernameInput.text.Trim();
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
        if (leaderboardText) leaderboardText.text = "Loading...";

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
                if (entries.Count == 0)
                {
                    leaderboardText.text = "No times yet!";
                }
                else
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < entries.Count; i++)
                    {
                        string rank     = $"#{i + 1}".PadRight(4);
                        string name     = entries[i].username.PadRight(16);
                        float  secs     = entries[i].time_ms / 1000f;
                        string time     = LapTimer.FormatTime(secs);
                        sb.AppendLine($"{rank}{name}{time}");
                    }
                    leaderboardText.text = sb.ToString().TrimEnd();
                }
            }
        }
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
