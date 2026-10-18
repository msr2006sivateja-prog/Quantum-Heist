
using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
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
    private AudioListener audioListener;

    private float cameraPitch;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new PlayerInputActions();

        if (cameraPivot != null)
            audioListener = cameraPivot.GetComponentInChildren<AudioListener>(true);
    }

    public override void OnNetworkSpawn()
    {
        bool isLocalPlayer = IsOwner;

        if (inputActions != null)
        {
            if (isLocalPlayer)
                inputActions.Enable();
            else
                inputActions.Disable();
        }

        if (audioListener != null)
            audioListener.enabled = isLocalPlayer;

        Camera playerCamera = cameraPivot != null
            ? cameraPivot.GetComponentInChildren<Camera>(true)
            : null;

        if (playerCamera != null)
            playerCamera.enabled = isLocalPlayer;
    }

    public override void OnNetworkDespawn()
    {
        if (inputActions != null)
            inputActions.Disable();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        Move();
        Look();
        HandleShooting();
        HandleReload();
    }

    private void HandleShooting()
    {
        if (weapon != null && inputActions.Player.Shoot.IsPressed())
            weapon.TryShoot();
    }

    private void HandleReload()
    {
        if (inputActions.Player.Reload.WasPressedThisFrame())
        {
            if (weapon != null)
                weapon.Reload();
        }
    }

    private void Move()
    {
        Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();

        Vector3 direction = new Vector3(input.x, 0f, input.y);
        direction = transform.TransformDirection(direction);

        if (controller != null && controller.enabled)
            controller.Move(direction * moveSpeed * Time.deltaTime);
    }

    private void Look()
    {
        if (cameraPivot == null)
            return;

        Vector2 input = inputActions.Player.Look.ReadValue<Vector2>();

        float mouseX = input.x * lookSensitivity;
        float mouseY = input.y * lookSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -maxLookAngle, maxLookAngle);

        cameraPivot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }
}
