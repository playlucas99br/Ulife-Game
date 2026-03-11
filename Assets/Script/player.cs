using UnityEngine;

public class Player : MonoBehaviour
{
    CharacterController controller;

    Vector3 forward;
    Vector3 strafe;
    Vector3 vertical;

    float forwardSpeed = 5f;
    float strafeSpeed = 5f;

    //Fisica da gravidade
    float forceJump = 2;
    float forceOfGravity = -2;
    float gravity;
    float jumpSpeed;
    float maxJumpHeight = 2f;
    float timeToMaxHeight = 0.5f;

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

        // Force = input * speed * direction
        forward = forwardInput * forwardSpeed * transform.forward;
        strafe = strafeInput * strafeSpeed * transform.right;

        vertical += gravity * Time.deltaTime * Vector3.up;

        if(controller.isGrounded)
        {
            vertical = Vector3.down;
        }

        if(Input.GetKeyDown(KeyCode.Space) && controller.isGrounded)
        {
            vertical = jumpSpeed * Vector3.up;
        }

        if (vertical.y > 0 && (controller.collisionFlags & CollisionFlags.Above) != 0) {
            vertical = Vector3.zero;
        }


        Vector3 finalSpeed = forward + strafe + vertical;

        controller.Move(finalSpeed * Time.deltaTime);
    }
}