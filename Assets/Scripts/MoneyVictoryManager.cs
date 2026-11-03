
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class MoneyVictoryManager : NetworkBehaviour
{
    public static MoneyVictoryManager Instance { get; private set; }

    [Header("Victory Settings")]
    [SerializeField] private int requiredPlayers = 2;

    private readonly HashSet<ulong> playersWhoReachedMoney =
        new HashSet<ulong>();

    private readonly NetworkVariable<bool> gameWon =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public bool GameWon => gameWon.Value;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        gameWon.OnValueChanged += OnGameWonChanged;
    }

    public override void OnNetworkDespawn()
    {
        gameWon.OnValueChanged -= OnGameWonChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Called by MoneyVictoryTrigger when a player reaches the money.
    public void PlayerReachedMoney(ulong playerId)
    {
        if (!IsServer || gameWon.Value)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(playerId))
            return;

        // Prevent the same player from counting twice.
        if (!playersWhoReachedMoney.Add(playerId))
            return;

        Debug.Log(
            $"VICTORY: Player {playerId} reached the money. " +
            $"Players collected: {playersWhoReachedMoney.Count}/{requiredPlayers}"
        );

        if (playersWhoReachedMoney.Count >= requiredPlayers)
        {
            EndGame();
        }
    }

    private void EndGame()
    {
        if (!IsServer || gameWon.Value)
            return;

        gameWon.Value = true;

        Debug.Log("VICTORY: BOTH PLAYERS REACHED THE MONEY!");

        // Pause every connected player.
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null)
                continue;

            PlayerController controller =
                client.PlayerObject.GetComponent<PlayerController>();

            if (controller != null)
                controller.SetGameplayPaused(true);
        }
    }

    private void OnGameWonChanged(bool oldValue, bool newValue)
    {
        if (newValue)
            Debug.Log("CONGRATULATIONS! YOU COMPLETED THE QUANTUM HEIST!");
    }

    private void OnGUI()
    {
        if (!gameWon.Value)
            return;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 42;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = Color.yellow;

        GUIStyle messageStyle = new GUIStyle(GUI.skin.label);
        messageStyle.fontSize = 24;
        messageStyle.alignment = TextAnchor.MiddleCenter;
        messageStyle.normal.textColor = Color.white;

        GUIStyle backgroundStyle = new GUIStyle(GUI.skin.box);

        GUI.Box(
            new Rect(
                Screen.width / 2f - 300f,
                Screen.height / 2f - 130f,
                600f,
                260f
            ),
            "",
            backgroundStyle
        );

        GUI.Label(
            new Rect(
                Screen.width / 2f - 280f,
                Screen.height / 2f - 100f,
                560f,
                80f
            ),
            "CONGRATULATIONS!",
            titleStyle
        );

        GUI.Label(
            new Rect(
                Screen.width / 2f - 280f,
                Screen.height / 2f - 20f,
                560f,
                100f
            ),
            "QUANTUM HEIST COMPLETED!\nBoth players secured the money!",
            messageStyle
        );
    }
}
