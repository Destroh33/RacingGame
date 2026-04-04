using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [SerializeField] AudioSource audioSource;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Play(AudioClip clip, float volume = 1f)
    {
        if (audioSource.clip == clip && audioSource.isPlaying) return;
        audioSource.clip   = clip;
        audioSource.volume = volume;
        audioSource.loop   = true;
        audioSource.Play();
    }

    public void Stop() => audioSource.Stop();
}
