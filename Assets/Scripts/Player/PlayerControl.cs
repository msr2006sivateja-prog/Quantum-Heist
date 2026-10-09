using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Look")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float maxLookAngle = 85f;

    [Header("Combat")]
    [SerializeField] private Weapon weapon;

    private CharacterController controller;
    private PlayerInputActions inputActions;

    private float cameraPitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void Update()
    {
        Move();
        Look();
        HandleShooting();
        HandleReload();
    }

    private void HandleShooting()
    {
        if (inputActions.Player.Shoot.IsPressed())
        {
            weapon.TryShoot();
        }
    }

    private void HandleReload()
    {
        if (inputActions.Player.Reload.WasPressedThisFrame())
        {
            weapon.Reload();
        }
    }

    private void Move()
    {
        Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();

        Vector3 direction = new Vector3(input.x, 0f, input.y);

        direction = transform.TransformDirection(direction);

        controller.Move(direction * moveSpeed * Time.deltaTime);
    }

    private void Look()
    {
        Vector2 input = inputActions.Player.Look.ReadValue<Vector2>();

        float mouseX = input.x * lookSensitivity;
        float mouseY = input.y * lookSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);

        cameraPivot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }
}