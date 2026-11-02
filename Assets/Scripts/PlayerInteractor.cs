
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 3f;

    private NetworkObject networkObject;
    private PlayerInventory inventory;

    private BankDoorUnlock nearbyDoor;
    private BankLockerUnlock nearbyLocker;

    private void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        inventory = GetComponentInChildren<PlayerInventory>();
    }

    private void Update()
    {
        if (networkObject == null ||
            !networkObject.IsSpawned ||
            !networkObject.IsOwner)
            return;

        nearbyDoor = null;
        nearbyLocker = null;

        float closestDistance = interactionDistance;

        BankDoorUnlock[] doors =
            FindObjectsByType<BankDoorUnlock>(FindObjectsSortMode.None);

        foreach (BankDoorUnlock door in doors)
        {
            if (door == null || door.IsUnlocked)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                door.transform.position
            );

            if (distance <= closestDistance)
            {
                closestDistance = distance;
                nearbyDoor = door;
                nearbyLocker = null;
            }
        }

        BankLockerUnlock[] lockers =
            FindObjectsByType<BankLockerUnlock>(FindObjectsSortMode.None);

        foreach (BankLockerUnlock locker in lockers)
        {
            if (locker == null || locker.IsUnlocked)
                continue;

            float distance = Vector3.Distance(
                transform.position,
                locker.transform.position
            );

            if (distance <= closestDistance)
            {
                closestDistance = distance;
                nearbyLocker = locker;
                nearbyDoor = null;
            }
        }

        if (Keyboard.current != null &&
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (inventory == null || !inventory.HasBankKey)
            {
                if (nearbyDoor != null || nearbyLocker != null)
                    Debug.Log("You need the Bank Key!");

                return;
            }

            if (nearbyDoor != null)
                nearbyDoor.RequestUnlock();

            if (nearbyLocker != null)
                nearbyLocker.RequestUnlock();
        }
    }

    private void OnGUI()
    {
        if (networkObject == null ||
            !networkObject.IsSpawned ||
            !networkObject.IsOwner)
            return;

        if (nearbyDoor == null && nearbyLocker == null)
            return;

        string message =
            inventory != null && inventory.HasBankKey
            ? "PRESS E TO UNLOCK"
            : "BANK KEY REQUIRED";

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 20;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.white;

        GUI.Box(
            new Rect(
                Screen.width / 2f - 150f,
                Screen.height - 130f,
                300f,
                50f
            ),
            message,
            style
        );
    }
}
