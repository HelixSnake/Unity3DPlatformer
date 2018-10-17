using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingObject: MonoBehaviour {

    protected Rigidbody rb;
    public Vector3 velocity;
    public System.Action<Vector3> OnPositionMoved;
    public CharacterPusher characterPusher;
	// Use this for initialization
	public virtual void Start () {
        rb = this.GetComponent<Rigidbody>();
        characterPusher = this.GetComponent<CharacterPusher>();
    }
	
	// Update is called once per frame
	void Update () {
		
	}

    protected void MovePosition(Vector3 newPos)
    {
        Vector3 oldPos = transform.position;
        if (!rb) return;
        transform.position = newPos;
        velocity = newPos - oldPos;
        if (characterPusher)
        {
            characterPusher.ResolveAllCollisions();
        }
        if (OnPositionMoved != null)
        {
            OnPositionMoved(velocity);
        }
    }

    protected void MoveRotation(Quaternion newRot)
    {
        if (!rb) return;
        rb.MoveRotation(newRot);
    }
}
