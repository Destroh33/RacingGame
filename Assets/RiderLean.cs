using UnityEngine;

public class RiderLean : MonoBehaviour
{
    [Header("References")]
    public MotorcyclePhysics physics;

    [Header("Lean")]
    [Range(0f, 1f)] public float leanFactor = 0.6f;
    public float leanSpeed = 4f;

    float currentLean;

    void Update()
    {
        if (physics == null) return;

        float target = physics.CurrentLean * leanFactor;
        currentLean = Mathf.Lerp(currentLean, target, leanSpeed * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(0f, 0f, -currentLean);
    }
}
