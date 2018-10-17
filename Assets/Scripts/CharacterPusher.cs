using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterPusher : MonoBehaviour {

    protected Rigidbody rb;
    protected Collider[] colliders;
    private LayerMask charactersLayerMask;
    // Use this for initialization
    void Start () {
        rb = this.GetComponent<Rigidbody>();
        colliders = this.GetComponentsInChildren<Collider>();
        charactersLayerMask = 1 << LayerMask.NameToLayer("MainCharacter");
    }
	
	// Update is called once per frame
	void Update () {
		
	}

    private void FixedUpdate()
    {
    }

    public void ResolveAllCollisions()
    {
        if (!rb) return;
        foreach (Collider c in colliders)
        {
            ResolveCollisions(c);
        }
    }

    private void ResolveCollisions(Collider collider)
    {
        Collider[] overlappingColliders = Physics.OverlapBox(collider.bounds.center, collider.bounds.extents, Quaternion.identity, charactersLayerMask);
        foreach (Collider c in overlappingColliders)
        {
            BetterCharacterController controller = c.GetComponent<BetterCharacterController>();
            if (controller)
            {
                controller.ResolveMovingObjIntersection(collider);
            }
        }
    }
}
