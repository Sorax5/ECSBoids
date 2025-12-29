using UnityEngine;

public class FreeFlyCamera : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float lookSensitivity = 2f;
    public float fastMultiplier = 4f;
    public float slowMultiplier = 0.25f;

    private float rotationX;
    private float rotationY;

    void Update()
    {
        // =============== ROTATION ================
        if (Input.GetMouseButton(1)) // clic droit
        {
            rotationX += Input.GetAxis("Mouse X") * lookSensitivity;
            rotationY -= Input.GetAxis("Mouse Y") * lookSensitivity;
            rotationY = Mathf.Clamp(rotationY, -89f, 89f);

            transform.rotation = Quaternion.Euler(rotationY, rotationX, 0);
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }

        // =============== DEPLACEMENT ================
        float speed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift)) speed *= fastMultiplier;
        if (Input.GetKey(KeyCode.LeftControl)) speed *= slowMultiplier;

        Vector3 movement = Vector3.zero;

        if (Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.W)) movement += transform.forward;
        if (Input.GetKey(KeyCode.S)) movement -= transform.forward;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.A)) movement -= transform.right;
        if (Input.GetKey(KeyCode.D)) movement += transform.right;

        transform.position += speed * Time.deltaTime * movement;

        // =============== AJUSTEMENT DE LA VITESSE VIA MOLETTE ================
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0)
        {
            moveSpeed *= scroll > 0 ? 1.1f : 0.9f;
            moveSpeed = Mathf.Clamp(moveSpeed, 1f, 200f);
        }
    }
}