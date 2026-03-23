using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MotorcyclePhysics : MonoBehaviour
{
    [Header("References")]
    public MotorcycleInput input;
    public Transform bikeModel;          // Visual model — rotated for lean
    public Transform frontWheelPos;      // Suspension raycast origin
    public Transform rearWheelPos;

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
    public float dragTucked           = 0.4f;

    [Header("Hang Off")]
    public float hangOffLeanReduction = 0.75f;  // multiplier on lean needed for same turn

    [Header("Reset")]
    public Vector3 resetOffset        = new Vector3(0f, 1f, 0f);

    // --- Public state read by camera / visuals / IK ---
    public float CurrentLean    { get; private set; }
    public float CurrentSpeed   { get; private set; }
    public bool  IsGrounded     { get; private set; }
    public bool  IsSliding      { get; private set; }
    public bool  IsTucked       => input != null && input.Tuck;

    Rigidbody rb;
    float prevFrontCompression;
    float prevRearCompression;
    Vector3 resetPosition;
    Quaternion resetRotation;

    // Debug readout — remove once suspension is tuned
    float debugFrontCompression, debugFrontForce;
    float debugRearCompression,  debugRearForce;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.1f, 0f);
        // Lean and pitch are cosmetic — keep the rigidbody upright
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.angularDamping = steeringDamping;
        resetPosition = transform.position;
        resetRotation = transform.rotation;
    }

    void FixedUpdate()
    {
        if (input == null) return;

        if (input.ResetBike)
        {
            Reset();
            return;
        }

        CurrentSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        SuspensionAndGround();
        ApplyDrag();
        ApplyThrottle();
        ApplyBraking();
        ApplyLeanSteering();
        ApplyGrip();
        UpdateVisualLean();
    }

    void SuspensionAndGround()
    {
        bool frontHit = SuspensionRay(frontWheelPos, ref prevFrontCompression, out debugFrontCompression, out debugFrontForce);
        bool rearHit  = SuspensionRay(rearWheelPos,  ref prevRearCompression,  out debugRearCompression,  out debugRearForce);
        IsGrounded = frontHit || rearHit;
    }

    bool SuspensionRay(Transform origin, ref float prevCompression, out float outCompression, out float outForce)
    {
        outCompression = 0f;
        outForce       = 0f;
        if (origin == null) return false;

        float rayLength = suspensionRestLength + suspensionTravel + wheelRadius;
        bool didHit = Physics.Raycast(origin.position, -transform.up, out RaycastHit hit, rayLength, groundMask, QueryTriggerInteraction.Ignore);

        if (!didHit) return false;

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

    void ApplyDrag()
    {
        rb.linearDamping = input.Tuck ? dragTucked : dragNormal;
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

        // Hang off allows same turn with less lean
        float leanInput = input.Lean;
        if (input.HangOff)
            leanInput *= hangOffLeanReduction;

        float targetLean = leanInput * effectiveMaxLean;
        float rate = Mathf.Abs(targetLean) > Mathf.Abs(CurrentLean) ? effectiveLeanRate : leanReturnSpeed;
        CurrentLean = Mathf.Lerp(CurrentLean, targetLean, rate * Time.fixedDeltaTime);

        // Yaw torque from lean — actually rotates the bike to face a new direction
        // Floor fades in from 0→1 over the first 5 m/s so you can't turn at standstill
        float floorFade = Mathf.InverseLerp(0f, 5f, Mathf.Abs(CurrentSpeed));
        float effectiveSpeedFactor = Mathf.Max(speedFactor, 0.3f * floorFade);
        float turnTorque = (CurrentLean / effectiveMaxLean) * maxTurnTorque * effectiveSpeedFactor;
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
        bikeModel.localRotation = Quaternion.Euler(0f, 0f, -CurrentLean);
    }

    void Reset()
    {
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(resetPosition + resetOffset, resetRotation);
        CurrentLean = 0f;
        if (bikeModel != null) bikeModel.localRotation = Quaternion.identity;
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 260, 110));
        GUI.color = Color.black;
        GUILayout.Label($"Front  compression: {debugFrontCompression:F3}  force: {debugFrontForce:F0} N");
        GUILayout.Label($"Rear   compression: {debugRearCompression:F3}  force: {debugRearForce:F0} N");
        GUILayout.Label($"Grounded: {IsGrounded}   Speed: {CurrentSpeed:F1} m/s");
        GUILayout.Label($"Mass: {(rb != null ? rb.mass : 0f):F0} kg   Need: {(rb != null ? rb.mass * Mathf.Abs(Physics.gravity.y) : 0f):F0} N total");
        GUILayout.EndArea();
    }

    void OnDrawGizmos()
    {
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
    public void SetResetPoint(Vector3 pos, Quaternion rot)
    {
        resetPosition = pos;
        resetRotation = rot;
    }
}
