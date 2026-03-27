using UnityEngine;
using UnityEngine.InputSystem;

public class MotorcycleInput : MonoBehaviour
{
    [Header("Input Asset")]
    public InputActionAsset actions;

    [Header("Ramp Rates")]
    public float throttleRampUp   = 3.0f;
    public float throttleRampDown = 5.0f;
    public float leanRampUp       = 2.5f;
    public float leanRampDown     = 2.0f;
    public float frontBrakeRamp   = 2.0f;
    public float rearBrakeRamp    = 4.0f;

    // Smoothed outputs — read by MotorcyclePhysics
    public float Throttle   { get; private set; }
    public float Lean       { get; private set; }   // -1 left, +1 right
    public float FrontBrake { get; private set; }
    public float RearBrake  { get; private set; }
    public bool  ResetBike  { get; private set; }

    InputAction throttleAction;
    InputAction rearBrakeAction;
    InputAction leanAction;
    InputAction frontBrakeAction;
    InputAction resetAction;

    [Header("Startup")]
    public float inputDelay = 1f;
    float startTime;

    void Awake()
    {
        startTime = Time.time;
        var map = actions.FindActionMap("Motorcycle", throwIfNotFound: true);

        throttleAction   = map.FindAction("Throttle",   throwIfNotFound: true);
        rearBrakeAction  = map.FindAction("RearBrake",  throwIfNotFound: true);
        leanAction       = map.FindAction("Lean",       throwIfNotFound: true);
        frontBrakeAction = map.FindAction("FrontBrake", throwIfNotFound: true);
        resetAction      = map.FindAction("Reset",      throwIfNotFound: true);

        map.Enable();
    }

    void OnDestroy()
    {
        actions.FindActionMap("Motorcycle")?.Disable();
    }

    void Update()
    {
        if (Time.time - startTime < inputDelay)
        {
            Throttle = FrontBrake = RearBrake = Lean = 0f;
            ResetBike = false;
            return;
        }

        float rawThrottle   = throttleAction.ReadValue<float>();
        float rawRearBrake  = rearBrakeAction.ReadValue<float>();
        float rawLean       = leanAction.ReadValue<float>();
        float rawFrontBrake = frontBrakeAction.ReadValue<float>();

        float throttleRate = rawThrottle > Throttle ? throttleRampUp : throttleRampDown;
        Throttle   = Mathf.MoveTowards(Throttle,   rawThrottle,   throttleRate   * Time.deltaTime);
        FrontBrake = Mathf.MoveTowards(FrontBrake, rawFrontBrake, frontBrakeRamp * Time.deltaTime);
        RearBrake  = Mathf.MoveTowards(RearBrake,  rawRearBrake,  rearBrakeRamp  * Time.deltaTime);

        float leanRate = Mathf.Abs(rawLean) > Mathf.Abs(Lean) ? leanRampUp : leanRampDown;
        Lean = Mathf.MoveTowards(Lean, rawLean, leanRate * Time.deltaTime);

        ResetBike = resetAction.WasPressedThisFrame();
    }
}
