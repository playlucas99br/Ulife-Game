using UnityEngine;

namespace FaseLucasGame
{
    /// <summary>
    /// Self contained first person controller (movement + look) used in FaseLucas.
    /// Movement and look are suspended while the magnet programming interface (TAB) is open.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerFPS : MonoBehaviour
    {
        [Header("References")]
        public Transform cameraPivot;

        [Header("Movement")]
        public float walkSpeed = 5f;
        public float runSpeed = 9f;
        public float jumpHeight = 1.6f;
        public float gravity = -20f;

        [Header("Look")]
        public float mouseSensitivity = 2f;
        public float minPitch = -85f;
        public float maxPitch = 85f;

        CharacterController controller;
        float pitch;
        float verticalVelocity;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            LockCursor(true);
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            bool uiOpen = MagnetProgramUI.IsOpen;

            if (!uiOpen)
            {
                HandleLook();
                HandleMovement();
            }
            else
            {
                // Keep applying gravity so the player doesn't float, but no input.
                verticalVelocity += gravity * Time.deltaTime;
                controller.Move(new Vector3(0f, verticalVelocity, 0f) * Time.deltaTime);
                if (controller.isGrounded) verticalVelocity = -2f;
            }
        }

        void HandleLook()
        {
            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up * mouseX);

            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            if (cameraPivot != null)
                cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }

        void HandleMovement()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 move = (transform.right * h + transform.forward * v).normalized;
            float speed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;

            if (controller.isGrounded)
            {
                verticalVelocity = -2f;
                if (Input.GetKeyDown(KeyCode.Space))
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = move * speed + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}
