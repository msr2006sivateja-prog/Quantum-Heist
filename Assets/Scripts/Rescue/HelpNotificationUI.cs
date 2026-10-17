
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
        Instance = this;

        if (notificationText == null)
            notificationText = GetComponent<TMP_Text>();

        if (notificationText != null)
            notificationText.text = "";
    }

    public void ShowHelpRequest(ulong playerId)
    {
        ShowMessage($"PLAYER {playerId} NEEDS HELP");
    }

    public void ShowMessage(string message)
    {
        if (notificationText == null)
        {
            Debug.LogWarning("HelpNotificationUI: Text is not assigned.");
            return;
        }

        notificationText.text = message;

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    public void ShowPersistentStatus(string message)
    {
        if (notificationText == null)
            return;

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        notificationText.text = message;
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
