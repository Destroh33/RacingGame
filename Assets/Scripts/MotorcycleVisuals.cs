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

    [Header("Speed Lines")]
    public ParticleSystem speedTrail;
    public float          speedTrailMinSpeed = 25f;
    public float          speedTrailMaxSpeed = 40f;

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
        bool skidding = !physics.IsCrashed && physics.IsSliding && physics.IsGrounded;
        SetParticles(rearTireSmoke,  skidding);
        SetParticles(frontTireSmoke, skidding);
    }

    void SetParticles(ParticleSystem ps, bool active)
    {
        if (ps == null) return;
        if (active)
        {
           ps.Stop();
           ps.Play();   
        }
        if (!active)  ps.Stop();
    }

    void HandleSkidMarks()
    {
        bool skidding = !physics.IsCrashed && physics.IsSliding && physics.IsGrounded;
        SetTrail(rearSkidTrail,  skidding);
        SetTrail(frontSkidTrail, skidding);
    }

    void SetTrail(TrailRenderer trail, bool active)
    {
        if (trail == null) return;
        trail.emitting = active;
    }

    void HandleSpeedLines()
    {
        if (speedTrail == null) return;

        float speed = Mathf.Abs(physics.CurrentSpeed);
        float t     = Mathf.InverseLerp(speedTrailMinSpeed, speedTrailMaxSpeed, speed);

        if (physics.IsCrashed || t <= 0f)
        {
            if (speedTrail != null && speedTrail.gameObject.activeSelf)
                speedTrail.gameObject.SetActive(false);
            return;
        }

        if (!speedTrail.gameObject.activeSelf)
            speedTrail.gameObject.SetActive(true);

        var main            = speedTrail.main;
        Color c             = main.startColor.color;
        c.a                 = Mathf.Lerp(0.5f, 1f, t);
        main.startColor     = c;

        var emission            = speedTrail.emission;
        emission.rateOverTime   = Mathf.Lerp(10f, 25f, t);
    }
}
