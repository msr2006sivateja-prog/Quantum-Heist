
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public bool HasBankKey { get; private set; }

    public void CollectBankKey()
    {
        if (HasBankKey)
            return;

        HasBankKey = true;
        Debug.Log("Bank key added to inventory!");
    }
}
