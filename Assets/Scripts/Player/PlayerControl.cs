
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

    [Header("Aim Camera")]
    [SerializeField] private Transform playerCameraTransform;
    [SerializeField] private Vector3 aimCameraLocalPosition =
        new Vector3(0.6f, 0f, -1.5f);
    [SerializeField] private float aimTransitionSpeed = 10f;

    [Header("Animation")]
    [SerializeField] private Animator playerAnimator;

    private CharacterController controller;
    private PlayerInputActions inputActions;
    private AudioListener audioListener;

    private float cameraPitch;
    private Vector3 normalCameraLocalPosition;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new PlayerInputActions();

        // Find the Animator on the soldier child if not assigned.
        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>(true);

        if (cameraPivot != null)
            audioListener =
                cameraPivot.GetComponentInChildren<AudioListener>(true);

        // Find the existing camera and save its starting position.
        if (cameraPivot != null)
        {
            Camera cam =
                cameraPivot.GetComponentInChildren<Camera>(true);

            if (cam != null)
            {
                playerCameraTransform = cam.transform;

                normalCameraLocalPosition =
                    playerCameraTransform.localPosition;
            }
        }
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

        // Keep the existing second-dimension spawn behavior.
        if (IsOwner && OwnerClientId != NetworkManager.ServerClientId)
        {
            transform.position = new Vector3(1000f, 0f, 0f);
        }
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
        HandleAiming();
        HandleShooting();
        HandleReload();
    }

    private void HandleAiming()
    {
        if (inputActions == null)
            return;

        // Hold the assigned Aim button to aim.
        bool isAiming = inputActions.Player.Aim.IsPressed();

        // Update the soldier's aiming animation.
        if (playerAnimator != null)
            playerAnimator.SetBool("Aiming", isAiming);

        // Move the camera smoothly between normal and aiming positions.
        if (playerCameraTransform != null)
        {
            Vector3 targetPosition = isAiming
                ? aimCameraLocalPosition
                : normalCameraLocalPosition;

            playerCameraTransform.localPosition = Vector3.Lerp(
                playerCameraTransform.localPosition,
                targetPosition,
                aimTransitionSpeed * Time.deltaTime
            );
        }
    }

    private void HandleShooting()
    {
        if (weapon != null && inputActions.Player.Shoot.IsPressed())
        {
            weapon.TryShoot();
        }
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
        Vector2 input =
            inputActions.Player.Move.ReadValue<Vector2>();

        // Update the soldier's movement animation.
        // Blend Tree thresholds: Idle = 0, Walk = 0.25,
        // Run = 1, Sprint = 2.
        if (playerAnimator != null)
        {
            float animationSpeed =
                input.magnitude > 0.1f ? 1f : 0f;

            playerAnimator.SetFloat("Speed", animationSpeed);
        }

        Vector3 direction = new Vector3(input.x, 0f, input.y);
        direction = transform.TransformDirection(direction);

        if (controller != null && controller.enabled)
        {
            controller.Move(
                direction * moveSpeed * Time.deltaTime
            );
        }
    }

    private void Look()
    {
        if (cameraPivot == null || inputActions == null)
            return;

        Vector2 input =
            inputActions.Player.Look.ReadValue<Vector2>();

        float mouseX = input.x * lookSensitivity;
        float mouseY = input.y * lookSensitivity;

        // Rotate the player horizontally.
        transform.Rotate(Vector3.up * mouseX);

        // Rotate the camera pivot vertically.
        cameraPitch -= mouseY;

        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxLookAngle,
            maxLookAngle
        );

        cameraPivot.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }
}
