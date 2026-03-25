using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class BikeCollisionDetector : MonoBehaviour
{
    [Header("References")]
    public MotorcyclePhysics physics;
    public MotorcycleInput   input;

    [Header("Crash Trigger")]
    public float crashSpeedThreshold = 5f;
    public BoxCollider box;

    [Header("Crash Audio")]
    public AudioClip crashClip;
    public float     minImpulse     = 5f;
    public float     maxImpulse     = 80f;
    public float     minCrashVolume = 0.15f;
    public float     maxCrashVolume = 1.0f;
    public float     crashCooldown  = 0.12f;

    // Tracks cooldown per-contact point so large hitboxes don't suppress
    // sounds from genuinely separate collision events happening at the same time
    System.Collections.Generic.Dictionary<int, float> contactCooldowns
        = new System.Collections.Generic.Dictionary<int, float>();

    AudioSource audioSource;

    void Awake()
    {
        audioSource              = GetComponent<AudioSource>();
        audioSource.loop         = false;
        audioSource.playOnAwake  = false;
        audioSource.spatialBlend = 0f;
        audioSource.priority     = 0;
    }

    void OnCollisionEnter(Collision col)
    {
        if (physics == null) return;

        if (!physics.IsCrashed && Mathf.Abs(physics.CurrentSpeed) > crashSpeedThreshold)
        {
            if (box != null)
            {
                box.center = new Vector3(box.center.x, 0.61f, box.center.z);
                box.size   = new Vector3(box.size.x,   1.22f, box.size.z);
            }
            physics.TriggerCrash(physics.GetComponent<Rigidbody>().linearVelocity);
        }

        HandleCollisionContacts(col);
    }

    void OnCollisionStay(Collision col)
    {
        if (physics == null || !physics.IsCrashed) return;
        HandleCollisionContacts(col);
    }

    // Each contact point gets its own cooldown so a large hitbox touching
    // multiple surfaces at once doesn't suppress any of them
    void HandleCollisionContacts(Collision col)
    {
        if (crashClip == null) return;

        for (int i = 0; i < col.contactCount; i++)
        {
            ContactPoint contact = col.GetContact(i);

            // Use a hash of position snapped to a grid as the contact ID
            // so nearby contacts on the same surface share a cooldown bucket
            // but contacts on clearly separate surfaces don't
            int contactID = GetContactID(contact.point);

            float lastTime;
            if (contactCooldowns.TryGetValue(contactID, out lastTime))
            {
                if (Time.time - lastTime < crashCooldown)
                    continue;
            }

            // Approximate per-contact impulse from the full collision impulse
            // weighted by how many contacts there are
            float impulse = col.impulse.magnitude / Mathf.Max(1, col.contactCount);

            if (impulse < minImpulse) continue;

            contactCooldowns[contactID] = Time.time;
            PlayCrashSound(impulse);
        }
    }

    // Snaps world position to a 0.5m grid and hashes it so nearby contact
    // points on the same surface share a cooldown, not a global one
    int GetContactID(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x * 2);
        int y = Mathf.RoundToInt(worldPos.y * 2);
        int z = Mathf.RoundToInt(worldPos.z * 2);
        return x * 73856093 ^ y * 19349663 ^ z * 83492791;
    }

    void PlayCrashSound(float impulse)
    {
        float volume = Mathf.Lerp(minCrashVolume, maxCrashVolume,
                                  Mathf.InverseLerp(minImpulse, maxImpulse, impulse));
        float pitch  = Random.Range(0.9f, 1.1f);

        // Spawn isolated AudioSource so engine fade Stop() calls can never cut it off
        var go = new GameObject("CrashSFX");
        go.transform.position = transform.position;
        var src          = go.AddComponent<AudioSource>();
        src.clip         = crashClip;
        src.volume       = volume;
        src.pitch        = pitch;
        src.spatialBlend = 0f;
        src.priority     = 0;
        src.Play();
        Destroy(go, crashClip.length + 0.1f);
    }

    // Clean up stale cooldown entries so the dictionary doesn't grow forever
    float lastCleanup;
    void Update()
    {
        // Purge cooldown entries older than 2 seconds every 5 seconds
        if (Time.time - lastCleanup > 5f)
        {
            lastCleanup = Time.time;
            var toRemove = new System.Collections.Generic.List<int>();
            foreach (var kvp in contactCooldowns)
                if (Time.time - kvp.Value > 2f)
                    toRemove.Add(kvp.Key);
            foreach (var key in toRemove)
                contactCooldowns.Remove(key);
        }

        if (!physics.IsCrashed) return;
        if (input == null || !input.ResetBike) return;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}