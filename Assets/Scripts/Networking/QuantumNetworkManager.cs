using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

public class QuantumNetworkManager : MonoBehaviour
{
    private async void Awake()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            Debug.Log("Quantum-Heist: Unity Services initialized!");
            Debug.Log("Quantum-Heist Player ID: " +
                      AuthenticationService.Instance.PlayerId);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Quantum-Heist initialization failed: " + e);
        }
    }
}