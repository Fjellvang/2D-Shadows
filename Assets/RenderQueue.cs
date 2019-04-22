using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RenderQueue : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<MeshRenderer>().material.renderQueue = 2000;        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
