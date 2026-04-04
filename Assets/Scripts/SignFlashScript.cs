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
        GameObject prev;
        GameObject curr;
        if (index+1 < flashObjContainers.Count)
        {
            prev = flashObjContainers[index];
            curr = flashObjContainers[index+1];
            index++;
        }
        else
        {
            prev = flashObjContainers[flashObjContainers.Count-1];
            curr = flashObjContainers[0];
            index = 0;
        }
        foreach(MeshRenderer mr in prev.GetComponentsInChildren<MeshRenderer>())
        {
            mr.material = dullMat;
        }
        foreach(MeshRenderer mr in curr.GetComponentsInChildren<MeshRenderer>())
        {
            mr.material = flashMat;
        }
        index++;
        
        yield return new WaitForSeconds(flashDelay);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
