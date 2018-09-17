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
    public float moveAcceleration = 5;
    public float moveSpeed = 0.2f;
    public float rotateSpeed = 1080;
    public float jumpForce = 0.4f;
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

    // Use this for initialization
    void Start()
    {
        velocity = Vector3.zero;
        controller.OnMoveCollision += AdjustVelocityOnCeilingCollision;
        maxCeilingAngleCos = Mathf.Cos(maxCeilingAngle);
    }

    // Update is called once per frame
    void Update()
    {
        if (v3TargetVelocity.magnitude > 0.02f)
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
        if (controller.isGrounded) velocity.y = Mathf.Max(velocity.y, -0.01f);
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
        v3TargetVelocity.x = fInputX * moveSpeed;
        v3TargetVelocity.z = fInputY * moveSpeed;

        Vector3 v3XYVelocity = velocity;
        v3XYVelocity.y = 0;
        Vector3 v3VelocityOffset = Vector3.ClampMagnitude(v3TargetVelocity - v3XYVelocity, moveAcceleration * Time.fixedDeltaTime);
        v3XYVelocity += v3VelocityOffset;

        animator.SetFloat("fSpeed", v3XYVelocity.magnitude / moveSpeed);

        velocity.x = v3XYVelocity.x;
        velocity.z = v3XYVelocity.z;

        controller.Move(velocity, didJump);
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
}