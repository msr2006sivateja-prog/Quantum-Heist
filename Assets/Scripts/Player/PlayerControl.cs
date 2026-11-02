
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Crouch")]
    [SerializeField] private float crouchMoveSpeed = 2.5f;
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float crouchCameraOffset = 0.5f;

    private readonly NetworkVariable<bool> isCrouching =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private float standingHeight;
    private Vector3 standingCenter;
    private Vector3 standingCameraPosition;

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

    private readonly NetworkVariable<bool> gameplayPaused =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private CharacterController controller;
    private PlayerInputActions inputActions;
    private AudioListener audioListener;

    private float cameraPitch;
    private Vector3 normalCameraLocalPosition;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private float accumulatedMouseX;

    private const float InputSendInterval = 1f / 30f;
    private float inputSendTimer;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new PlayerInputActions();

        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>(true);

        if (controller != null)
        {
            standingHeight = controller.height;
            standingCenter = controller.center;
        }

        if (cameraPivot != null)
        {
            standingCameraPosition = cameraPivot.localPosition;

            audioListener =
                cameraPivot.GetComponentInChildren<AudioListener>(true);

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
        isCrouching.OnValueChanged += OnCrouchChanged;
        ApplyCrouchState(isCrouching.Value);

        if (IsOwner)
        {
            inputActions.Enable();

            if (audioListener != null)
                audioListener.enabled = true;

            Camera cam = cameraPivot != null
                ? cameraPivot.GetComponentInChildren<Camera>(true)
                : null;

            if (cam != null)
                cam.enabled = true;
        }
        else
        {
            inputActions.Disable();

            if (audioListener != null)
                audioListener.enabled = false;

            Camera cam = cameraPivot != null
                ? cameraPivot.GetComponentInChildren<Camera>(true)
                : null;

            if (cam != null)
                cam.enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        isCrouching.OnValueChanged -= OnCrouchChanged;

        if (inputActions != null)
            inputActions.Disable();
    }

    public override void OnDestroy()
    {
        if (inputActions != null)
            inputActions.Dispose();

        base.OnDestroy();
    }

    public void SetGameplayPaused(bool paused)
    {
        if (!IsServer)
            return;

        gameplayPaused.Value = paused;

        if (paused)
        {
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            accumulatedMouseX = 0f;
        }

        Debug.Log(
            $"PLAYER: Gameplay {(paused ? "paused" : "resumed")} " +
            $"for client {OwnerClientId}"
        );
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (gameplayPaused.Value)
        {
            if (IsOwner)
            {
                moveInput = Vector2.zero;
                lookInput = Vector2.zero;
            }

            if (IsServer)
                moveInput = Vector2.zero;

            return;
        }

        if (IsOwner)
        {
            ReadInput();
            HandleLook();
            HandleAiming();
            HandleShooting();
            HandleReload();
            HandleCrouchInput();

            accumulatedMouseX += lookInput.x;

            if (Mathf.Abs(accumulatedMouseX) > 0.001f)
            {
                SendLookInputServerRpc(accumulatedMouseX);
                accumulatedMouseX = 0f;
            }

            inputSendTimer += Time.deltaTime;

            if (inputSendTimer >= InputSendInterval)
            {
                inputSendTimer = 0f;
                SendMovementInputServerRpc(moveInput);
            }
        }

        if (IsServer)
            UpdateMovement();
    }

    private void ReadInput()
    {
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();
        lookInput = inputActions.Player.Look.ReadValue<Vector2>();
    }

    private void HandleCrouchInput()
    {
        if (Keyboard.current != null &&
            Keyboard.current.cKey.wasPressedThisFrame)
        {
            ToggleCrouchServerRpc();
        }
    }

    [ServerRpc]
    private void ToggleCrouchServerRpc()
    {
        isCrouching.Value = !isCrouching.Value;
    }

    private void OnCrouchChanged(bool oldValue, bool newValue)
    {
        ApplyCrouchState(newValue);
    }

    private void ApplyCrouchState(bool crouching)
    {
        if (controller != null)
        {
            if (crouching)
            {
                controller.height = crouchHeight;

                float heightDifference =
                    standingHeight - crouchHeight;

                controller.center =
                    standingCenter - new Vector3(
                        0f,
                        heightDifference / 2f,
                        0f
                    );
            }
            else
            {
                controller.height = standingHeight;
                controller.center = standingCenter;
            }
        }

        if (IsOwner && cameraPivot != null)
        {
            Vector3 targetPosition = standingCameraPosition;

            if (crouching)
                targetPosition.y -= crouchCameraOffset;

            cameraPivot.localPosition = targetPosition;
        }

        if (playerAnimator != null)
            playerAnimator.SetBool("Crouching", crouching);
    }

    [ServerRpc]
    private void SendMovementInputServerRpc(Vector2 input)
    {
        if (gameplayPaused.Value)
            return;

        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    [ServerRpc]
    private void SendLookInputServerRpc(float mouseX)
    {
        if (gameplayPaused.Value)
            return;

        if (Mathf.Abs(mouseX) > 0.001f)
        {
            float rotationAmount = mouseX * lookSensitivity;
            transform.Rotate(Vector3.up * rotationAmount);
        }
    }

    private void UpdateMovement()
    {
        if (gameplayPaused.Value ||
            controller == null ||
            !controller.enabled)
            return;

        float currentSpeed =
            isCrouching.Value ? crouchMoveSpeed : moveSpeed;

        Vector3 direction = new Vector3(
            moveInput.x,
            0f,
            moveInput.y
        );

        direction = transform.TransformDirection(direction);

        controller.Move(
            direction * currentSpeed * Time.deltaTime
        );

        if (playerAnimator != null)
        {
            float animationSpeed =
                moveInput.magnitude > 0.1f ? 1f : 0f;

            playerAnimator.SetFloat("Speed", animationSpeed);
        }
    }

    private void HandleLook()
    {
        if (cameraPivot == null)
            return;

        float mouseY = lookInput.y * lookSensitivity;

        cameraPitch -= mouseY;

        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxLookAngle,
            maxLookAngle
        );

        cameraPivot.localRotation =
            Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    private void HandleAiming()
    {
        bool isAiming = inputActions.Player.Aim.IsPressed();

        if (playerAnimator != null)
            playerAnimator.SetBool("Aiming", isAiming);

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
}
