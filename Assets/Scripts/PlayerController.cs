using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    private InputActions inp;
    private Rigidbody rb;

    [SerializeField] private float accelerationFactor = 2;
    [SerializeField] private float maxVelocity = 5;
    [Tooltip("When a force is applied in the opposite direction to velocity, that force is multiplied by the braking multiplier")]
    [SerializeField] private float brakingMultiplier = 4;

    [SerializeField] private Camera thirdPersonCamera;
    [SerializeField] private float camDistance = 5f;
    [SerializeField] private Vector3 camOffset = new Vector3(0, 1, 0);
    [SerializeField] private float maxVerticalLook = 60f;
    [SerializeField] private float minVerticalLook = 60f;
    [SerializeField] private bool invertLook = false;
    private Vector3 lookAngles;
    private GameObject camAnchor;

    void Awake() {
        inp = new();
        rb = GetComponent<Rigidbody>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        camAnchor = new();
        thirdPersonCamera.transform.parent = camAnchor.transform;
        thirdPersonCamera.transform.position = Vector3.forward * -camDistance;
        lookAngles = new();
    }

    void OnEnable() {
        inp.Enable();
    }

    void OnDisable() {
        inp.Disable();
    }

    void Start() {
        // Cap linear velocity
        rb.maxLinearVelocity = maxVelocity;
    }

    void Update() {
        // Get look delta
        Vector2 lookDelta = inp.Player.Look.ReadValue<Vector2>();
        lookAngles += new Vector3(lookDelta.y, (invertLook ? -1 : 1) * lookDelta.x, 0);
        lookAngles.x = Mathf.Clamp(lookAngles.x, minVerticalLook, maxVerticalLook);
        Quaternion lookRot = Quaternion.Euler(lookAngles);

        // Get movement direction and create the movement vector
        Vector2 dir = inp.Player.Move.ReadValue<Vector2>();
        dir *= accelerationFactor * Time.deltaTime;
        Vector3 movement = Quaternion.Euler(
            0,
            lookAngles.y,
            0
        ) * new Vector3(
            dir.x,
            0,
            dir.y
        );

        // If the player has linear velocity
        if (rb.linearVelocity.magnitude != 0) {
            // Separate movement vector into components parallel and perpendicular to velocity vector
            Vector3 perpendicularVector = Vector3.Cross(Vector3.up, rb.linearVelocity);
            float[] movementComponents = new float[] {
                Vector3.Dot(movement, perpendicularVector) / perpendicularVector.magnitude,
                Vector3.Dot(movement, rb.linearVelocity) / rb.linearVelocity.magnitude
            };

            // Apply the braking multiplier to any component of movement which is in the opposing direction to its respoective component of velocity
            if (movementComponents[0] < 0)
                movementComponents[0] *= brakingMultiplier;
            if (movementComponents[1] < 0)
                movementComponents[1] *= brakingMultiplier;

            // Add components to get the final movement vector
            movement = movementComponents[0] * perpendicularVector.normalized + movementComponents[1] * rb.linearVelocity.normalized;
        }

        // Apply force
        rb.AddForce(movement, ForceMode.Impulse);

        // Set camAnchor position and rotate camera to face player
        camAnchor.transform.SetPositionAndRotation(
            transform.position + camOffset,
            lookRot
        );
    }
}
