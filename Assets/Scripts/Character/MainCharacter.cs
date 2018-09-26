using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCharacter : MonoBehaviour
{

    private const int MAX_READJUSTMENTS = 1;

    public DollyCamera dollyCamera;
    public BetterCharacterController controller;
    public Animator animator;
    public BallCharacterController ballController;

    public float gravity = 60;
    public float terminalVelocity = 240;
    public float moveAcceleration = 60;
    public float moveAccelerationAir = 20f;
    public float wallCollisionDeceleration = 240;
    public float moveSpeed = 12f;
    public float superSprintSpeed = 23f;
    public float superSprintAcceleration = 12;
    public float MaxSpeedDecceleration = 1;
    public float timeBeforeSuperSprint = 1;
    public float rotateSpeed = 1080;
    public float jumpForce = 24;
    public float minJumpButtonTime = 0.2f;
    [Tooltip("Extra gravity when we reduce the jump button")]
    public float jumpHeightReduceForce = 24;
    [Tooltip("When we hit a flat enough ceiling, reduce the y velocity to this to improve game feel")]
    public float reducedCeilingVelocity = 6;
    public float moveDeadZone = 0.1f;
    public float maxCeilingAngle = 45;
    [Tooltip("Time not on ground before we start falling animation")]
    public float timeBeforeFallingAnim = 0.1f;
    public float jumpForgivenessTime = 0.2f;

    private Vector3 v3TargetVelocity;
    private Vector3 velocity;
    private float maxCeilingAngleCos;
    private bool inputButtonDownJump;
    private bool inputButtonDownCurlUp;
    private bool inputButtonUpCurlUp;
    private float inAirTimer;
    private bool didJump;
    private float currentMaxSpeed;
    private float superSprintTimer;
    private readonly List<Vector3> collidedWallNormals = new List<Vector3>();
    private bool isJumpingUp;
    private float minJumpTimer;
    private bool isCurledUp;
    private float jumpForgivenessTimer;

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
        if (v3TargetVelocity.magnitude > 0.02f && controller.isGrounded && !isCurledUp)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(v3TargetVelocity, Vector3.up), Time.deltaTime * rotateSpeed);
        }
        if (Input.GetButtonDown("Fire2")) inputButtonDownJump = true;
        if (Input.GetButtonDown("CurlUp")) inputButtonDownCurlUp = true;
        if (Input.GetButtonUp("CurlUp")) inputButtonUpCurlUp = true;
    }

    private void FixedUpdate()
    {
        ApplyGravity();
        GetInputLocalToCamera();
        if (!isCurledUp)
        {
            DoJumpingCode();
            DoNormalMovementCode();
        }
        DoAnimatorCode();
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

    private void ApplyGravity()
    {
        velocity.y -= gravity * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -terminalVelocity);
        if (controller.isGrounded) velocity.y = Mathf.Max(velocity.y, -0.01f);
    }

    private void GetInputLocalToCamera()
    {
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
        v3TargetVelocity.x = fInputX;
        v3TargetVelocity.z = fInputY;
        v3TargetVelocity.Normalize();
        v3TargetVelocity *= currentMaxSpeed;
    }

    private void DoJumpingCode()
    {
        minJumpTimer = Mathf.Min(minJumpTimer + Time.fixedDeltaTime, minJumpButtonTime);
        if (velocity.y <= 0 || (controller.isGrounded || !Input.GetButton("Fire2")) && minJumpTimer >= minJumpButtonTime)
        {
            isJumpingUp = false;
        }
        if (!isJumpingUp && velocity.y > 0 && !controller.isGrounded)
        {
            velocity.y -= jumpHeightReduceForce * Time.fixedDeltaTime;
        }
    }

    private void DoNormalMovementCode()
    {
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
        Vector3 newXYVelocity = v3XYVelocity + v3VelocityOffset;


        if (currentMaxSpeed > superSprintSpeed)
        {
            superSprintTimer = timeBeforeSuperSprint;
            currentMaxSpeed = Mathf.Max(superSprintSpeed, currentMaxSpeed - MaxSpeedDecceleration * Time.fixedDeltaTime);
            currentMaxSpeed = Mathf.Min(currentMaxSpeed, v3XYVelocity.magnitude);
        }
        else if (newXYVelocity.magnitude >= moveSpeed * 0.95f)
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

        velocity.x = newXYVelocity.x;
        velocity.z = newXYVelocity.z;

        controller.Move(velocity * Time.fixedDeltaTime, didJump);

        ApplyCollidedWallNormals();
    }

    private void DoAnimatorCode()
    {
        didJump = false;

        if (controller.isGrounded)
        {
            animator.SetBool("bIsInAir", false);
            animator.SetBool("bIsLanding", true);
            inAirTimer = timeBeforeFallingAnim;
            jumpForgivenessTimer = jumpForgivenessTime;
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
            if (jumpForgivenessTimer > 0)
            {
                jumpForgivenessTimer -= Time.fixedDeltaTime;
            }
            animator.SetBool("bIsLanding", false);
        }
        if (inputButtonDownJump && (controller.isGrounded || jumpForgivenessTimer > 0))
        {
            velocity.y = jumpForce;
            animator.SetTrigger("tJump");
            animator.SetBool("bIsLanding", false);
            didJump = true;
            isJumpingUp = true;
            minJumpTimer = 0;
        }
        if (inputButtonDownCurlUp)
        {
            isCurledUp = true;
            ballController.ActivateBallMode(velocity);
            animator.SetBool("bCurlUp", true);
            animator.SetTrigger("tCurlUp");
        }
        if (inputButtonUpCurlUp)
        {
            isCurledUp = false;
            velocity = ballController.DeactivateBallMode();
            currentMaxSpeed = Mathf.Max(currentMaxSpeed, new Vector3(velocity.x, 0, velocity.z).magnitude);
            animator.SetBool("bCurlUp", false);
        }
        inputButtonDownJump = false;
        inputButtonDownCurlUp = false;
        inputButtonUpCurlUp = false;
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