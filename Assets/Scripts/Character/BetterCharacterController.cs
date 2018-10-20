using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DebugPhysics = RotaryHeart.Lib.PhysicsExtension.Physics;
using DebugExtentions = RotaryHeart.Lib.PhysicsExtension.DebugExtensions;

public class BetterCharacterController : MonoBehaviour {

    private const float GROUND_CHECK_DISTANCE = 0.01f;
    private const int MAX_PHYSICS_SLIDE_ITERATIONS = 20;
    private const int FAKE_CAPSULE_CAST_BOXES = 10;
    private const float STAIR_SWEEPER_THICKNESS = 0.001f;
    private const float STUCK_BETWEEN_SLOPES_TEST_VALUE = 0.001f;
    private const float STUCK_BETWEEN_SLOPES_MIN_Y = 0.01f;
    //public CapsuleCollider capsuleCollider;
    public float capsuleRadius = 0.5f;
    public float capsuleHeight = 2.0f;
    public float slopeLimit = 45;
    public float skinWidth = 0.005f;
    public float minMoveDistance = 0.001f;
    [Tooltip("If we're at the edge of the platform and sunk slightly below, how fast should we rise up")]
    public float platformRisingSpeed = 1;
    public float maxStairHeight = 0.3f;
    [Tooltip("Divide the stair checks into this many cubes to increase accuracy")]
    public float numStairChecks = 2; //use at least 2 for proper results
    [Tooltip("When climbing, this is the angle the stair check should be. The character will move at this angle when climbing stairs.")]
    public float stairClimbAngle = 45; // For best results this should be equal to or less than slopeLimit
    //[HideInInspector]
    //[System.NonSerialized]
    public bool isGrounded;
    public float onStairsDelayBeforeFalling = 0.1f;
    public bool isMovingUp = false;

    private float onStairsFallDelayTimer = 0;
    private float cosSlopeLimit;
    private float sinStairClimbAngle;
    private float cosStairClimbAngle;
    private float tanStairClimbAngle;

    public delegate void CollisionEvent(Vector3 impactPoint, Vector3 normal, Collider collider);
    public event CollisionEvent OnMoveGroundCollision;
    public event CollisionEvent OnMoveCollision; // doesn't fire if we collide with the ground, usually
    public event System.Action<Vector3> OnDisconnectFromGroundParent;
    private int geometryCollisionLayers;
    private int MovingGeometryLayer;
    private Vector3 lastMoveVector;
    private bool stuckBetweenSlopes;
    private CapsuleCollider capsuleCollider;
    public MovingObject groundParent;
    private Vector3 lastGroundParentVelocity;

    // Use this for initialization
    void Start() {
        capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
        capsuleCollider.direction = 1;
        capsuleCollider.height = capsuleHeight;
        capsuleCollider.radius = capsuleRadius;
        cosSlopeLimit = Mathf.Cos(slopeLimit * Mathf.Deg2Rad);
        sinStairClimbAngle = Mathf.Sin(stairClimbAngle * Mathf.Deg2Rad);
        cosStairClimbAngle = Mathf.Cos(stairClimbAngle * Mathf.Deg2Rad);
        tanStairClimbAngle = Mathf.Tan(stairClimbAngle * Mathf.Deg2Rad);
        geometryCollisionLayers = 1 << LayerMask.NameToLayer("StaticGeometry") | 1 << LayerMask.NameToLayer("MovingGeometry");
        MovingGeometryLayer = LayerMask.NameToLayer("MovingGeometry");
    }

    // Update is called once per frame
    void Update() {
        Vector3 capsuleCenter = transform.position;
        float pointDist = capsuleHeight / 2 - capsuleRadius;

        DebugExtentions.DebugCapsule(capsuleCenter + pointDist * Vector3.down, capsuleCenter + pointDist * Vector3.up, Color.green, capsuleRadius);
        DebugExtentions.DebugCircle(capsuleCenter + capsuleHeight / 2 * Vector3.down, Vector3.up, Color.green, capsuleRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 capsuleCenter = transform.position;
        float pointDist = capsuleHeight / 2 - capsuleRadius;
        DebugExtentions.DebugCapsule(capsuleCenter + pointDist * Vector3.down, capsuleCenter + pointDist * Vector3.up, Color.green, capsuleRadius);
    }

    private void FixedUpdate()
    {
        if (!TestForGround())
        {
            ClearGroundParent();
        }
    }

    private void OnDisable()
    {
        if (capsuleCollider)
            capsuleCollider.enabled = false;
    }
    private void OnEnable()
    {
        if (capsuleCollider)
            capsuleCollider.enabled = true;
    }

    private bool TestForGround() // returns false if we skip the test for whetever reason
    {
        if (stuckBetweenSlopes)
        {
            stuckBetweenSlopes = false;
            return false;
        }
        if (isMovingUp) {
            isGrounded = false;
            return false;
        }
        //check to see if we're climbing stairs; if so, stay on ground
        if (isGrounded)
        {
            if (StairClimbSweep(ref lastMoveVector, true).Length > 0)
            {
                onStairsFallDelayTimer = onStairsDelayBeforeFalling;
                return false;
            }
            else
            {
                onStairsFallDelayTimer -= Time.fixedDeltaTime;
                if (onStairsFallDelayTimer > 0)
                    return false;
            }
        }

        float riseAmount = 0;
        Vector3 capsuleCenter = transform.position;
        float pointDist = capsuleHeight / 2 - capsuleRadius;
        RaycastHit[] raycasthits;
        raycasthits = Physics.CapsuleCastAll(capsuleCenter - pointDist * Vector3.down, capsuleCenter - pointDist * Vector3.up, capsuleRadius,
            Vector3.down, capsuleRadius + GROUND_CHECK_DISTANCE + skinWidth, geometryCollisionLayers); //first capsule cast, checking for ground. Casts an extra radius and ignores results within the lower hemisphere, in order to imitate a cylinder
        isGrounded = false;
        Collider groundCollider = null;
        bool hitGround = false; // did we hit something that's within our "cylinder" the bottom of which is GROUND_CHECK_DISTANCE lower than the bottom of the collision capsule
        foreach (RaycastHit hit in raycasthits)
        {
            if (hit.point.y > capsuleCenter.y - capsuleHeight / 2 - GROUND_CHECK_DISTANCE - skinWidth) //calculating the distance if we were casting a cylinder instead of a capsule
            {
                riseAmount = hit.point.y - (capsuleCenter.y - capsuleHeight / 2 - skinWidth); //calculating the distance if we were casting a cylinder instead of a capsule
                riseAmount = Mathf.Max(riseAmount, 0);
                hitGround = true;
                if (IsGround(hit.normal))
                {
                    isGrounded = true;
                    groundCollider = hit.collider;
                    break;
                }
            }
        }
        if (hitGround && !isGrounded)
        {
            RaycastHit groundHitinfo;
            if (PhysHelper.FakeCylinderCast(FAKE_CAPSULE_CAST_BOXES, capsuleHeight - capsuleRadius * 2, capsuleRadius, capsuleCenter, Vector3.down, out groundHitinfo,
                capsuleRadius + skinWidth, geometryCollisionLayers, false)) // fake cylinder cast, checking for steep slopes and prevent us from thinking we're on ground if the slope is too steep
            {
                if (IsGround(groundHitinfo.normal))
                {
                    isGrounded = true;
                }
            }
        }
        bool groundParentFound = false;
        if (isGrounded)
        {
            float amtToMoveUp = Mathf.Min(platformRisingSpeed * Time.fixedDeltaTime, riseAmount);
            RaycastHit hitinfo;
            bool hitUp = Physics.CapsuleCast(capsuleCenter - pointDist * Vector3.down, capsuleCenter - pointDist * Vector3.up, capsuleRadius,
            Vector3.up, out hitinfo, amtToMoveUp + skinWidth, geometryCollisionLayers); // cast to check if we can rise
            if (!hitUp) AttemptSetPosition(transform.position + Vector3.up * amtToMoveUp);
            else if (hitinfo.distance > skinWidth)
            {
                AttemptSetPosition(transform.position + Vector3.up * (hitinfo.distance - skinWidth));
            }
            if (groundCollider && groundCollider.gameObject.layer == MovingGeometryLayer)
            {
                MovingObject groundMovingObject = groundCollider.GetComponent<MovingObject>();
                if (groundMovingObject)
                {
                    SetGroundParent(groundMovingObject);
                    groundParentFound = true;
                }
            }
        }
        if (!groundParentFound)
        {
            ClearGroundParent();
        }
        return true;
    }

    ///<summary>Move the character in a vector direction, with corrections for platform edges and steep slopes</summary>
    public void Move(Vector3 moveVector, bool jump = false, bool doKeepOnGroundMove = true)
    {
        lastMoveVector = moveVector;
        float startY = transform.position.y;
        Vector3 newMoveVector = moveVector;
        Vector3 capsuleCenter = transform.position;
        float pointDist = capsuleHeight / 2 - capsuleRadius;

        RaycastHit[] raycasthits;
        if (moveVector.y < 0)
        {
            raycasthits = Physics.CapsuleCastAll(capsuleCenter - pointDist * Vector3.down, capsuleCenter - pointDist * Vector3.up, capsuleRadius,
            Vector3.down, capsuleRadius - moveVector.y + skinWidth, geometryCollisionLayers); // first capsule cast, checking for ground. Casts an extra radius and ignores results within the lower hemisphere, in order to imitate a cylinder
            float minDistance = -moveVector.y;
            bool hitGround = false;
            Vector3 groundHitNormal = Vector3.zero;
            Vector3 groundHitPoint = Vector3.zero;
            Collider groundHitCollider = null;
            foreach (RaycastHit hit in raycasthits)
            {
                float distToPoint = (capsuleCenter.y - capsuleHeight / 2) - hit.point.y; //calculating the distance if we were casting a cylinder instead of a capsule
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
                if (PhysHelper.FakeCylinderCast(FAKE_CAPSULE_CAST_BOXES, capsuleHeight - capsuleRadius * 2, capsuleRadius, capsuleCenter, Vector3.down, out groundHitinfo,
                    capsuleRadius - moveVector.y + skinWidth, geometryCollisionLayers)) // Fake cylinder cast to check for steep slopes and prevent stopping if the slope is too steep
                {
                    if (IsGround(groundHitinfo.normal))
                    {
                        newMoveVector.y = -(minDistance - skinWidth);
                        newMoveVector.y = Mathf.Min(newMoveVector.y, 0); //Emergency edge case fixer in case for some reason we end up with upward movement
                        if (OnMoveGroundCollision != null)
                        {
                            OnMoveGroundCollision(groundHitPoint, groundHitNormal, groundHitCollider);
                        }
                        isGrounded = true;
                    }
                }
            }
        }
        Vector3[] stairLocations = new Vector3[0];
        // Adjust for climbing stairs
        if (isGrounded)
        {
            stairLocations = StairClimbSweep(ref newMoveVector, false);
        }

        /*float distanceToStairPlanes = newMoveVector.magnitude;
        foreach (Vector3 stairLocation in stairLocations)
        {
            float stairPlaneDistance = SweepToStairClimbPlane(newMoveVector, stairLocation);
            if (stairPlaneDistance < distanceToStairPlanes)
                distanceToStairPlanes = stairPlaneDistance;
        }*/

        RaycastHit hitinfo;
        if (Physics.CapsuleCast(capsuleCenter - pointDist * Vector3.down, capsuleCenter - pointDist * Vector3.up, capsuleRadius,
        newMoveVector.normalized, out hitinfo, newMoveVector.magnitude + skinWidth, geometryCollisionLayers))
        {
            if (stairLocations.Length > 0 && Vector3.Dot(newMoveVector.normalized, Vector3.up) < sinStairClimbAngle - 0.001f) // if we hit the stair climbing plane before we hit our target. If our move vector is steeper than the stair climbing angle, disregard.
            {
                //Debug.DrawRay(transform.position - (capsuleHeight / 2 - 0.01f) * Vector3.down, newMoveVector.normalized * distanceToStairPlanes);

                //AttemptMove(transform.position + newMoveVector.normalized * distanceToStairPlanes);
                //Vector3 remainingVector = newMoveVector - newMoveVector.normalized * (hitinfo.distance);
                Vector3 stairSlideVector = GetStairClimbVector(moveVector);

                //Debug.DrawRay(transform.position + (capsuleHeight / 2 - 0.01f) * Vector3.down + newMoveVector.normalized * distanceToStairPlanes, stairSlideVector);

                DoMoveSlide(stairSlideVector, MAX_PHYSICS_SLIDE_ITERATIONS, stairLocations, true); // see DoMoveSlide comment
            }
            else
            {
                float newSkinWidth = skinWidth / Vector3.Dot(-newMoveVector.normalized, hitinfo.normal);
                newSkinWidth = Mathf.Max(newSkinWidth, 0);
                AttemptSetPosition(transform.position + newMoveVector.normalized * (hitinfo.distance - newSkinWidth));
                Vector3 remainingVector = newMoveVector - newMoveVector.normalized * (hitinfo.distance - newSkinWidth);
                Vector3 slideVector = Vector3.ProjectOnPlane(remainingVector, hitinfo.normal);
                if (isGrounded && newMoveVector.y <= 0 && slideVector.y > 0 && !IsGround(hitinfo.normal) && Vector3.Dot(hitinfo.normal, Vector3.up) > 0) // If we've hit a steep slope, stop horizontal momentum from turning into additional vertical momentum.
                {
                    Vector3 crossVector = Vector3.Cross(hitinfo.normal, Vector3.up);
                    slideVector = Vector3.Project(slideVector, crossVector);
                }
                if (OnMoveCollision != null && !IsGround(hitinfo.normal))
                {
                    OnMoveCollision(hitinfo.point, hitinfo.normal, hitinfo.collider);
                }
                DoMoveSlide(slideVector, MAX_PHYSICS_SLIDE_ITERATIONS, stairLocations, false); // see DoMoveSlide comment
            }
        }
        else
        {
            if (stairLocations.Length > 0 && Vector3.Dot(newMoveVector.normalized, Vector3.up) < sinStairClimbAngle - 0.001f)// if we hit the stair climbing plane before we hit our target. If our move vector is steeper than the stair climbing angle, disregard.
            {
                //AttemptMove(transform.position + newMoveVector.normalized * distanceToStairPlanes);
                //Vector3 remainingVector = newMoveVector - newMoveVector.normalized * (hitinfo.distance);
                Vector3 stairSlideVector = GetStairClimbVector(moveVector);
                DoMoveSlide(stairSlideVector, MAX_PHYSICS_SLIDE_ITERATIONS, stairLocations, true); // see DoMoveSlide comment
            }
            else
                AttemptSetPosition(transform.position + newMoveVector);
        }
        if (isGrounded && stairLocations.Length == 0 && !jump && doKeepOnGroundMove)
        {
            DoKeepOnGroundMove();
        }
        if ((lastMoveVector.y < -STUCK_BETWEEN_SLOPES_MIN_Y) && transform.position.y > startY - STUCK_BETWEEN_SLOPES_TEST_VALUE)
        {
            isGrounded = true;
            stuckBetweenSlopes = true;
        }
    }

    // When we hit something, we need to continue our movement instead of just stopping, to prevent things like freezing when running into walls
    private void DoMoveSlide(Vector3 inVector, int iterations, Vector3[] stairLocations, bool stairClimbing)
    {
        if (iterations == 0)
        {
            //Debug.Log("DoMoveSlide out of iterations");
            return;
        }

        /*float distanceToStairPlanes = inVector.magnitude;
        foreach (Vector3 stairLocation in stairLocations)
        {
            float stairPlaneDistance = SweepToStairClimbPlane(inVector, stairLocation);
            if (stairPlaneDistance < distanceToStairPlanes)
                distanceToStairPlanes = stairPlaneDistance;
        }*/

        Vector3 capsuleCenter = transform.position;
        float pointDist = capsuleHeight / 2 - capsuleRadius;
        RaycastHit hitinfo;
        if (Physics.CapsuleCast(capsuleCenter - pointDist * Vector3.down, capsuleCenter - pointDist * Vector3.up, capsuleRadius,
        inVector.normalized, out hitinfo, inVector.magnitude + skinWidth, geometryCollisionLayers)) // Capsule Cast that checks our slide move, the same way as you would for a regular move
        {
            if (stairClimbing && Vector3.Dot(inVector.normalized, Vector3.up) < sinStairClimbAngle - 0.001f) // if we hit the stair climbing plane before we hit our target. If our move vector is steeper than the stair climbing angle, disregard.
            {
                //Debug.DrawRay(transform.position - (capsuleHeight / 2 - 0.01f) * Vector3.down, inVector.normalized * distanceToStairPlanes);

                //AttemptMove(transform.position + inVector.normalized * distanceToStairPlanes);
                //Vector3 remainingVector = inVector - inVector.normalized * (hitinfo.distance);
                Vector3 stairSlideVector = GetStairClimbVector(inVector);

                //Debug.DrawRay(transform.position - (capsuleHeight / 2 - 0.01f) * Vector3.down + inVector.normalized * distanceToStairPlanes, stairSlideVector);

                DoMoveSlide(stairSlideVector, iterations - 1, stairLocations, stairClimbing); // see DoMoveSlide comment
            }
            else
            {
                float newSkinWidth = skinWidth / Vector3.Dot(-inVector.normalized, hitinfo.normal);
                newSkinWidth = Mathf.Max(newSkinWidth, 0);
                AttemptSetPosition(transform.position + inVector.normalized * (hitinfo.distance - newSkinWidth));
                Vector3 remainingVector = inVector - inVector.normalized * (hitinfo.distance - newSkinWidth);
                Vector3 slideVector = Vector3.ProjectOnPlane(remainingVector, hitinfo.normal);
                if (isGrounded && !stairClimbing && slideVector.y > 0 && !IsGround(hitinfo.normal) && Vector3.Dot(hitinfo.normal, Vector3.up) > 0) // If we've hit a steep slope, stop horizontal momentum from turning into additional vertical momentum. Don't do this when climbing stairs.
                {
                    Vector3 crossVector = Vector3.Cross(hitinfo.normal, Vector3.up);
                    slideVector = Vector3.Project(slideVector, crossVector);
                }
                if (OnMoveCollision != null && !IsGround(hitinfo.normal))
                {
                    OnMoveCollision(hitinfo.point, hitinfo.normal, hitinfo.collider);
                }

                DoMoveSlide(slideVector, iterations - 1, stairLocations, stairClimbing); // Run this function recursively. It can run a total of MAX_PHYSICS_SLIDE_ITERATIONS times, but most of the time it won't run that many.
            }
        }
        else
        {
            if (stairClimbing && Vector3.Dot(inVector.normalized, Vector3.up) < sinStairClimbAngle - 0.001f) // if we hit the stair climbing plane before we hit our target. If our move vector is steeper than the stair climbing angle, disregard.
            {
                //AttemptMove(transform.position + inVector.normalized * distanceToStairPlanes);
                //Vector3 remainingVector = inVector - inVector.normalized * (hitinfo.distance);
                Vector3 stairSlideVector = GetStairClimbVector(inVector);
                DoMoveSlide(stairSlideVector, iterations - 1, stairLocations, stairClimbing); // see DoMoveSlide comment
            }
            AttemptSetPosition(transform.position + inVector);
        }
    }

    public bool IsGround(Vector3 normal)
    {
        return (Vector3.Dot(normal, Vector3.up) > cosSlopeLimit);
    }

    private void DoKeepOnGroundMove() // Used to drop the character's position when walking down a slope
    {
        Vector3 lastMoveVectorXZ = lastMoveVector;
        lastMoveVectorXZ.y = 0;
        float downwardsMovement = Mathf.Tan(slopeLimit * Mathf.Deg2Rad) * lastMoveVectorXZ.magnitude;

        Vector3 capsuleCenter = transform.position;
        float pointDist = capsuleHeight / 2 - capsuleRadius;
        RaycastHit[] raycasthits;
        raycasthits = Physics.CapsuleCastAll(capsuleCenter - pointDist * Vector3.down, capsuleCenter - pointDist * Vector3.up, capsuleRadius,
            Vector3.down, capsuleRadius + downwardsMovement + skinWidth, geometryCollisionLayers); // capsule cast, checking for ground. Casts an extra radius and ignores results within the lower hemisphere, in order to imitate a cylinder
        float minDistance = downwardsMovement;
        bool doDownwardsMovement = false;
        foreach (RaycastHit hit in raycasthits)
        {
            float distToPoint = (capsuleCenter.y - capsuleHeight / 2) - hit.point.y; //calculating the distance if we were casting a cylinder instead of a capsule
            if (distToPoint < minDistance)
            {
                minDistance = distToPoint;
                downwardsMovement = (minDistance - skinWidth);
                doDownwardsMovement = true;
            }
        }

        if (doDownwardsMovement)
        {
            downwardsMovement = Mathf.Max(downwardsMovement, 0); //Emergency edge case fixer in case for some reason we end up with upward movement
            DoMoveSlide(downwardsMovement * Vector3.down, 1, new Vector3[0], false);
        }
    }

    private Vector3[] StairClimbSweep(ref Vector3 movement, bool groundCheck)
    {
        List<Vector3> foundLocations = new List<Vector3>();
        for (int i = 0; i < numStairChecks; i++)
        {
            Vector3 stairLocation;
            if (StairClimbSweepPart(movement, groundCheck, i, capsuleRadius / numStairChecks, out stairLocation))
            {
                foundLocations.Add(stairLocation);
            }
        }
        return foundLocations.ToArray();
    }

    // Stair climbing sweep: a very thin box, facing diagonally up, is swept forward in front of our character, to allow for smooth stair climbing. To understand how this works, uncomment the DrawBoxCast calls.
    private bool StairClimbSweepPart(Vector3 movement, bool groundCheck, int checkIndex, float width, out Vector3 stairLocation)
    {
        stairLocation = Vector3.zero;
        Vector3 capsuleCenter = transform.position;
        if (Mathf.Abs(movement.x) < 0.001 && Mathf.Abs(movement.z) < 0.001) return false;
        Vector3 XZMovement = new Vector3(movement.x, 0, movement.z);
        Vector3 halfExtents = new Vector3(width, STAIR_SWEEPER_THICKNESS, maxStairHeight / sinStairClimbAngle / 2);
        float startDistance = maxStairHeight / tanStairClimbAngle / 2;
        Vector3 startPos = new Vector3(0, maxStairHeight / 2 + STAIR_SWEEPER_THICKNESS, 0);
        startPos += -XZMovement.normalized * startDistance;
        startPos += Vector3.Cross(XZMovement.normalized, Vector3.up) * (capsuleRadius * (1 - 1/numStairChecks - 1/numStairChecks * checkIndex * 2)); // offset our startpos depending on which of the multiple stairchecks it is
        startPos += capsuleCenter + (capsuleHeight / 2) * Vector3.down;
        Quaternion yawRotation = Quaternion.LookRotation(new Vector3(movement.x, 0, movement.z));
        Quaternion pitchRotation = Quaternion.Euler(-stairClimbAngle, 0, 0);
        RaycastHit hitinfo = new RaycastHit();
        Quaternion orientation = yawRotation * pitchRotation;
        float addedDistance = capsuleRadius * Mathf.Tan(stairClimbAngle * Mathf.Deg2Rad / 2);
        Vector3 castVector = XZMovement.normalized * (XZMovement.magnitude + addedDistance + startDistance * 2);
        bool didCollide = Physics.BoxCast(startPos, halfExtents, castVector.normalized, out hitinfo, orientation,
            castVector.magnitude, geometryCollisionLayers);
        if (didCollide)
        {
            bool doRecast = false;
            for (int i = 0; i < 20; i++)
            {
                if (didCollide && Vector3.Dot((hitinfo.point - transform.position).normalized, XZMovement.normalized) < 0) // Edge case; attempt to redo traces if we hit small objects
                {
                    PhysHelper.DrawBoxCast(halfExtents, startPos, startPos + hitinfo.distance * castVector.normalized, orientation, true);
                    
                    castVector = castVector.normalized * (castVector.magnitude - hitinfo.distance); //Reduce our castvector distance by the distance traveled before hitting something
                    startPos += (hitinfo.distance + 0.001f) * castVector.normalized; //Go past the small object and cast again
                    didCollide = Physics.BoxCast(startPos, halfExtents, castVector.normalized, out hitinfo, orientation,
                    castVector.magnitude, geometryCollisionLayers);
                    doRecast = true;
                }
                Vector3 endPos = startPos + hitinfo.distance * castVector.normalized;
                bool didHitBoxSide = Vector3.Project(hitinfo.point - endPos, Vector3.Cross(XZMovement.normalized, Vector3.up)).magnitude > halfExtents.x - 0.001f;
                bool didHitBoxTop = hitinfo.point.y > endPos.y + halfExtents.z * sinStairClimbAngle - STAIR_SWEEPER_THICKNESS;
                bool didHitBoxBottom = hitinfo.point.y < endPos.y - halfExtents.z * sinStairClimbAngle + STAIR_SWEEPER_THICKNESS;
                if (didCollide && Vector3.Dot(hitinfo.normal, Vector3.up) < cosSlopeLimit && didHitBoxSide && (didHitBoxBottom || didHitBoxTop)) // If we're walking diagonally into a wall
                {
                    PhysHelper.DrawBoxCast(halfExtents, startPos, startPos + hitinfo.distance * castVector.normalized, orientation, true);
                    
                    castVector = castVector.normalized * (castVector.magnitude - hitinfo.distance); //Reduce our castvector distance by the distance traveled before hitting something
                    startPos += (hitinfo.distance - skinWidth) * castVector.normalized;
                    Vector3 xzNormal = Vector3.ProjectOnPlane(hitinfo.normal, Vector3.up).normalized;
                    castVector = Vector3.ProjectOnPlane(castVector, xzNormal);
                    didCollide = Physics.BoxCast(startPos, halfExtents, castVector.normalized, out hitinfo, orientation,
                    castVector.magnitude, geometryCollisionLayers);
                    doRecast = true;
                }
                if (didCollide && Vector3.Dot(hitinfo.normal, Vector3.up) > cosSlopeLimit && didHitBoxBottom) // If we're walking up a shallow slope
                {
                    PhysHelper.DrawBoxCast(halfExtents, startPos, startPos + hitinfo.distance * castVector.normalized, orientation, true);

                    castVector = castVector.normalized * (castVector.magnitude - hitinfo.distance); //Reduce our castvector distance by the distance traveled before hitting something
                    startPos += (hitinfo.distance - skinWidth) * castVector.normalized;
                    castVector = Vector3.ProjectOnPlane(castVector, hitinfo.normal);
                    didCollide = Physics.BoxCast(startPos, halfExtents, castVector.normalized, out hitinfo, orientation,
                    castVector.magnitude, geometryCollisionLayers);
                    doRecast = true;
                }
                if (!doRecast)
                    break;
            }
            if (didCollide)
            {
                Vector3 endPos = startPos + hitinfo.distance * castVector.normalized;
                bool didHitBoxTop = hitinfo.point.y > endPos.y + halfExtents.z * sinStairClimbAngle - STAIR_SWEEPER_THICKNESS;
                bool didHitBoxBottom = hitinfo.point.y < endPos.y - halfExtents.z * sinStairClimbAngle + STAIR_SWEEPER_THICKNESS;
                if (didHitBoxTop || didHitBoxBottom) // It doesn't count as a stair if it hit the top or bottom of the collider
                {
                    PhysHelper.DrawBoxCast(halfExtents, startPos, startPos + hitinfo.distance * castVector.normalized, orientation, true);
                    return false;
                }
                if (Vector3.Dot(hitinfo.normal, Vector3.up) < cosSlopeLimit) // If we hit something that's too steep, raycast to check if it's a stair anyways
                {
                    Vector3 rayCastStart = endPos; // Our start position should be the center of the bottom of the box
                    rayCastStart.y -= halfExtents.z * sinStairClimbAngle;
                    rayCastStart -= halfExtents.z * XZMovement.normalized * cosStairClimbAngle;
                    Vector3 rayCastEnd = hitinfo.point + 0.001f * Vector3.up;
                    Vector3 rayCastDir = rayCastEnd - rayCastStart;
                    float rayCastDistance = (rayCastEnd - rayCastStart).magnitude + 0.001f;
                    if (Physics.Raycast(rayCastStart, rayCastDir.normalized, rayCastDistance, geometryCollisionLayers)) // Cast to see if we hit a sloped wall, if we do the cast should fail
                    {
                        PhysHelper.DrawBoxCast(halfExtents, startPos, startPos + hitinfo.distance * castVector.normalized, orientation, true);
                        return false;
                    }

                    rayCastStart = rayCastStart + rayCastDir.normalized * (rayCastDir.magnitude + 0.001f);

                    RaycastHit stairTopTestHitinfo;
                    if (Physics.Raycast(rayCastStart, Vector3.down, out stairTopTestHitinfo, maxStairHeight, geometryCollisionLayers)) // Cast straight down from a position that should be over the stair
                    {
                        if (Vector3.Dot(stairTopTestHitinfo.normal, Vector3.up) < cosSlopeLimit)
                        {
                            PhysHelper.DrawBoxCast(halfExtents, startPos, startPos + hitinfo.distance * castVector.normalized, orientation, true);
                            return false;
                        }
                    }
                }
                PhysHelper.DrawBoxCast(halfExtents, startPos, startPos + hitinfo.distance * castVector.normalized, orientation, true);
                stairLocation = hitinfo.point;
                return true;
            }
            else
            {
                PhysHelper.DrawBoxCast(halfExtents, startPos, startPos + castVector, orientation, false);
                return false;
            }
        }
        else
        {
            PhysHelper.DrawBoxCast(halfExtents, startPos, startPos + castVector, orientation, false);
            return false;
        }
    }

    // Does a sphere cast from the bottom of the capsule in the direction of moveVector, checking intersections with a plane sloping diagonally down from stairpoint at the angle of StairClimbAngle
    /*private float SweepToStairClimbPlane(Vector3 moveVector, Vector3 stairPoint)
    {
        Vector3 startingMoveVector = moveVector;
        Vector3 normal = -moveVector;
        Vector3 sphereStart = transform.position + (capsuleHeight / 2 - capsuleRadius) * Vector3.down;
        normal.y = 0;
        normal.y = normal.magnitude / tanStairClimbAngle;
        normal.Normalize();

        if (Mathf.Abs(Vector3.Dot(normal, -moveVector.normalized)) < 0.01f) // prevent divide by 0 float weirdness
        {
            return 0;
        }

        float sphereFromPlaneDistance = Vector3.Project(stairPoint - sphereStart, normal).magnitude; //distance from the plane to the center of the sphere
        float moveVectorRayIntersect = sphereFromPlaneDistance / Vector3.Dot(normal, -moveVector.normalized);
        moveVector = moveVector.normalized * moveVectorRayIntersect;
        Debug.DrawRay(sphereStart, moveVector);

        Debug.DrawRay(stairPoint, normal);
        float removedCastDistance = capsuleRadius / Vector3.Dot(normal, -moveVector.normalized);
        float castDistance = moveVector.magnitude - removedCastDistance;
        
        if (castDistance > startingMoveVector.magnitude + 0.01f)
        {
            Debug.Log("That should not have happened");
        }
        //Debug.DrawRay(sphereStart, moveVector.normalized * CastDistance);
        return castDistance;
    }*/

    private Vector3 GetStairClimbVector(Vector3 moveVector)
    {
        Vector3 newMoveVectorXZ = new Vector3(moveVector.x, 0, moveVector.z);
        Vector3 stairSlideVector = newMoveVectorXZ + Vector3.up * newMoveVectorXZ.magnitude * tanStairClimbAngle;
        stairSlideVector = stairSlideVector.normalized * newMoveVectorXZ.magnitude;
        return stairSlideVector;
    }

    private bool AttemptSetPosition(Vector3 position)
    {
        float pointDist = capsuleHeight / 2 - capsuleRadius;
        RaycastHit[] hitinfos = Physics.CapsuleCastAll(position + pointDist * Vector3.up, position + pointDist * Vector3.down, capsuleRadius, Vector3.down, 0.001f, geometryCollisionLayers);
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

    ///<summary>Casts in 26 directions to determine the best direction to go to escape the intersection. Don't make the distance less than the radius * 2!</summary>
    public bool ResolveIntersection(float distance)
    {
        bool resolvedIntersection = false;
        RaycastHit hitinfo;
        Collider[] colBuffer = new Collider[1];
        float pointDist = capsuleHeight / 2 - capsuleRadius;
        Vector3 point1 = transform.position + pointDist * Vector3.down;
        Vector3 point2 = transform.position + pointDist * Vector3.up;

        float minDistance = distance;
        Vector3 desiredPosition = transform.position;

        if (Physics.OverlapCapsuleNonAlloc(point1, point2, capsuleRadius, colBuffer, geometryCollisionLayers) == 0)
        {
            return true; // There was no intersection to resolve.
        }

        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                for (int k = -1; k <= 1; k++)
                {
                    if (i == 0 && j == 0 && k == 0) continue;
                    Vector3 direction = new Vector3(i, j, k);
                    direction.Normalize();
                    Vector3 castOrigin;
                    float castBackDistance;
                    if (Physics.CapsuleCast(point1, point2, capsuleRadius, direction, out hitinfo, distance + skinWidth, geometryCollisionLayers))
                    {
                        castOrigin = transform.position + (hitinfo.distance - skinWidth) * direction;
                        castBackDistance = hitinfo.distance;
                    }
                    else
                    {
                        castOrigin = transform.position + distance * direction;
                        castBackDistance = distance;
                    }
                    Vector3 newPoint1 = castOrigin + pointDist * Vector3.down;
                    Vector3 newPoint2 = castOrigin + pointDist * Vector3.up;
                    if (Physics.OverlapCapsuleNonAlloc(newPoint1, newPoint2, capsuleRadius, colBuffer, geometryCollisionLayers) > 0)
                    {
                        continue;
                    }
                    /*if (DebugPhysics.CapsuleCast(newPoint1, newPoint2, capsuleRadius, -direction, out hitinfo, castBackDistance + skinWidth, geometryCollisionLayers,
                        DebugPhysics.PreviewCondition.Editor, 1, Color.red, Color.green))*/
                    if (Physics.CapsuleCast(newPoint1, newPoint2, capsuleRadius, -direction, out hitinfo, castBackDistance + skinWidth, geometryCollisionLayers))
                    {
                        Vector3 endPosition = castOrigin - direction * (hitinfo.distance - skinWidth);
                        float endPosDistance = (this.transform.position - endPosition).magnitude;
                        if (endPosDistance < minDistance)
                        {
                            minDistance = endPosDistance;
                            desiredPosition = endPosition;
                            resolvedIntersection = true;
                        }
                    }
                }
            }
        }
        if (resolvedIntersection)
        {
            AttemptSetPosition(desiredPosition);
        }
        return resolvedIntersection;
    }

    public void ResolveMovingObjIntersection(Collider collider)
    {
        Vector3 resolveDirection;
        float resolveDistance;
        if (!Physics.ComputePenetration(capsuleCollider, transform.position, Quaternion.identity,
            collider, collider.transform.position, collider.transform.rotation, out resolveDirection, out resolveDistance))
        {
            return; //No penetration
        }
        if (!AttemptSetPosition(transform.position + (resolveDistance + skinWidth) * resolveDirection))
        {
            Debug.Log("failed to set position");
        }
    }

    private void SetGroundParent(MovingObject movingObject)
    {
        if (groundParent != null)
        {
            groundParent.OnPositionMoved -= MoveWithGroundParent;
        }
        groundParent = movingObject;
        groundParent.OnPositionMoved += MoveWithGroundParent;
    }

    private void ClearGroundParent()
    {
        if (!groundParent) return;
        groundParent.OnPositionMoved -= MoveWithGroundParent;
        groundParent = null;
        if (OnDisconnectFromGroundParent != null)
        {
            OnDisconnectFromGroundParent(lastGroundParentVelocity);
        }
    }

    private void MoveWithGroundParent(Vector3 velocity)
    {
        Vector3 newVelocity = velocity;
        Move(newVelocity, false, false);
        //AttemptSetPosition(transform.position + velocity);
        lastGroundParentVelocity = velocity / Time.fixedDeltaTime;
    }
}
