using UnityEngine;

public class MotorcycleCamera : MonoBehaviour
{
    [Header("References")]
    public Transform target;              // The bike transform
    public MotorcyclePhysics physics;

    [Header("Follow")]
    public float followDistance  = 6f;
    public float cameraHeight    = 2f;
    public float positionSmooth  = 8f;
    public float rotationSmooth  = 6f;

    [Header("Speed Pull-back")]
    public float maxSpeedPullback = 3f;   // Extra distance added at max speed
    public float maxSpeed         = 80f;

    [Header("Lean")]
    public float cameraLeanFactor = 0.3f; // How much camera rolls with the bike (0–1)

    [Header("Shake")]
    public float shakeAmplitude  = 0.08f;
    public float shakeFrequency  = 12f;

    Vector3 currentVelocity;
    Vector3 smoothedTargetPos;
    float shakeTimer;

    void Start()
    {
        if (target == null) return;
        smoothedTargetPos = target.position;
        transform.position = target.position - target.forward * followDistance + Vector3.up * cameraHeight;
        transform.LookAt(target.position + Vector3.up * 0.5f);
    }

    void LateUpdate()
    {
        if (target == null) return;

        float speed      = physics != null ? Mathf.Abs(physics.CurrentSpeed) : 0f;
        float lean       = physics != null ? physics.CurrentLean : 0f;
        bool  sliding    = physics != null && physics.IsSliding;

        // Distance scales with speed
        float speedRatio  = Mathf.InverseLerp(0f, maxSpeed, speed);
        float distance    = followDistance + speedRatio * maxSpeedPullback;

        // Smooth the target position to absorb physics timestep jitter
        smoothedTargetPos = Vector3.Lerp(smoothedTargetPos, target.position, 25f * Time.deltaTime);

        // Desired position: behind and above target
        Vector3 desiredPos = smoothedTargetPos
            - target.forward * distance
            + Vector3.up * cameraHeight;

        // Smooth position
        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPos, ref currentVelocity,
            1f / positionSmooth, Mathf.Infinity, Time.deltaTime);

        // Look at target
        Quaternion lookRot = Quaternion.LookRotation(
            (target.position + Vector3.up * 0.5f) - transform.position);

        // Lean roll
        Quaternion leanRot = Quaternion.Euler(0f, 0f, -lean * cameraLeanFactor);
        Quaternion desiredRot = lookRot * leanRot;

        transform.rotation = Quaternion.Slerp(
            transform.rotation, desiredRot,
            rotationSmooth * Time.deltaTime);

        // Shake on slide or high speed over bumps
        if (sliding || (speedRatio > 0.85f && physics != null && !physics.IsTucked))
        {
            shakeTimer += Time.deltaTime * shakeFrequency;
            float shake = Mathf.Sin(shakeTimer) * shakeAmplitude * (sliding ? 0.6f : 0.3f);
            transform.position += transform.up * shake;
        }
        else
        {
            shakeTimer = 0f;
        }
    }
}
