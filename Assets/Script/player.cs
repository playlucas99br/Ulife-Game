using UnityEngine;

public class Player : MonoBehaviour
{
    CharacterController controller;

    Vector3 forward;
    Vector3 strafe;
    Vector3 vertical;

    float forwardSpeed = 5f;
    float strafeSpeed = 5f;

    // Física da gravidade
    float forceJump = 2f;
    float forceOfGravity = -2f;
    float gravity;
    float jumpSpeed;
    float maxJumpHeight = 2f;
    float timeToMaxHeight = 0.5f;

    float runMultiplier = 2f;
    float crouchSpeedMultiplier = 0.5f;
    float crouchSmoothSpeed = 8f;

    float normalHeight = 1.8f;
    float crouchHeight = 1.0f;

    bool isCrouching;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        gravity = (forceOfGravity * maxJumpHeight) / (timeToMaxHeight * timeToMaxHeight);
        jumpSpeed = (forceJump * maxJumpHeight) / timeToMaxHeight;
    }

    void Update()
    {
        float forwardInput = Input.GetAxisRaw("Vertical");
        float strafeInput = Input.GetAxisRaw("Horizontal");

        ApplyGravity();
        HandleJump();
        HandleCrouchInput();
        SmoothCrouch();
        HandleMovement(forwardInput, strafeInput);
    }

    void ApplyGravity()
    {
        if (controller.isGrounded && vertical.y < 0f)
        {
            vertical = Vector3.down;
        }
        else
        {
            vertical += gravity * Time.deltaTime * Vector3.up;
        }

        if (vertical.y > 0f && (controller.collisionFlags & CollisionFlags.Above) != 0)
        {
            vertical = Vector3.zero;
        }
    }

    void HandleJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded && !isCrouching)
        {
            vertical = jumpSpeed * Vector3.up;
        }
    }

    void HandleCrouchInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            isCrouching = true;
        }

        if (Input.GetKeyUp(KeyCode.LeftControl))
        {
            isCrouching = false;
        }
    }

    void SmoothCrouch()
    {
        float targetHeight = isCrouching ? crouchHeight : normalHeight;

        controller.height = Mathf.Lerp(
            controller.height,
            targetHeight,
            crouchSmoothSpeed * Time.deltaTime
        );
    }

    void HandleMovement(float forwardInput, float strafeInput)
    {
        float currentForwardSpeed = forwardSpeed;
        float currentStrafeSpeed = strafeSpeed;

        if (Input.GetKey(KeyCode.LeftShift) && !isCrouching)
        {
            currentForwardSpeed *= runMultiplier;
            currentStrafeSpeed *= runMultiplier;
        }

        if (isCrouching)
        {
            currentForwardSpeed *= crouchSpeedMultiplier;
            currentStrafeSpeed *= crouchSpeedMultiplier;
        }

        forward = forwardInput * currentForwardSpeed * transform.forward;
        strafe = strafeInput * currentStrafeSpeed * transform.right;

        Vector3 finalSpeed = forward + strafe + vertical;
        controller.Move(finalSpeed * Time.deltaTime);
    }
}