
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class NetworkPlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform dimension1Spawn;
    [SerializeField] private Transform dimension2Spawn;

    private NetworkManager networkManager;
    private readonly HashSet<ulong> spawningClients = new();

    private System.Collections.IEnumerator CheckPosition(NetworkObject player, ulong clientId)
    {
        yield return new WaitForSeconds(2f);

        if (player != null)
        {
            Debug.Log(
                $"[POSITION CHECK] Client {clientId}, " +
                $"Owner={player.OwnerClientId}, " +
                $"Position={player.transform.position}"
            );
        }
    }

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
                HandleClientConnected(clientId);
        }
    }

    private void OnDestroy()
    {
        if (networkManager != null)
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"[Spawner] Connected: {clientId}, IsServer={networkManager.IsServer}");

        if (!networkManager.IsServer)
            return;

        if (networkManager.ConnectedClients.TryGetValue(clientId, out var client) &&
            client.PlayerObject != null)
        {
            Debug.Log($"[Spawner] Client {clientId} already has a player.");
            return;
        }

        if (!spawningClients.Add(clientId))
            return;

        Transform spawn = clientId == NetworkManager.ServerClientId
            ? dimension1Spawn
            : dimension2Spawn;

        if (playerPrefab == null || spawn == null)
        {
            Debug.LogError("[Spawner] Assign Player Prefab and both spawn points!");
            spawningClients.Remove(clientId);
            return;
        }

        Debug.Log($"[Spawner] Instantiating client {clientId} at {spawn.name}, position {spawn.position}");

        GameObject player = Instantiate(playerPrefab, spawn.position, spawn.rotation);
        NetworkObject netObj = player.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[Spawner] Player prefab has no NetworkObject!");
            Destroy(player);
            spawningClients.Remove(clientId);
            return;
        }

        netObj.SpawnAsPlayerObject(clientId, true);
        StartCoroutine(CheckPosition(netObj, clientId));
        Debug.Log($"[Spawner] Actual spawned position for {clientId}: {netObj.transform.position}");
        Debug.Log($"[Spawner] SUCCESS: Client {clientId} spawned at {spawn.position}");
    }
}
