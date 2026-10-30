
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class NetworkPlayerSpawner : MonoBehaviour
{
    [Header("Player Settings")]
    [SerializeField] private GameObject playerPrefab;

    [Header("Dimension Spawn Points")]
    [SerializeField] private Transform dimension1Spawn;
    [SerializeField] private Transform dimension2Spawn;

    private NetworkManager networkManager;

    private readonly HashSet<ulong> spawningClients = new();

    private void Start()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("[Spawner] NetworkManager not found!");
            return;
        }

        networkManager.OnClientConnectedCallback += HandleClientConnected;

        Debug.Log("[Spawner] Listener registered.");

        if (networkManager.IsServer)
        {
            foreach (ulong clientId in networkManager.ConnectedClientsIds)
            {
                HandleClientConnected(clientId);
            }
        }
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (networkManager == null || !networkManager.IsServer)
            return;

        Transform spawnPoint =
            clientId == NetworkManager.ServerClientId
                ? dimension1Spawn
                : dimension2Spawn;

        if (spawnPoint == null)
        {
            Debug.LogError(
                $"[Spawner] Spawn point missing for client {clientId}!"
            );
            return;
        }

        Debug.Log(
            $"[Spawner] Client {clientId} selected {spawnPoint.name} " +
            $"at {spawnPoint.position}"
        );

        // Check whether this client already has a player.
        if (networkManager.ConnectedClients.TryGetValue(
                clientId, out var client) &&
            client.PlayerObject != null)
        {
            Debug.Log(
                $"[Spawner] Client {clientId} already has a player. " +
                "Correcting its spawn position."
            );

            MovePlayerToSpawn(
                client.PlayerObject,
                spawnPoint
            );

            return;
        }

        if (!spawningClients.Add(clientId))
            return;

        if (playerPrefab == null)
        {
            Debug.LogError("[Spawner] Player Prefab is missing!");
            spawningClients.Remove(clientId);
            return;
        }

        GameObject player = Instantiate(
            playerPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        NetworkObject networkObject =
            player.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError(
                "[Spawner] Player prefab has no NetworkObject!"
            );

            Destroy(player);
            spawningClients.Remove(clientId);
            return;
        }

        networkObject.SpawnAsPlayerObject(clientId, true);

        Debug.Log(
            $"[Spawner] SUCCESS: Client {clientId} spawned at " +
            $"{spawnPoint.name}, position {spawnPoint.position}"
        );
    }

    private void MovePlayerToSpawn(
        NetworkObject player,
        Transform spawnPoint)
    {
        if (player == null || spawnPoint == null)
            return;

        // Temporarily disable CharacterControllers while teleporting.
        CharacterController[] controllers =
            player.GetComponentsInChildren<CharacterController>(true);

        foreach (CharacterController controller in controllers)
        {
            controller.enabled = false;
        }

        player.transform.SetPositionAndRotation(
            spawnPoint.position,
            spawnPoint.rotation
        );

        foreach (CharacterController controller in controllers)
        {
            controller.enabled = true;
        }

        Debug.Log(
            $"[Spawner] Corrected player position: " +
            $"{player.transform.position}"
        );
    }
}
