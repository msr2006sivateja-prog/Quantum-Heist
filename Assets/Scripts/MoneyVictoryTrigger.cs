
using UnityEngine;
using Unity.Netcode;

public class MoneyVictoryTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
            return;

        if (MoneyVictoryManager.Instance == null)
        {
            Debug.LogError(
                "VICTORY: MoneyVictoryManager not found."
            );
            return;
        }

        NetworkObject playerObject =
            other.GetComponentInParent<NetworkObject>();

        if (playerObject == null || !playerObject.IsPlayerObject)
            return;

        MoneyVictoryManager.Instance.PlayerReachedMoney(
            playerObject.OwnerClientId
        );
    }
}
