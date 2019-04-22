using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControllerScript : MonoBehaviour
{
    Rigidbody2D rig;
    public float speed = 5f;

    // Start is called before the first frame update
    void Start()
    {

        rig = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        var horizontal = Input.GetAxis("Horizontal");
        var vertical = Input.GetAxis("Vertical");

        rig.AddForce(new Vector2(horizontal, vertical) * speed);
    }
}
