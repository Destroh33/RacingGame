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

    bool cachedCrash = false;
    public GameObject crashParticles;
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
        if(physics.IsCrashed && !cachedCrash)
            Instantiate(crashParticles, this.gameObject.transform.position, this.gameObject.transform.rotation);
        cachedCrash = physics.IsCrashed;
        bool skidding = !physics.IsCrashed && physics.IsSliding;
        SetParticles(rearTireSmoke,  skidding);
        SetParticles(frontTireSmoke, skidding);
    }

    void SetParticles(ParticleSystem ps, bool active)
    {
        if (ps == null) return;
        if (active && !ps.isPlaying)  ps.Play();
        if (!active && ps.isPlaying)  ps.Stop();
    }

    void HandleSkidMarks()
    {
        bool skidding = !physics.IsCrashed && physics.IsSliding;
        SetTrail(rearSkidTrail,  skidding);
        SetTrail(frontSkidTrail, skidding);
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
        bool  show       = !physics.IsCrashed && speedRatio > speedLinesThreshold;

        SetParticles(speedLines, show);
    }
}
