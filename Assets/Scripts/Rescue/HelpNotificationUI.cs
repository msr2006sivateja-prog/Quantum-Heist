
using System.Collections;
using TMPro;
using UnityEngine;

public class HelpNotificationUI : MonoBehaviour
{
    public static HelpNotificationUI Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private TMP_Text notificationText;
    [SerializeField] private float displayDuration = 4f;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "HelpNotificationUI: Duplicate instance found.", this
            );
            return;
        }

        Instance = this;

        if (notificationText == null)
            notificationText = GetComponentInChildren<TMP_Text>(true);

        if (notificationText == null)
        {
            Debug.LogError(
                "HelpNotificationUI: No TMP_Text found. " +
                "Assign the notification text in the Inspector.", this
            );
            return;
        }

        notificationText.text = "";
        notificationText.enabled = true;

        Debug.Log("HELP UI: Notification UI initialized.");
    }

    public void ShowHelpRequest(ulong playerId)
    {
        ShowMessage($"PLAYER {playerId} NEEDS HELP");
    }

    public void ShowMessage(string message)
    {
        if (notificationText == null)
        {
            Debug.LogError("HELP UI: Cannot show message; TMP_Text is missing.");
            return;
        }

        notificationText.enabled = true;
        notificationText.text = message;

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterDelay());

        Debug.Log($"HELP UI: Displaying: {message}");
    }

    public void ShowPersistentStatus(string message)
    {
        if (notificationText == null)
        {
            Debug.LogError("HELP UI: Cannot show status; TMP_Text is missing.");
            return;
        }

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        notificationText.enabled = true;
        notificationText.text = message;

        Debug.Log($"HELP UI: Persistent status: {message}");
    }

    public void ClearStatus()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (notificationText != null)
            notificationText.text = "";
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);

        if (notificationText != null)
            notificationText.text = "";

        hideCoroutine = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
