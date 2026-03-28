using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MotorcyclePhysics : MonoBehaviour
{
    [Header("References")]
    public MotorcycleInput input;
    public Transform bikeModel;          // Visual model — rotated for lean
    public Transform frontWheelPos;      // Suspension raycast origin
    public Transform rearWheelPos;
    public Collider bikeCollider;

    [Header("Speed")]
    public float maxSpeed            = 80f;
    public float accelerationForce   = 4000f;

    [Header("Braking & Reverse")]
    public float brakeForce          = 5700f;
    public float reverseForce        = 1500f;
    public float maxReverseSpeed     = 8f;

    [Header("Lean & Steering")]
    public float maxLeanAngle        = 55f;
    public float leanSpeed           = 3.5f;
    public float leanReturnSpeed     = 2.5f;
    public float lowSpeedLeanRate    = 4.0f;
    public float highSpeedLeanRate   = 1.5f;
    public float lowSpeedMaxLean     = 35f;
    public float highSpeedMaxLean    = 55f;
    public float maxTurnTorque       = 6f;
    public float steeringDamping     = 2.5f;
    public float yawCounterStrength  = 5f;

    [Header("Suspension")]
    public float suspensionRestLength = 0.4f;
    public float suspensionTravel     = 0.2f;
    public float springStrength       = 18000f;
    public float dampStrength         = 1200f;
    public float wheelRadius          = 0.3f;
    public LayerMask groundMask       = ~0;   // default: everything — set to your Ground layer in Inspector

    [Header("Grip")]
    public float maxGrip              = 6000f;
    public float gripFalloffWithLean  = 0.6f;   // grip = maxGrip * lerp(1, cos, this)
    public float slideThreshold       = 0.65f;

    [Header("Drag")]
    public float dragNormal           = 0.8f;

    [Header("Air & Landing")]
    public float airSelfRightTorque  = 5f;   // pulls bike level in air (unused, reserved)
    public float airControlTorque    = 2f;   // A/D steers in air for landing orientation

    [Header("Reset")]
    public Vector3 resetOffset        = new Vector3(0f, 1f, 0f);

    // --- Public state read by camera / visuals / IK ---
    public float CurrentLean    { get; private set; }
    public float CurrentPitch   { get; private set; }
    public float CurrentSpeed   { get; private set; }
    public bool  IsGrounded     { get; private set; }
    public bool  IsSliding      { get; private set; }
    public bool  IsCrashed      { get; private set; }
    public bool  IsFinished     { get; private set; }

    public event System.Action<Vector3> OnCrash;

    Rigidbody rb;
    float prevFrontCompression;
    float prevRearCompression;
    Vector3 resetPosition;
    Quaternion resetRotation;
    Vector3 frontGroundNormal = Vector3.up;
    Vector3 rearGroundNormal  = Vector3.up;
    bool frontIsGrounded;
    bool rearIsGrounded;
    Vector3    savedCoM;
    Vector3    savedInertia;
    Quaternion savedInertiaRot;
    float currentVisualPitch;

    // Debug readout — remove once suspension is tuned
    float debugFrontCompression, debugFrontForce;
    float debugRearCompression,  debugRearForce;

    [SerializeField] GameObject minimapCam;
    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Capture auto CoM and inertia tensor before collider is active, then lock both
        rb.automaticCenterOfMass  = true;
        rb.automaticInertiaTensor = true;
        Vector3 naturalCoM       = rb.centerOfMass;
        Vector3 naturalInertia   = rb.inertiaTensor;
        Quaternion naturalRot    = rb.inertiaTensorRotation;
        savedCoM         = naturalCoM;
        savedInertia     = naturalInertia;
        savedInertiaRot  = naturalRot;
        rb.automaticCenterOfMass  = false;
        rb.automaticInertiaTensor = false;
        rb.centerOfMass           = savedCoM;
        rb.inertiaTensor          = savedInertia;
        rb.inertiaTensorRotation  = savedInertiaRot;
        if (bikeCollider != null) bikeCollider.enabled = true;

        // Keep rigidbody fully upright — pitch and lean are visual only
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.angularDamping = steeringDamping;
        resetPosition = transform.position;
        resetRotation = transform.rotation;
    }

    void FixedUpdate()
    {
        if (input == null) return;

        if (IsCrashed)
        {
            CurrentLean = Mathf.Lerp(CurrentLean, 0f, 5f * Time.fixedDeltaTime);
            UpdateVisualLean();
            return;
        }

        CurrentSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        SuspensionAndGround();
        ApplyDrag();

        if (IsFinished)
        {
            if (IsGrounded)
            {
                ApplyFinishControl();
                ApplyGrip();
            }
            UpdateVisualLean();
            return;
        }

        if (IsGrounded)
        {
            ApplyThrottle();
            ApplyBraking();
            ApplyLeanSteering();
            ApplyGrip();
        }
        else
        {
            ApplyAirPhysics();
        }

        UpdateVisualLean();
    }

    void SuspensionAndGround()
    {
        frontIsGrounded = SuspensionRay(frontWheelPos, ref prevFrontCompression, out debugFrontCompression, out debugFrontForce, out frontGroundNormal);
        rearIsGrounded  = SuspensionRay(rearWheelPos,  ref prevRearCompression,  out debugRearCompression,  out debugRearForce,  out rearGroundNormal);
        IsGrounded = frontIsGrounded || rearIsGrounded;
    }

    bool SuspensionRay(Transform origin, ref float prevCompression, out float outCompression, out float outForce, out Vector3 outNormal)
    {
        outCompression = 0f;
        outForce       = 0f;
        outNormal      = Vector3.up;
        if (origin == null) return false;

        float rayLength = suspensionRestLength + suspensionTravel + wheelRadius;
        bool didHit = Physics.Raycast(origin.position, -transform.up, out RaycastHit hit, rayLength, groundMask, QueryTriggerInteraction.Ignore);

        if (!didHit) return false;
        outNormal = hit.normal;

        float compression = 1f - ((hit.distance - wheelRadius) / suspensionRestLength);
        compression = Mathf.Clamp01(compression);

        float compressionVelocity = (compression - prevCompression) / Time.fixedDeltaTime;
        prevCompression = compression;

        float spring = compression * springStrength;
        float damp   = compressionVelocity * dampStrength;
        float force  = Mathf.Max(0f, spring + damp);

        rb.AddForceAtPosition(transform.up * force, origin.position, ForceMode.Force);

        outCompression = compression;
        outForce       = force;
        return true;
    }

    void ApplyAirPhysics()
    {
        // A/D steers yaw in air so you can orient for landing
        rb.AddRelativeTorque(Vector3.up * (input.Lean * airControlTorque), ForceMode.Acceleration);
    }

    void ApplyDrag()
    {
        rb.linearDamping = IsGrounded ? dragNormal : 0.05f;
    }

    void ApplyThrottle()
    {
        if (!IsGrounded || input.Throttle <= 0f) return;

        float speedRatio = Mathf.Clamp01(CurrentSpeed / maxSpeed);
        // Power curve: full force at low speed, tapers off near max
        float powerCurve = 1f - speedRatio * speedRatio;
        float force = input.Throttle * accelerationForce * powerCurve;
        rb.AddForce(transform.forward * force, ForceMode.Force);
    }

    void ApplyBraking()
    {
        if (!IsGrounded) return;

        // Space (FrontBrake) = brake, decelerates regardless of direction
        if (input.FrontBrake > 0f && Mathf.Abs(CurrentSpeed) > 0.1f)
            rb.AddForce(-transform.forward * Mathf.Sign(CurrentSpeed) * input.FrontBrake * brakeForce, ForceMode.Force);

        // S (RearBrake) = reverse, capped at maxReverseSpeed
        if (input.RearBrake > 0f && CurrentSpeed > -maxReverseSpeed)
            rb.AddForce(-transform.forward * input.RearBrake * reverseForce, ForceMode.Force);
    }

    void ApplyLeanSteering()
    {
        if (!IsGrounded) return;

        float speedFactor        = Mathf.InverseLerp(0f, maxSpeed, Mathf.Abs(CurrentSpeed));
        float effectiveLeanRate  = Mathf.Lerp(lowSpeedLeanRate, highSpeedLeanRate, speedFactor);
        float effectiveMaxLean   = Mathf.Lerp(lowSpeedMaxLean,  highSpeedMaxLean,  speedFactor);

        float targetLean = input.Lean * effectiveMaxLean;
        float lowSpeedBoost = Mathf.InverseLerp(5f, 0f, Mathf.Abs(CurrentSpeed));
        float effectiveLeanReturn = Mathf.Lerp(leanReturnSpeed, leanReturnSpeed * 5f, lowSpeedBoost);
        float rate = Mathf.Abs(targetLean) > Mathf.Abs(CurrentLean) ? effectiveLeanRate : effectiveLeanReturn;
        CurrentLean = Mathf.Lerp(CurrentLean, targetLean, rate * Time.fixedDeltaTime);

        // Yaw torque from lean — actually rotates the bike to face a new direction
        // Floor fades in from 0→1 over the first 5 m/s so you can't turn at standstill
        float floorFade = Mathf.InverseLerp(3f, 8f, Mathf.Abs(CurrentSpeed));
        float effectiveSpeedFactor = (1f - speedFactor * 0.5f) * floorFade;
        float turnTorque = (CurrentLean / effectiveMaxLean) * maxTurnTorque * effectiveSpeedFactor * Mathf.Sign(CurrentSpeed);
        rb.AddRelativeTorque(Vector3.up * turnTorque, ForceMode.Acceleration);

        // When lean input is released, actively counter the yaw so the bike stops turning quickly
        if (Mathf.Approximately(input.Lean, 0f))
        {
            float yawRate = Vector3.Dot(rb.angularVelocity, transform.up);
            rb.AddRelativeTorque(Vector3.up * -yawRate * yawCounterStrength, ForceMode.Acceleration);
        }
    }

    void ApplyGrip()
    {
        if (!IsGrounded) return;

        float lateralVelocity = Vector3.Dot(rb.linearVelocity, transform.right);
        float cosLean = Mathf.Cos(CurrentLean * Mathf.Deg2Rad);
        float totalGrip = maxGrip * Mathf.Lerp(1f, cosLean, gripFalloffWithLean);

        // Traction circle: braking consumes grip budget, leaving less for lateral correction
        float brakeGripUsed = Mathf.Clamp01(input.FrontBrake * brakeForce / totalGrip);
        float lateralGripAvailable = totalGrip * (1f - brakeGripUsed);

        float desiredCorrectionForce = -lateralVelocity * rb.mass / Time.fixedDeltaTime;
        float lateralForce = Mathf.Clamp(desiredCorrectionForce, -lateralGripAvailable, lateralGripAvailable);

        // Sliding when lateral grip can't fully counter lateral velocity
        IsSliding = Mathf.Abs(lateralVelocity) > slideThreshold &&
                    Mathf.Abs(desiredCorrectionForce) > lateralGripAvailable;

        rb.AddForce(transform.right * lateralForce, ForceMode.Force);
    }

    void UpdateVisualLean()
    {
        if (bikeModel == null) return;

        float targetPitch = 0f;
        if (IsGrounded)
        {
            // Only average normals from wheels that are actually on the ground
            // Avoids half-angle on ramps when one wheel is still on flat terrain
            Vector3 avgNormal;
            if (frontIsGrounded && rearIsGrounded)
                avgNormal = (frontGroundNormal + rearGroundNormal).normalized;
            else if (frontIsGrounded)
                avgNormal = frontGroundNormal;
            else
                avgNormal = rearGroundNormal;

            targetPitch = Vector3.SignedAngle(Vector3.up, avgNormal, transform.right);
        }

        // In air lerp slowly back to level; on ground snap to slope
        float pitchLerpSpeed = IsGrounded ? 25f : 3f;
        currentVisualPitch = Mathf.Lerp(currentVisualPitch, targetPitch, pitchLerpSpeed * Time.fixedDeltaTime);
        CurrentPitch = currentVisualPitch;

        bikeModel.localRotation = Quaternion.Euler(currentVisualPitch, 0f, -CurrentLean);
    }

    public void Reset()
    {
        IsCrashed = false;
        rb.constraints            = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.linearDamping          = dragNormal;
        rb.angularDamping         = steeringDamping;
        rb.automaticCenterOfMass  = false;
        rb.automaticInertiaTensor = false;
        rb.centerOfMass           = savedCoM;
        rb.inertiaTensor          = savedInertia;
        rb.inertiaTensorRotation  = savedInertiaRot;
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(resetPosition + resetOffset, resetRotation);
        CurrentLean = 0f;
        if (bikeModel != null) bikeModel.localRotation = Quaternion.identity;
    }


    void OnDrawGizmos()
    {
        if (rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(rb.centerOfMass), 0.5f);
        }
        DrawSuspensionGizmo(frontWheelPos);
        DrawSuspensionGizmo(rearWheelPos);
    }

    void DrawSuspensionGizmo(Transform origin)
    {
        if (origin == null) return;

        float rayLength = suspensionRestLength + suspensionTravel + wheelRadius;
        Vector3 down    = -transform.up;

        if (Physics.Raycast(origin.position, down, out RaycastHit hit, rayLength, groundMask, QueryTriggerInteraction.Ignore))
        {
            // Green to hit point, then dim to end of ray
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin.position, hit.point);
            Gizmos.color = new Color(0f, 0.4f, 0f);
            Gizmos.DrawLine(hit.point, origin.position + down * rayLength);
            // Yellow normal at contact
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(hit.point, hit.normal * 0.3f);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(origin.position, origin.position + down * rayLength);
        }

        // Small sphere at ray origin
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(origin.position, 0.05f);
    }

    // Called by MotorcycleVisuals or external code to update reset point
    public void TriggerCrash(Vector3 impactVelocity)
    {
        IsCrashed = true;
        rb.constraints            = RigidbodyConstraints.None;
        rb.linearDamping          = 0.2f;
        rb.angularDamping         = 0.5f;
        rb.automaticCenterOfMass  = true;
        rb.automaticInertiaTensor = true;
        OnCrash?.Invoke(impactVelocity);
        if (minimapCam != null) minimapCam.transform.SetParent(null);

    }

    Vector3 finishTarget;

    public void TriggerFinish(Vector3 targetPosition)
    {
        IsFinished  = true;
        finishTarget = targetPosition;
    }

    void ApplyFinishControl()
    {
        // Full braking
        if (Mathf.Abs(CurrentSpeed) > 0.1f)
            rb.AddForce(-transform.forward * Mathf.Sign(CurrentSpeed) * brakeForce, ForceMode.Force);

        float speedKph  = Mathf.Abs(CurrentSpeed) * 3.6f;
        bool  nearStop  = speedKph <= 1.5f;

        // Below 1.5 kph straighten out, otherwise steer toward finish target
        float targetLean;
        if (nearStop)
        {
            targetLean = 0f;
        }
        else
        {
            Vector3 toTarget   = Vector3.ProjectOnPlane(finishTarget - transform.position, Vector3.up).normalized;
            float   lateralDot = Vector3.Dot(transform.right, toTarget);
            targetLean = Mathf.Clamp(lateralDot * maxLeanAngle, -maxLeanAngle, maxLeanAngle);
        }

        CurrentLean = Mathf.Lerp(CurrentLean, targetLean, leanSpeed * Time.fixedDeltaTime);

        float speedFactor = Mathf.InverseLerp(0f, maxSpeed, Mathf.Abs(CurrentSpeed));
        float floorFade   = Mathf.InverseLerp(3f, 8f, Mathf.Abs(CurrentSpeed));
        float turnTorque  = (CurrentLean / maxLeanAngle) * maxTurnTorque
                            * (1f - speedFactor * 0.5f) * floorFade
                            * Mathf.Sign(CurrentSpeed);
        rb.AddRelativeTorque(Vector3.up * turnTorque, ForceMode.Acceleration);
    }

    public void SetResetPoint(Vector3 pos, Quaternion rot)
    {
        resetPosition = pos;
        resetRotation = rot;
    }
}
