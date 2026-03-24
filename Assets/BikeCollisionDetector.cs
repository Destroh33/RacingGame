using UnityEngine;
using UnityEngine.SceneManagement;

public class BikeCollisionDetector : MonoBehaviour
{
    public MotorcyclePhysics physics;
    public MotorcycleInput   input;
    public float crashSpeedThreshold = 5f;

    public BoxCollider box;

    void OnCollisionEnter(Collision col)
    {
        if (physics == null || physics.IsCrashed) return;

        if (Mathf.Abs(physics.CurrentSpeed) > crashSpeedThreshold)
        {
            if (box != null)
            {
                box.center = new Vector3(box.center.x, 0.61f, box.center.z);
                box.size   = new Vector3(box.size.x,   1.22f, box.size.z);
            }

            physics.TriggerCrash(physics.GetComponent<Rigidbody>().linearVelocity);
        }
    }

    void Update()
    {
        if (!physics.IsCrashed) return;
        if (input == null || !input.ResetBike) return;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
