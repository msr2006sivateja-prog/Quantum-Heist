
using UnityEngine;

public class BankKeyPickup : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PlayerInventory inventory =
            other.GetComponentInParent<PlayerInventory>();

        if (inventory == null)
            return;

        inventory.CollectBankKey();

        Debug.Log("Bank key collected and stored!");
        gameObject.SetActive(false);
    }
}
