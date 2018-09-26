using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallCharacterController : MonoBehaviour {

    public SphereCollider sphereCollider;
    public Rigidbody rb;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public void ActivateBallMode(Vector3 velocity)
    {
        rb.isKinematic = false;
        rb.velocity = velocity;
        rb.maxAngularVelocity = 200;
    }

    public Vector3 DeactivateBallMode()
    {
        Vector3 velocity = rb.velocity;
        Vector3 XZForward = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        if (XZForward.magnitude < 0.1f)
        {
            XZForward = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
            if (XZForward.magnitude == 0)
            {
                XZForward = Vector3.forward;
            }
        }
        Vector3 ballPosition = transform.TransformPoint(sphereCollider.center);
        transform.LookAt(transform.position + XZForward, Vector3.up);
        transform.position = ballPosition - sphereCollider.center + new Vector3(0, 0.001f, 0);
        rb.isKinematic = true;
        return velocity;
    }
}
