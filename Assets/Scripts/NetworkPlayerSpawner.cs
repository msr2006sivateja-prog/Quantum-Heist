
using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform dimension1Spawn;
    [SerializeField] private Transform dimension2Spawn;

    private NetworkManager networkManager;

    private void Start()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("NetworkPlayerSpawner: NetworkManager not found.");
            return;
        }

        networkManager.OnClientConnectedCallback += HandleClientConnected;
    }

    private void OnDestroy()
    {
        if (networkManager != null)
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"CONNECTED: {clientId}, IsServer: {networkManager.IsServer}");
        if (!networkManager.IsServer)
            return;

        if (networkManager.ConnectedClients.ContainsKey(clientId) &&
            networkManager.ConnectedClients[clientId].PlayerObject != null)
            return;

        Transform spawnPoint = clientId == NetworkManager.ServerClientId
            ? dimension1Spawn
            : dimension2Spawn;

        if (playerPrefab == null || spawnPoint == null)
        {
            Debug.LogError("Assign the player prefab and both spawn points.");
            return;
        }

        GameObject player = Instantiate(
            playerPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        NetworkObject networkObject = player.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError("Player prefab needs a NetworkObject component.");
            Destroy(player);
            return;
        }
        
        Debug.Log($"SPAWNING {clientId} AT {spawnPoint.name}: {spawnPoint.position}");
        networkObject.SpawnAsPlayerObject(clientId, true);

        Debug.Log($"Player {clientId} spawned at {spawnPoint.name}");
    }
}
