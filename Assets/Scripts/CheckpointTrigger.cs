using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    [Tooltip("0 = Sector 1, 1 = Sector 2, 2 = Finish")]
    public int      sectorIndex;
    public LapTimer lapTimer;

    [SerializeField] AudioSource source;
    [SerializeField] AudioClip clip;
    void OnTriggerEnter(Collider other)
    {
        if (lapTimer == null) return;
        bool b = false;
        if (other.GetComponentInParent<MotorcyclePhysics>() != null)
            b = lapTimer.OnCheckpointHit(sectorIndex, transform.position);

        if(b)
            source.PlayOneShot(clip);
    }
}
