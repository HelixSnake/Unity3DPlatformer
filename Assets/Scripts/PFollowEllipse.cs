using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PFollowEllipse : MovingObject {

    public Vector3 xDirectionAndDistance = Vector3.left;
    public Vector3 yDirectionAndDistance = Vector3.forward;
    public float timeForOneRotation = 1;
    float time = 0;
    Vector3 center;
    // Use this for initialization
    public override void Start()
    {
        base.Start();
        center = transform.position - yDirectionAndDistance;
    }
	// Update is called once per frame
	void Update () {
		
	}

    private void FixedUpdate()
    {
        time += Time.fixedDeltaTime;
        time = Mathf.Repeat(time, timeForOneRotation);
        float normalizedTime = time * Mathf.PI * 2 / timeForOneRotation;
        MovePosition(Mathf.Sin(normalizedTime) * xDirectionAndDistance + Mathf.Cos(normalizedTime) * yDirectionAndDistance + center);
    }
}
