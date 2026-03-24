using UnityEngine;

public class MotorcycleCamera : MonoBehaviour
{
    [Header("References")]
    public Transform target;
    public MotorcyclePhysics physics;

    [Header("Follow")]
    public float followDistance  = 6f;
    public float cameraHeight    = 2f;
    public float positionSmooth  = 8f;
    public float rotationSmooth  = 6f;

    [Header("Speed Pull-back")]
    public float maxSpeedPullback = 3f;
    public float maxSpeed         = 80f;

    [Header("Lean")]
    public float cameraLeanFactor = 0.3f;

    [Header("Shake")]
    public float shakeAmplitude    = 0.05f;
    public float shakeFrequency    = 14f;
    public float shakeMinSpeed     = 25f;
    public float shakeMaxSpeed     = 40f;

    [Header("Crash")]
    public float crashRiseHeight  = 6f;   // how high camera pulls up after crash
    public float crashRiseSpeed   = 2f;

    Vector3 currentVelocity;
    Vector3 smoothedTargetPos;
    Vector3 crashCameraTarget;
    float   shakeTimer;
    Vector3 shakeOffset;
    float   shakeOffsetTimer;

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

        bool crashed = physics != null && physics.IsCrashed;

        if (crashed)
        {
            UpdateCrashCamera();
            return;
        }

        float speed   = physics != null ? Mathf.Abs(physics.CurrentSpeed) : 0f;
        float lean    = physics != null ? physics.CurrentLean : 0f;
        bool  sliding = physics != null && physics.IsSliding;

        float speedRatio = Mathf.InverseLerp(0f, maxSpeed, speed);
        float distance   = followDistance + speedRatio * maxSpeedPullback;

        smoothedTargetPos = Vector3.Lerp(smoothedTargetPos, target.position, 25f * Time.deltaTime);

        Vector3 desiredPos = smoothedTargetPos
            - target.forward * distance
            + Vector3.up * cameraHeight;

        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPos, ref currentVelocity,
            1f / positionSmooth, Mathf.Infinity, Time.deltaTime);

        Quaternion lookRot  = Quaternion.LookRotation((target.position + Vector3.up * 0.5f) - transform.position);
        Quaternion leanRot  = Quaternion.Euler(0f, 0f, -lean * cameraLeanFactor);
        Quaternion desiredRot = lookRot * leanRot;

        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotationSmooth * Time.deltaTime);

        float shakeT = Mathf.InverseLerp(shakeMinSpeed, shakeMaxSpeed, speed);
        if (shakeT > 0f)
        {
            // Pick a new random target offset at shakeFrequency Hz
            shakeOffsetTimer += Time.deltaTime * shakeFrequency;
            if (shakeOffsetTimer >= 1f)
            {
                shakeOffsetTimer = 0f;
                shakeOffset = Random.insideUnitSphere * shakeAmplitude * shakeT;
                shakeOffset.z = 0f; // no forward/back — only up/sideways
            }
        }
        else
        {
            shakeOffset = Vector3.zero;
            shakeOffsetTimer = 0f;
        }

        // Always lerp back toward zero so it never feels stuck
        shakeOffset = Vector3.Lerp(shakeOffset, Vector3.zero, shakeFrequency * Time.deltaTime);
        transform.position += transform.TransformDirection(shakeOffset);
    }

    void UpdateCrashCamera()
    {
        // On first crash frame, lock the aerial target position
        if (crashCameraTarget == Vector3.zero)
            crashCameraTarget = transform.position + Vector3.up * crashRiseHeight;

        // Drift up to the crash target
        transform.position = Vector3.Lerp(transform.position, crashCameraTarget, crashRiseSpeed * Time.deltaTime);

        // Look at crash site, lerp lean back to level
        Quaternion lookRot   = Quaternion.LookRotation((target.position + Vector3.up * 0.5f) - transform.position);
        Quaternion levelRot  = Quaternion.Euler(lookRot.eulerAngles.x, lookRot.eulerAngles.y, 0f);
        transform.rotation   = Quaternion.Slerp(transform.rotation, levelRot, 3f * Time.deltaTime);
    }

    void OnEnable()
    {
        crashCameraTarget = Vector3.zero;
    }
}
