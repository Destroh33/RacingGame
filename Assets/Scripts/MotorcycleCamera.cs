using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

    [Header("Speed Effects")]
    public Volume postProcessVolume;
    public float  fovBase        = 60f;
    public float  fovIncrease    = 8f;
    public float  fovSmooth      = 4f;
    public float  maxMotionBlur  = 0.25f;
    public float  vignetteBase   = 0.25f;
    public float  vignetteMax    = 0.45f;
    public float  speedFxMin     = 30f;
    public float  speedFxMax     = 40f;

    [Header("Crash")]
    public float crashRiseHeight  = 6f;
    public float crashRiseSpeed   = 2f;

    Camera     cam;
    MotionBlur motionBlur;
    Vignette   vignette;

    Vector3 currentVelocity;
    Vector3 smoothedTargetPos;
    Vector3 crashCameraTarget;
    Vector3 shakeOffset;
    float   shakeOffsetTimer;
    float   speedFxT;

    // ─── Lifecycle ────────────────────────────────────────────────────────────

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null) cam.fieldOfView = fovBase;

        InitVolume();

        if (target == null) return;
        smoothedTargetPos  = target.position;
        transform.position = target.position - target.forward * followDistance + Vector3.up * cameraHeight;
        transform.LookAt(target.position + Vector3.up * 0.5f);
    }

    void OnEnable()
    {
        crashCameraTarget = Vector3.zero;
        // Re-init in case the GameObject was disabled and re-enabled
        // (Start won't re-run but OnEnable will)
        if (cam != null) InitVolume();
    }

    void OnDestroy()
    {
        // Clean up the runtime profile instance so it doesn't leak as a
        // floating ScriptableObject in the editor
        if (postProcessVolume != null && postProcessVolume.profile != null
            && postProcessVolume.profile != postProcessVolume.sharedProfile)
        {
            Destroy(postProcessVolume.profile);
        }
    }

    // ─── Volume Setup ─────────────────────────────────────────────────────────

    void InitVolume()
    {
        motionBlur = null;
        vignette   = null;

        if (postProcessVolume == null) return;
        if (postProcessVolume.sharedProfile == null) return;

        // Instantiate a runtime copy so we never modify the asset on disk
        var runtimeProfile = Instantiate(postProcessVolume.sharedProfile);
        postProcessVolume.profile = runtimeProfile;

        runtimeProfile.TryGet(out motionBlur);
        runtimeProfile.TryGet(out vignette);

        // Must be true or the volume blending system ignores our values
        if (motionBlur != null)
        {
            motionBlur.intensity.overrideState = true;
            motionBlur.intensity.value         = 0f;
        }
        if (vignette != null)
        {
            vignette.intensity.overrideState = true;
            vignette.intensity.value         = vignetteBase;
        }
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (target == null) return;

        bool crashed = physics != null && physics.IsCrashed;

        if (crashed)
        {
            ApplySpeedFx(0f);
            UpdateCrashCamera();
            return;
        }

        float speed = physics != null ? Mathf.Abs(physics.CurrentSpeed) : 0f;
        float lean  = physics != null ? physics.CurrentLean : 0f;

        float speedRatio = Mathf.InverseLerp(0f, maxSpeed, speed);
        float distance   = followDistance + speedRatio * maxSpeedPullback;

        smoothedTargetPos = Vector3.Lerp(smoothedTargetPos, target.position, 25f * Time.deltaTime);

        Vector3 desiredPos = smoothedTargetPos
            - target.forward * distance
            + Vector3.up * cameraHeight;

        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPos, ref currentVelocity,
            1f / positionSmooth, Mathf.Infinity, Time.deltaTime);

        Quaternion lookRot = Quaternion.LookRotation(
            (target.position + Vector3.up * 0.5f) - transform.position);
        Quaternion leanRot = Quaternion.Euler(0f, 0f, -lean * cameraLeanFactor);
        transform.rotation = Quaternion.Slerp(
            transform.rotation, lookRot * leanRot, rotationSmooth * Time.deltaTime);

        // Shake
        float shakeT = Mathf.InverseLerp(shakeMinSpeed, shakeMaxSpeed, speed);
        if (shakeT > 0f)
        {
            shakeOffsetTimer += Time.deltaTime * shakeFrequency;
            if (shakeOffsetTimer >= 1f)
            {
                shakeOffsetTimer = 0f;
                shakeOffset      = Random.insideUnitSphere * shakeAmplitude * shakeT;
                shakeOffset.z    = 0f;
            }
        }
        else
        {
            shakeOffset      = Vector3.zero;
            shakeOffsetTimer = 0f;
        }
        shakeOffset        = Vector3.Lerp(shakeOffset, Vector3.zero, shakeFrequency * Time.deltaTime);
        transform.position += transform.TransformDirection(shakeOffset);

        // FOV / post fx
        float targetT = Mathf.InverseLerp(speedFxMin, speedFxMax, speed);
        speedFxT      = Mathf.Lerp(speedFxT, targetT, fovSmooth * Time.deltaTime);
        ApplySpeedFx(speedFxT);
    }

    // ─── Speed FX ─────────────────────────────────────────────────────────────

    void ApplySpeedFx(float t)
    {
        if (cam != null)
            cam.fieldOfView = fovBase + fovIncrease * t;

        if (postProcessVolume == null || !postProcessVolume.isActiveAndEnabled) return;

        if (motionBlur != null)
            motionBlur.intensity.value = maxMotionBlur * t;

        if (vignette != null)
            vignette.intensity.value = Mathf.Lerp(vignetteBase, vignetteMax, t);
    }

    // ─── Crash Camera ─────────────────────────────────────────────────────────

    void UpdateCrashCamera()
    {
        if (crashCameraTarget == Vector3.zero)
            crashCameraTarget = transform.position + Vector3.up * crashRiseHeight;

        transform.position = Vector3.Lerp(
            transform.position, crashCameraTarget, crashRiseSpeed * Time.deltaTime);

        Quaternion lookRot  = Quaternion.LookRotation(
            (target.position + Vector3.up * 0.5f) - transform.position);
        Quaternion levelRot = Quaternion.Euler(
            lookRot.eulerAngles.x, lookRot.eulerAngles.y, 0f);
        transform.rotation  = Quaternion.Slerp(
            transform.rotation, levelRot, 3f * Time.deltaTime);
    }
}