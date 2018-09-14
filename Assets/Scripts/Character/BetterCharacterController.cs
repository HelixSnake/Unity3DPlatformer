using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BetterCharacterController : MonoBehaviour {
    
    private const float GROUND_CHECK_DISTANCE = 0.01f;
    private const int MAX_PHYSICS_SLIDE_ITERATIONS = 20;
    private const int FAKE_CAPSULE_CAST_BOXES = 10;
    private const float STAIR_SWEEPER_THICKNESS = 0.001f;
    public CapsuleCollider capsuleCollider;
    public float slopeLimit = 45;
    public float skinWidth = 0.5f;
    public float minMoveDistance = 0.001f;
    [Tooltip("If we're at the edge of the platform and sunk slightly below, how fast should we rise up")]
    public float platformRisingSpeed = 1;
    public float maxStairHeight = 0.3f;
    [Tooltip("Divide the stair checks into this many cubes to increase accuracy")]
    public float numStairChecks = 1; //Todo: implement this
    [Tooltip("When climbing, this is the angle the stair check should be. The character will move at this angle when climbing stairs.")]
    public float stairClimbAngle = 45;
    [HideInInspector]
    [System.NonSerialized]
    public bool isGrounded;
    private float cosSlopeLimit;

    public delegate void CollisionEvent(Vector3 impactPoint, Vector3 normal, Collider collider);
    public event CollisionEvent OnMoveGroundCollision;
    public event CollisionEvent OnMoveCollision; // doesn't fire if we collide with the ground, usually
    private int geometryCollisionLayers;
    private Vector3 lastMoveVector;

    // Use this for initialization
    void Start () {
        cosSlopeLimit = Mathf.Cos(slopeLimit * Mathf.Deg2Rad);
        geometryCollisionLayers = 1 << LayerMask.NameToLayer("Geometry");

    }
	
	// Update is called once per frame
	void Update () {
	}

    private void FixedUpdate()
    {
        TestForGround();
    }

    private void TestForGround()
    {
        //check to see if we're climbing stairs; if so, stay on ground
        if (isGrounded)
        {
            if (StairClimbSweep(ref lastMoveVector, true))
                return;
        }

        float riseAmount = 0;
        Vector3 capsuleCenter = transform.TransformPoint(capsuleCollider.center);
        float pointDist = capsuleCollider.height / 2 - capsuleCollider.radius;
        RaycastHit[] raycasthits;
        raycasthits = Physics.CapsuleCastAll(capsuleCenter - pointDist * Vector3.down, capsuleCenter - pointDist * Vector3.up, capsuleCollider.radius,
            Vector3.down, capsuleCollider.radius + GROUND_CHECK_DISTANCE + skinWidth, geometryCollisionLayers); //first capsule cast, checking for ground. Casts an extra radius and ignores results within the lower hemisphere, in order to imitate a cylinder
        isGrounded = false;
        bool hitGround = false; // did we hit something that's within our "cylinder" the bottom of which is GROUND_CHECK_DISTANCE lower than the bottom of the collision capsule
        foreach (RaycastHit hit in raycasthits)
        {
            if (hit.point.y > capsuleCenter.y - capsuleCollider.height / 2 - GROUND_CHECK_DISTANCE - skinWidth) //calculating the distance if we were casting a cylinder instead of a capsule
            {
                riseAmount = hit.point.y - (capsuleCenter.y - capsuleCollider.height / 2 - skinWidth); //calculating the distance if we were casting a cylinder instead of a capsule
                riseAmount = Mathf.Max(riseAmount, 0);
                hitGround = true;
                if (Vector3.Dot(hit.normal, Vector3.up) > cosSlopeLimit)
                {
                    isGrounded = true;
                    break;
                }
            }
        }
        if (hitGround && !isGrounded)
        {
            RaycastHit groundHitinfo;
            if (PhysHelper.FakeCylinderCast(FAKE_CAPSULE_CAST_BOXES, capsuleCollider.height - capsuleCollider.radius * 2, capsuleCollider.radius, capsuleCenter, Vector3.down, out groundHitinfo,
                capsuleCollider.radius + skinWidth, geometryCollisionLayers, true)) // fake cylinder cast, checking for steep slopes and prevent us from thinking we're on ground if the slope is too steep
            {
                if (Vector3.Dot(groundHitinfo.normal, Vector3.up) > cosSlopeLimit)
                {
                    isGrounded = true;
                }
            }
        }
        if (isGrounded)
        {
            float amtToMoveUp = Mathf.Min(platformRisingSpeed * Time.fixedDeltaTime, riseAmount);
            RaycastHit hitinfo;
            bool hitUp = Physics.CapsuleCast(capsuleCenter - pointDist * Vector3.down, capsuleCenter - pointDist * Vector3.up, capsuleCollider.radius,
            Vector3.up, out hitinfo, amtToMoveUp + skinWidth, geometryCollisionLayers); // cast to check if we can rise
            if (!hitUp) AttemptMove(transform.position + Vector3.up * amtToMoveUp);
            else if (hitinfo.distance > skinWidth)
            {
                AttemptMove(transform.position + Vector3.up * (hitinfo.distance - skinWidth));
            }
        }
    }
    ///<summary>Move the character in a vector direction, with corrections for platform edges and steep slopes</summary>
    public void Move(Vector3 moveVector)
    {
        lastMoveVector = moveVector;
        Vector3 newMoveVector = moveVector;
        Vector3 capsuleCenter = transform.TransformPoint(capsuleCollider.center);
        float pointDist = capsuleCollider.height / 2 - capsuleCollider.radius;
        RaycastHit[] raycasthits;
        if (moveVector.y < 0)
        {
            raycasthits = Physics.CapsuleCastAll(capsuleCenter - pointDist * Vector3.down, capsuleCenter - pointDist * Vector3.up, capsuleCollider.radius,
            Vector3.down, capsuleCollider.radius - moveVector.y + skinWidth, geometryCollisionLayers); // first capsule cast, checking for ground. Casts an extra radius and ignores results within the lower hemisphere, in order to imitate a cylinder
            float minDistance = -moveVector.y;
            bool hitGround = false;
            Vector3 groundHitNormal = Vector3.zero;
            Vector3 groundHitPoint = Vector3.zero;
            Collider groundHitCollider = null;
            foreach (RaycastHit hit in raycasthits)
            {
                float distToPoint = (capsuleCenter.y - capsuleCollider.height / 2) - hit.point.y; //calculating the distance if we were casting a cylinder instead of a capsule
                if (distToPoint < minDistance)
                {
                    hitGround = true;
                    minDistance = distToPoint;
                    groundHitNormal = hit.normal;
                    groundHitPoint = hit.point;
                    groundHitCollider = hit.collider;
                }
            }
            if (hitGround)
            {
                RaycastHit groundHitinfo;
                if (PhysHelper.FakeCylinderCast(FAKE_CAPSULE_CAST_BOXES, capsuleCollider.height - capsuleCollider.radius * 2, capsuleCollider.radius, capsuleCenter, Vector3.down, out groundHitinfo,
                    capsuleCollider.radius - moveVector.y + skinWidth, geometryCollisionLayers)) // Fake cylinder cast to check for steep slopes and prevent stopping if the slope is too steep
                {
                    if (Vector3.Dot(groundHitinfo.normal, Vector3.up) > cosSlopeLimit)
                    {
                        newMoveVector.y = -(minDistance - skinWidth);
                        if (OnMoveGroundCollision != null)
                        {
                            OnMoveGroundCollision(groundHitPoint, groundHitNormal, groundHitCollider);
                        }
                    }
                }
            }
        }

        // Adjust for climbing stairs
        if (isGrounded)
        {
            StairClimbSweep(ref newMoveVector, false);
        }

        RaycastHit hitinfo;
        if (Physics.CapsuleCast(capsuleCenter - pointDist * Vector3.down, capsuleCenter - pointDist * Vector3.up, capsuleCollider.radius,
        newMoveVector.normalized, out hitinfo, newMoveVector.magnitude + skinWidth, geometryCollisionLayers))
        {
            float newSkinWidth = skinWidth / Vector3.Dot(-newMoveVector.normalized, hitinfo.normal);
            AttemptMove(transform.position + newMoveVector.normalized * (hitinfo.distance - newSkinWidth));
            Vector3 remainingVector = newMoveVector - newMoveVector.normalized * (hitinfo.distance - newSkinWidth);
            Vector3 slideVector = Vector3.ProjectOnPlane(remainingVector, hitinfo.normal);
            if (isGrounded && newMoveVector.y <= 0 && slideVector.y > 0 && Vector3.Dot(hitinfo.normal, Vector3.up) < cosSlopeLimit && Vector3.Dot(hitinfo.normal, Vector3.up) > 0) // If we've hit a steep slope, stop horizontal momentum from turning into additional vertical momentum.
            {
                Vector3 crossVector = Vector3.Cross(hitinfo.normal, Vector3.up);
                slideVector = Vector3.Project(slideVector, crossVector);
            }
            if (OnMoveCollision != null)
            {
                OnMoveCollision(hitinfo.point, hitinfo.normal, hitinfo.collider);
            }
            DoMoveSlide(slideVector, MAX_PHYSICS_SLIDE_ITERATIONS); // see DoMoveSlide comment
        }
        else
        {
            AttemptMove(transform.position + newMoveVector);
        }
    }

    // When we hit something, we need to continue our movement instead of just stopping, to prevent things like freezing when running into walls
    private void DoMoveSlide(Vector3 inVector, int iterations)
    {
        if (iterations == 0) return;
        Vector3 capsuleCenter = transform.TransformPoint(capsuleCollider.center);
        float pointDist = capsuleCollider.height / 2 - capsuleCollider.radius;
        RaycastHit hitinfo;
        if (Physics.CapsuleCast(capsuleCenter - pointDist * Vector3.down, capsuleCenter - pointDist * Vector3.up, capsuleCollider.radius,
        inVector.normalized, out hitinfo, inVector.magnitude + skinWidth, geometryCollisionLayers)) // Capsule Cast that checks our slide move, the same way as you would for a regular move
        {
            float newSkinWidth = skinWidth / Vector3.Dot(-inVector.normalized, hitinfo.normal);
            AttemptMove(transform.position + inVector.normalized * (hitinfo.distance - newSkinWidth));
            Vector3 remainingVector = inVector - inVector.normalized * (hitinfo.distance - newSkinWidth);
            Vector3 slideVector = Vector3.ProjectOnPlane(remainingVector, hitinfo.normal);
            if (isGrounded && slideVector.y > 0 && Vector3.Dot(hitinfo.normal, Vector3.up) < cosSlopeLimit && Vector3.Dot(hitinfo.normal, Vector3.up) > 0) // If we've hit a steep slope, stop horizontal momentum from turning into additional vertical momentum.
            {
                Vector3 crossVector = Vector3.Cross(hitinfo.normal, Vector3.up);
                slideVector = Vector3.Project(slideVector, crossVector);
            }
            if (OnMoveCollision != null)
            {
                OnMoveCollision(hitinfo.point, hitinfo.normal, hitinfo.collider);
            }
            DoMoveSlide(slideVector, iterations - 1); // Run this function recursively. It can run a total of MAX_PHYSICS_SLIDE_ITERATIONS times, but most of the time it won't run that many.
        }
        else
        {
            AttemptMove(transform.position + inVector);
        }
    }

    // Stair climbing sweep: a very thin box, facing diagonally up, is swept forward in front of our character, to allow for smooth stair climbing. To understand how this works, uncomment the DrawBoxCast calls.
    private bool StairClimbSweep(ref Vector3 movement, bool groundCheck)
    {
        Vector3 capsuleCenter = transform.TransformPoint(capsuleCollider.center);
        if (Mathf.Abs(movement.x) < 0.001 && Mathf.Abs(movement.z) < 0.001) return false;
        Vector3 XZMovement = new Vector3(movement.x, 0, movement.z);
        Vector3 halfExtents = new Vector3(capsuleCollider.radius, STAIR_SWEEPER_THICKNESS, maxStairHeight / Mathf.Sin(stairClimbAngle * Mathf.Deg2Rad) / 2);
        float startDistance = maxStairHeight / Mathf.Tan(stairClimbAngle * Mathf.Deg2Rad) / 2;
        Vector3 startPos = new Vector3(0, maxStairHeight / 2 + STAIR_SWEEPER_THICKNESS, 0);
        startPos += XZMovement.normalized * startDistance;
        startPos += capsuleCenter + (capsuleCollider.height / 2) * Vector3.down;
        Quaternion yawRotation = Quaternion.LookRotation(new Vector3(movement.x, 0, movement.z));
        Quaternion pitchRotation = Quaternion.Euler(-stairClimbAngle, 0, 0);
        RaycastHit hitinfo = new RaycastHit();
        Quaternion orientation = yawRotation * pitchRotation;
        Vector3 castVector = XZMovement.normalized * (XZMovement.magnitude + capsuleCollider.radius);
        if (Physics.BoxCast(startPos, halfExtents, castVector.normalized, out hitinfo, orientation,
            castVector.magnitude, geometryCollisionLayers))
        {
            PhysHelper.DrawBoxCast(halfExtents, startPos, startPos + hitinfo.distance * castVector.normalized, orientation, true);
            return true;
        }
        else
        {
            return false;
            PhysHelper.DrawBoxCast(halfExtents, startPos, startPos + castVector, orientation, false);
        }
    }

    private bool AttemptMove(Vector3 position)
    {
        float pointDist = capsuleCollider.height / 2 - capsuleCollider.radius;
        RaycastHit[] hitinfos = Physics.CapsuleCastAll(position + pointDist * Vector3.up, position + pointDist * Vector3.down, capsuleCollider.radius, Vector3.down, 0.001f, geometryCollisionLayers);
        foreach (RaycastHit hitinfo in hitinfos)
        {
            if (hitinfo.distance == 0) // If the attempted move position is obstructed
            {
                return false;
            }
        }
        transform.position = position;
        return true;
    }
}
