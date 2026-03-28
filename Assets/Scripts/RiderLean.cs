using UnityEngine;

public class RiderLean : MonoBehaviour
{
    [Header("References")]
    public MotorcyclePhysics physics;

    [Header("Lean")]
    [Range(0f, 1f)] public float leanFactor = 0.6f;
    public float leanSpeed = 4f;

    [Header("Slope Pitch")]
    [Range(0f, 1f)] public float pitchFactor = 0.5f;
    public float pitchSpeed = 4f;

    float currentLean;
    float currentPitch;
    public float CurrentLean => currentLean;

    void Update()
    {
        if (physics == null) return;

        float leanTarget = physics.CurrentLean * leanFactor;
        currentLean = Mathf.Lerp(currentLean, leanTarget, leanSpeed * Time.deltaTime);

        float pitchTarget = physics.CurrentPitch * pitchFactor;
        currentPitch = Mathf.Lerp(currentPitch, pitchTarget, pitchSpeed * Time.deltaTime);

        transform.localRotation = Quaternion.Euler(currentPitch, 0f, -currentLean);
    }
}
