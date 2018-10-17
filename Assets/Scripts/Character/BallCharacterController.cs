using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallCharacterController : MonoBehaviour {

    public SphereCollider sphereCollider;
    public Rigidbody rb;
    public float moveForce = 5;
    public float maxRotationalAcceleration = 20;
    public float drag = 0.1f;
    public float qDrag = 0.1f;
    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    private void FixedUpdate()
    {
    }

    public void Move(Vector3 velocity)
    {
        Vector3 velocityXZ = velocity;
        velocityXZ.y = 0;
        Vector3 desiredRotation = Quaternion.Euler(0, 90, 0) * velocityXZ * moveForce;
        Vector3 angularVelocityXZ = rb.angularVelocity;
        angularVelocityXZ.y = 0;
        if (angularVelocityXZ.magnitude > maxRotationalAcceleration)
        {
            if (Vector3.Dot(angularVelocityXZ.normalized, desiredRotation.normalized) > 0)
            {
                velocityXZ = Vector3.Project(velocityXZ, angularVelocityXZ);
                desiredRotation = Quaternion.Euler(0, 90, 0) * velocityXZ * moveForce;
            }
        }
        rb.AddTorque(Quaternion.Euler(0, 90, 0) * velocityXZ * moveForce, ForceMode.Acceleration);
        rb.AddForce(velocityXZ * 0.01f); //Emergency force if our ball gets stuck
    }

    public void ApplyQuadraticDrag()
    {
        Vector3 appliedDrag = -qDrag * rb.velocity.normalized * rb.velocity.sqrMagnitude * Time.fixedDeltaTime;
        rb.AddForce(appliedDrag, ForceMode.VelocityChange);
    }

    public void ApplyNonQuadraticDrag()
    {
        Vector3 appliedDrag = -drag * rb.velocity.normalized * rb.velocity.magnitude * Time.fixedDeltaTime;
        rb.AddForce(appliedDrag, ForceMode.VelocityChange);
    }

    public void ApplyLinearDrag()
    {
        Vector3 appliedDrag = rb.velocity.normalized * Mathf.Min(-drag, -rb.velocity.magnitude) * Time.fixedDeltaTime;
        rb.AddForce(appliedDrag, ForceMode.VelocityChange);
    }

    public void ActivateBallMode(Vector3 velocity)
    {
        rb.isKinematic = false;
        rb.velocity = velocity;
        rb.maxAngularVelocity = 200;
    }

    public bool DeactivateBallMode(BetterCharacterController characterController, out Vector3 velocity)
    {
        velocity = rb.velocity;
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
        Vector3 savedTransformPos = transform.position;
        Quaternion savedTransformRot = transform.rotation;
        transform.LookAt(transform.position + XZForward, Vector3.up);
        transform.position = ballPosition - sphereCollider.center + new Vector3(0, 0.001f, 0);
        if (!characterController.ResolveIntersection(characterController.capsuleRadius / 2))
        {
            transform.position = savedTransformPos;
            transform.rotation = savedTransformRot;
            return false;
        }
        rb.isKinematic = true;
        characterController.enabled = true;
        return true;
    }
}
