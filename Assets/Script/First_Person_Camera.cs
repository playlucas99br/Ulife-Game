using UnityEngine;

public class First_Person_Camera : MonoBehaviour
{
    public Transform characterBody;
    public Transform characterHead;

    float rotationX = 0;
    float rotationY = 0;

    float angleMin = -90;
    float angleMax = 90;

    float sensitibityX = 0.5f;
    float sensitibityY = 0.5f;

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void LateUpdate()
    {
        // Trava de segurança: Só tenta seguir a cabeça se a cabeça existir
        if (characterHead != null)
        {
            transform.position = characterHead.position;
        }
    }

    void Update()
    {
        float verticalDeltar = Input.GetAxisRaw("Mouse Y") * sensitibityY;
        float HorizontalDeltar = Input.GetAxisRaw("Mouse X") * sensitibityX;

        rotationX += HorizontalDeltar;
        rotationY += verticalDeltar;

        rotationY = Mathf.Clamp(rotationY, angleMin, angleMax);

        characterBody.localEulerAngles = new Vector3(0, rotationX, 0);
        transform.localEulerAngles = new Vector3(-rotationY, rotationX, 0);
    }
}