using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class QuantumRelayManager : MonoBehaviour
{
    [SerializeField] private string joinCode;

    private UnityTransport transport;

    private void Awake()
    {
        transport = FindAnyObjectByType<UnityTransport>();
    }

    [ContextMenu("Create Game")]
    public async void CreateGame()
    {
        try
        {
            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(1);

            string code =
                await RelayService.Instance.GetJoinCodeAsync(
                    allocation.AllocationId);

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "dtls")
            );

            Debug.Log("Quantum-Heist: Game created! Join code: " + code);

            NetworkManager.Singleton.StartHost();
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "Quantum-Heist: Failed to create game: " + e.Message);
        }
    }

    [ContextMenu("Join Game")]
    public async void JoinGame()
    {
        try
        {
            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "dtls")
            );

            Debug.Log(
                "Quantum-Heist: Joining game with join code: " + joinCode);

            NetworkManager.Singleton.StartClient();
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                "Quantum-Heist: Failed to join game: " + e.Message);
        }
    }
}