using UnityEngine;

public class RiderRagdoll : MonoBehaviour
{
    public MotorcyclePhysics physics;
    public Animator animator;

    Rigidbody[] ragdollBodies;
    Collider[]  ragdollColliders;

    void Awake()
    {
        ragdollBodies    = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();
        SetRagdoll(false);
    }

    void OnEnable()
    {
        if (physics != null)
            physics.OnCrash += Activate;
    }

    void OnDisable()
    {
        if (physics != null)
            physics.OnCrash -= Activate;
    }

    void Activate(Vector3 impactVelocity)
    {
        // Unparent so the bike tumbling doesn't drag the ragdoll
        transform.SetParent(null, worldPositionStays: true);

        SetRagdoll(true);

        if (animator != null)
            animator.enabled = false;

        foreach (Rigidbody rb in ragdollBodies)
            rb.linearVelocity = impactVelocity;
    }

    void SetRagdoll(bool active)
    {
        foreach (Rigidbody rb in ragdollBodies)
            rb.isKinematic = !active;

        foreach (Collider col in ragdollColliders)
            col.enabled = active;
    }
}
