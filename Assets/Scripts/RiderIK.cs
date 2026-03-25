using UnityEngine;

public class RiderIK : MonoBehaviour
{
    [Header("References")]
    public Animator animator;

    [Header("IK Targets")]
    public Transform leftHandTarget;
    public Transform rightHandTarget;
    public Transform leftFootTarget;
    public Transform rightFootTarget;
    public Transform lookTarget;

    [Header("IK Hints")]
    public Transform leftElbowHint;
    public Transform rightElbowHint;
    public Transform leftKneeHint;
    public Transform rightKneeHint;

    [Header("Weights")]
    [Range(0f, 1f)] public float handWeight = 1f;
    [Range(0f, 1f)] public float footWeight = 1f;
    [Range(0f, 1f)] public float hintWeight = 1f;
    [Range(0f, 1f)] public float lookWeight = 1f;

    [Header("Look At Settings")]
    [Range(0f, 1f)] public float bodyLookWeight = 0.2f;
    [Range(0f, 1f)] public float headLookWeight = 0.5f;
    [Range(0f, 1f)] public float eyesLookWeight = 1f;

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        // Elbow hints
        if (leftElbowHint != null)
        {
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, hintWeight);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, leftElbowHint.position);
        }
        if (rightElbowHint != null)
        {
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, hintWeight);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, rightElbowHint.position);
        }

        // Knee hints
        if (leftKneeHint != null)
        {
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftKnee, hintWeight);
            animator.SetIKHintPosition(AvatarIKHint.LeftKnee, leftKneeHint.position);
        }
        if (rightKneeHint != null)
        {
            animator.SetIKHintPositionWeight(AvatarIKHint.RightKnee, hintWeight);
            animator.SetIKHintPosition(AvatarIKHint.RightKnee, rightKneeHint.position);
        }

        // Left hand
        if (leftHandTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, handWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, handWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandTarget.rotation);
        }

        // Right hand
        if (rightHandTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, handWeight);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandTarget.rotation);
        }

        // Left foot
        if (leftFootTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, footWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, footWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFootTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftFoot, leftFootTarget.rotation);
        }

        // Right foot
        if (rightFootTarget != null)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, footWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, footWeight);
            animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFootTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightFoot, rightFootTarget.rotation);
        }

        // Look direction
        if (lookTarget != null)
        {
            animator.SetLookAtWeight(lookWeight, bodyLookWeight, 
                headLookWeight, eyesLookWeight);
            animator.SetLookAtPosition(lookTarget.position);
        }
    }
}