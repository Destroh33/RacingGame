using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("0 = Sector 1, 1 = Sector 2, 2 = Finish")]
    public int      sectorIndex;
    public LapTimer lapTimer;

    void OnTriggerEnter(Collider other)
    {
        if (lapTimer == null) return;
        if (other.GetComponentInParent<MotorcyclePhysics>() != null)
            lapTimer.OnCheckpointHit(sectorIndex, transform.position);
    }
}
