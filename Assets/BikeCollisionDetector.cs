using UnityEngine;
using UnityEngine.SceneManagement;

public class BikeCollisionDetector : MonoBehaviour
{
    public MotorcyclePhysics physics;
    public MotorcycleInput   input;
    public float crashSpeedThreshold = 5f;

    void OnCollisionEnter(Collision col)
    {
        if (physics == null || physics.IsCrashed) return;

        if (Mathf.Abs(physics.CurrentSpeed) > crashSpeedThreshold)
        {
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
