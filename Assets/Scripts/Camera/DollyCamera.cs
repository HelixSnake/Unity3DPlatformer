using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DollyCamera : MonoBehaviour {

    public GameObject target;
    private float fPitch;
    private float fYaw;
    public float fDistance = 10;
    public float fPitchSpeed = 180;
    public float fYawSpeed = 180;
    public float fPitchLimit = 70;

    // Use this for initialization
    void Start () {
        fPitch = 0;
        fYaw = 0;
    }
	
	// Update is called once per frame
	void LateUpdate () {
        fPitch += Input.GetAxis("Vertical2") * Time.deltaTime * fPitchSpeed;
        fYaw += Input.GetAxis("Horizontal2") * Time.deltaTime * fYawSpeed;
        fPitch = Mathf.Clamp(fPitch, -fPitchLimit, fPitchLimit);

        Vector3 v3Offset = Vector3.back;
        v3Offset = Quaternion.Euler(fPitch, fYaw, 0) * v3Offset * fDistance;
        transform.position = target.transform.position + v3Offset;
        transform.LookAt(target.transform);
	}

    public float GetYaw()
    {
        return fYaw;
    }
}
