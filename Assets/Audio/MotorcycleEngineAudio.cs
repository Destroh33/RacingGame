using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MotorcycleEngineAudio : MonoBehaviour
{
    [Header("References")]
    public MotorcyclePhysics physics;
    public MotorcycleInput   input;

    [Header("Engine Clip")]
    public AudioClip engineLoop;

    [Header("Tire Screech")]
    public AudioClip screechwLoop;
    public float     screechFadeSpeed = 8f;
    public float     screechMaxVolume = 0.6f;

    [Header("Wind")]
    public AudioClip windLoop;
    public float     windMinSpeed   = 30f;
    public float     windFadeSpeed  = 3f;
    public float     windMaxVolume  = 0.5f;

    [Header("Speed Range")]
    public float maxSpeed    = 80f;

    [Header("Pitch")]
    public float minPitch    = 0.6f;
    public float maxPitch    = 1.8f;
    public float pitchSmooth = 6f;

    [Header("Volume")]
    public float minVolume    = 0.7f;
    public float maxVolume    = 1.0f;
    public float volumeSmooth = 6f;

    [Header("Crash Fade")]
    public float engineFadeDuration = 0.8f;

    AudioSource engineSource;
    AudioSource screechSource;
    AudioSource windSource;
    bool        cachedCrash;

    void Awake()
    {
        engineSource        = GetComponent<AudioSource>();
        engineSource.clip   = engineLoop;
        engineSource.loop   = true;
        engineSource.volume = minVolume;
        engineSource.pitch  = minPitch;
        if (engineLoop != null) engineSource.Play();

        screechSource = CreateLoopSource(screechwLoop);
        windSource    = CreateLoopSource(windLoop);
    }

    AudioSource CreateLoopSource(AudioClip clip)
    {
        var src          = gameObject.AddComponent<AudioSource>();
        src.clip         = clip;
        src.loop         = true;
        src.playOnAwake  = false;
        src.spatialBlend = 0f;
        src.priority     = 128;
        src.volume       = 0f;
        if (clip != null) src.Play();
        return src;
    }

    void Update()
    {
        if (physics == null) return;

        bool isCrashed = physics.IsCrashed;

        if (isCrashed && !cachedCrash) StartCoroutine(FadeEngineOut());
        if (!isCrashed && cachedCrash) StartCoroutine(FadeEngineIn());

        cachedCrash = isCrashed;

        if (isCrashed)
        {
            // Silence loops immediately on crash
            screechSource.volume = Mathf.Lerp(screechSource.volume, 0f, screechFadeSpeed * Time.deltaTime);
            windSource.volume    = Mathf.Lerp(windSource.volume,    0f, windFadeSpeed    * Time.deltaTime);
            return;
        }

        if (!engineSource.isPlaying)
            engineSource.Play();

        float speed    = Mathf.Abs(physics.CurrentSpeed);
        float speedT   = Mathf.InverseLerp(0f, maxSpeed, speed);
        float throttle = input != null ? input.Throttle : 0f;

        // Engine pitch & volume
        float targetPitch = Mathf.Lerp(minPitch, maxPitch, speedT);
        if (throttle < 0.1f && speed > 10f)
            targetPitch *= 0.92f;

        engineSource.pitch  = Mathf.Lerp(engineSource.pitch,  targetPitch,                        pitchSmooth  * Time.deltaTime);
        engineSource.volume = Mathf.Lerp(engineSource.volume, Mathf.Lerp(minVolume, maxVolume, speedT), volumeSmooth * Time.deltaTime);

        // Screech — fades in while skidding
        float screechTarget  = physics.IsSliding && physics.IsGrounded ? screechMaxVolume : 0f;
        screechSource.volume = Mathf.Lerp(screechSource.volume, screechTarget, screechFadeSpeed * Time.deltaTime);

        // Wind — fades in above windMinSpeed
        float windT          = Mathf.InverseLerp(windMinSpeed, maxSpeed, speed);
        windSource.volume    = Mathf.Lerp(windSource.volume, windMaxVolume * windT, windFadeSpeed * Time.deltaTime);
    }

    IEnumerator FadeEngineOut()
    {
        float startVolume = engineSource.volume;
        float elapsed     = 0f;
        while (elapsed < engineFadeDuration)
        {
            elapsed += Time.deltaTime;
            engineSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / engineFadeDuration);
            yield return null;
        }
        engineSource.volume = 0f;
        engineSource.Stop();
    }

    IEnumerator FadeEngineIn()
    {
        if (!engineSource.isPlaying)
        {
            engineSource.volume = 0f;
            engineSource.Play();
        }
        float elapsed = 0f;
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime;
            engineSource.volume = Mathf.Lerp(0f, minVolume, elapsed);
            yield return null;
        }
        engineSource.volume = minVolume;
    }
}
