using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class SignFlashScript : MonoBehaviour
{
    [SerializeField] float flashDelay;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] List<GameObject> flashObjContainers;

    [SerializeField] Material dullMat;
    [SerializeField] Material flashMat;
    void Start()
    {
        StartCoroutine(FlashRoutine(0));
    }

    IEnumerator FlashRoutine(int index)
    {
        while (true)
        {
            int prevIndex = index;
            int nextIndex = (index + 1) % flashObjContainers.Count;

            foreach (MeshRenderer mr in flashObjContainers[prevIndex].GetComponentsInChildren<MeshRenderer>())
            {
                mr.material = dullMat;
            }
            foreach (MeshRenderer mr in flashObjContainers[nextIndex].GetComponentsInChildren<MeshRenderer>())
            {
                mr.material = flashMat;
            }

            index = nextIndex;
            yield return new WaitForSeconds(flashDelay);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
