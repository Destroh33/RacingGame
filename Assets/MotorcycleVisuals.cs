using UnityEngine;

public class MotorcycleVisuals : MonoBehaviour
{
    [Header("References")]
    public MotorcyclePhysics physics;
    public MotorcycleInput   input;

    [Header("Wheel Meshes")]
    public Transform frontWheelMesh;
    public Transform rearWheelMesh;
    public float     wheelRadius = 0.3f;

    [Header("Tire Smoke")]
    public ParticleSystem frontTireSmoke;
    public ParticleSystem rearTireSmoke;
    public float          smokeSlideThreshold = 0.5f;

    [Header("Skid Marks")]
    public TrailRenderer frontSkidTrail;
    public TrailRenderer rearSkidTrail;

    [Header("Speed Lines (optional)")]
    public ParticleSystem speedLines;
    public float          speedLinesThreshold = 0.7f;  // fraction of maxSpeed

    float frontWheelRot;
    float rearWheelRot;

    void Update()
    {
        if (physics == null) return;

        SpinWheels();
        HandleSmoke();
        HandleSkidMarks();
        HandleSpeedLines();
    }

    void SpinWheels()
    {
        float speed = physics.CurrentSpeed;
        float degreesPerSecond = (speed / (2f * Mathf.PI * wheelRadius)) * 360f;

        frontWheelRot += degreesPerSecond * Time.deltaTime;
        rearWheelRot  += degreesPerSecond * Time.deltaTime;

        if (frontWheelMesh != null)
            frontWheelMesh.localRotation = Quaternion.Euler(0f, 0f, -frontWheelRot);

        if (rearWheelMesh != null)
            rearWheelMesh.localRotation = Quaternion.Euler(0f, 0f, -rearWheelRot);
    }

    void HandleSmoke()
    {
        bool showSmoke = physics.IsSliding || (input != null && input.RearBrake > smokeSlideThreshold);

        SetParticles(rearTireSmoke, showSmoke);

        // Front smoke only on heavy front braking
        bool frontSmoke = input != null && input.FrontBrake > 0.8f && physics.IsGrounded;
        SetParticles(frontTireSmoke, frontSmoke);
    }

    void SetParticles(ParticleSystem ps, bool active)
    {
        if (ps == null) return;
        if (active && !ps.isPlaying)  ps.Play();
        if (!active && ps.isPlaying)  ps.Stop();
    }

    void HandleSkidMarks()
    {
        SetTrail(rearSkidTrail,  physics.IsSliding);
        SetTrail(frontSkidTrail, input != null && input.FrontBrake > 0.8f);
    }

    void SetTrail(TrailRenderer trail, bool active)
    {
        if (trail == null) return;
        trail.emitting = active && physics.IsGrounded;
    }

    void HandleSpeedLines()
    {
        if (speedLines == null || physics == null) return;

        float speedRatio = Mathf.Abs(physics.CurrentSpeed) / physics.maxSpeed;
        bool  show       = speedRatio > speedLinesThreshold;

        SetParticles(speedLines, show);
    }
}
