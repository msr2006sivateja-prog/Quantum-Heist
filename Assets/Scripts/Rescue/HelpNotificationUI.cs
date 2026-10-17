
using System.Collections;
using TMPro;
using UnityEngine;

public class HelpNotificationUI : MonoBehaviour
{
    public static HelpNotificationUI Instance { get; private set; }

    [SerializeField] private TMP_Text notificationText;
    [SerializeField] private float displayDuration = 4f;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        Instance = this;

        if (notificationText == null)
            notificationText = GetComponent<TMP_Text>();

        notificationText.text = "";
    }

    public void ShowHelpRequest(ulong playerId)
    {
        notificationText.text =
            $"HELP REQUESTED BY PLAYER {playerId}";

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);

        notificationText.text = "";
        hideCoroutine = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    
    public void ShowMessage(string message)
    {
        notificationText.text = message;

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

}
