using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCharacter : MonoBehaviour
{

    private const int MAX_READJUSTMENTS = 1;

    public DollyCamera dollyCamera;
    public BetterCharacterController controller;
    public Animator animator;

    public float gravity = 10;
    public float terminalVelocity = 4;
    public float moveAcceleration = 1;
    public float moveAccelerationAir = 0.3f;
    public float wallCollisionDeceleration = 2;
    public float moveSpeed = 0.2f;
    public float superSprintSpeed = 0.4f;
    public float superSprintAcceleration = 0.2f;
    public float timeBeforeSuperSprint = 1;
    public float rotateSpeed = 1080;
    public float jumpForce = 0.4f;
    public float minJumpButtonTime = 0.2f;
    [Tooltip("Extra gravity when we reduce the jump button")]
    public float jumpHeightReduceForce = 10;
    [Tooltip("When we hit a flat enough ceiling, reduce the y velocity to this to improve game feel")]
    public float reducedCeilingVelocity = 0.1f;
    public float moveDeadZone = 0.1f;
    public float maxCeilingAngle = 45;
    [Tooltip("Time not on ground before we start falling animation")]
    public float timeBeforeFallingAnim = 0.1f;

    private Vector3 v3TargetVelocity;
    private Vector3 velocity;
    private float maxCeilingAngleCos;
    private bool inputButtonDownJump;
    private float inAirTimer;
    private bool didJump;
    private float currentMaxSpeed;
    private float superSprintTimer;
    private readonly List<Vector3> collidedWallNormals = new List<Vector3>();
    private bool isJumpingUp;
    private float minJumpTimer;

    // Use this for initialization
    void Start()
    {
        velocity = Vector3.zero;
        controller.OnMoveCollision += AdjustVelocityOnCeilingCollision;
        controller.OnMoveCollision += AdjustVelocityOnWallCollision;
        maxCeilingAngleCos = Mathf.Cos(maxCeilingAngle);
        superSprintTimer = 0;
    }

    // Update is called once per frame
    void Update()
    {
        if (v3TargetVelocity.magnitude > 0.02f && controller.isGrounded)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(v3TargetVelocity, Vector3.up), Time.deltaTime * rotateSpeed);
        }
        if (Input.GetButtonDown("Fire2")) inputButtonDownJump = true;
    }

    private void FixedUpdate()
    {
        if (controller.isGrounded)
        {
            Debug.DrawRay(this.transform.position, Vector3.up);
        }
        velocity.y -= gravity * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -terminalVelocity);
        if (controller.isGrounded) velocity.y = Mathf.Max(velocity.y, -0.01f);

        // Controller input
        float fInputRawX;
        float fInputRawY;
        fInputRawX = Input.GetAxis("Horizontal");
        fInputRawY = Input.GetAxis("Vertical");
        if (new Vector2(fInputRawX, fInputRawY).magnitude < moveDeadZone)
        {
            fInputRawX = 0;
            fInputRawY = 0;
        }

        float fYawRadians = -dollyCamera.GetYaw() * Mathf.Deg2Rad;
        float fInputX = Mathf.Cos(fYawRadians) * fInputRawX - Mathf.Sin(fYawRadians) * fInputRawY; // translate inputs to cameraspace
        float fInputY = Mathf.Sin(fYawRadians) * fInputRawX + Mathf.Cos(fYawRadians) * fInputRawY; // translate inputs to cameraspace
        v3TargetVelocity = Vector3.zero;
        v3TargetVelocity.x = fInputX * currentMaxSpeed;
        v3TargetVelocity.z = fInputY * currentMaxSpeed;

        // Jumping code
        minJumpTimer = Mathf.Min(minJumpTimer + Time.fixedDeltaTime, minJumpButtonTime);
        if (velocity.y <= 0 || (controller.isGrounded || !Input.GetButton("Fire2")) && minJumpTimer >= minJumpButtonTime)
        {
            isJumpingUp = false;
        }
        if (!isJumpingUp && velocity.y > 0 && !controller.isGrounded)
        {
            velocity.y -= jumpHeightReduceForce * Time.fixedDeltaTime;
        }

        // Movement code
        Vector3 v3XYVelocity = velocity;
        v3XYVelocity.y = 0;

        float animMoveSpeed = v3XYVelocity.magnitude / moveSpeed;
        if (animMoveSpeed <= 0.05f) animMoveSpeed = 0;

        animator.SetFloat("fSpeed", animMoveSpeed);
        if (animMoveSpeed < 1)
            animator.SetFloat("fMoveAnimSpeedMult", Mathf.Pow(animMoveSpeed, 0.3f) * 1.5f);
        else
            animator.SetFloat("fMoveAnimSpeedMult", animMoveSpeed * 1.5f);

        Vector3 v3VelocityOffset;
        if (controller.isGrounded)
            v3VelocityOffset = Vector3.ClampMagnitude(v3TargetVelocity - v3XYVelocity, moveAcceleration * Time.fixedDeltaTime);
        else
            v3VelocityOffset = Vector3.ClampMagnitude(v3TargetVelocity - v3XYVelocity, moveAccelerationAir * Time.fixedDeltaTime);
        v3XYVelocity += v3VelocityOffset;

        if (v3XYVelocity.magnitude >= moveSpeed * 0.95f)
        {
            if (controller.isGrounded)
            {
                if (superSprintTimer < timeBeforeSuperSprint)
                    superSprintTimer += Time.fixedDeltaTime;
                else
                    currentMaxSpeed = Mathf.Min(superSprintSpeed, currentMaxSpeed + superSprintAcceleration * Time.fixedDeltaTime);
            }
            else
                superSprintTimer = 0;

        }
        else
        {
            currentMaxSpeed = moveSpeed;
            superSprintTimer = 0;
        }

        velocity.x = v3XYVelocity.x;
        velocity.z = v3XYVelocity.z;

        controller.Move(velocity, didJump);

        ApplyCollidedWallNormals();

        // Animator code
        didJump = false;

        if (controller.isGrounded)
        {
            animator.SetBool("bIsInAir", false);
            animator.SetBool("bIsLanding", true);
            inAirTimer = timeBeforeFallingAnim;
        }
        else
        {
            if (inAirTimer <= 0)
            {
                animator.SetBool("bIsInAir", true);
            }
            else
            {
                inAirTimer -= Time.fixedDeltaTime;
            }
            animator.SetBool("bIsLanding", false);
        }
        if (inputButtonDownJump && controller.isGrounded)
        {
            velocity.y = jumpForce;
            animator.SetTrigger("bJump");
            animator.SetBool("bIsLanding", false);
            didJump = true;
            isJumpingUp = true;
            minJumpTimer = 0;
        }
        inputButtonDownJump = false;
    }

    private void AdjustVelocityOnCeilingCollision(Vector3 impactPoint, Vector3 normal, Collider collider)
    {
        if (Vector3.Dot(normal, Vector3.down) > maxCeilingAngleCos && velocity.y > 0)
        {
            velocity.y = Mathf.Min(velocity.y, reducedCeilingVelocity);
        }
    }
    private void AdjustVelocityOnWallCollision(Vector3 impactPoint, Vector3 normal, Collider collider)
    {
        Vector3 XZNormal = normal;
        XZNormal.y = 0;
        XZNormal.Normalize();
        collidedWallNormals.Add(XZNormal);
    }

    private void ApplyCollidedWallNormals()
    {
        Vector3 XZVelocity = velocity;
        XZVelocity.y = 0;
        foreach (Vector3 XZNormal in collidedWallNormals)
        {
            Vector3 XZTargetVelocity = Vector3.ProjectOnPlane(XZVelocity, XZNormal);
            Vector3 XZVelocityOffset;
            XZVelocityOffset = Vector3.ClampMagnitude(XZTargetVelocity - XZVelocity, wallCollisionDeceleration * Time.fixedDeltaTime / collidedWallNormals.Count);
            XZVelocity += XZVelocityOffset;
        }

        collidedWallNormals.Clear();

        velocity = new Vector3(XZVelocity.x, velocity.y, XZVelocity.z);
    }
}