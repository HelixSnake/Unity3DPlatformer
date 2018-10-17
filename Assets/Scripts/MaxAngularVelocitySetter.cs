using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MaxAngularVelocitySetter : MonoBehaviour {

    public float maxAngularVelocity = 100;

	// Use this for initialization
	void Start () {
        Rigidbody rb = this.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.maxAngularVelocity = maxAngularVelocity;
        }
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
