using System.Collections;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private float fireRate = 0.1f;
    [SerializeField] private float range = 100f;

    [Header("Magazine")]
    [SerializeField] private int magazineSize = 25;
    [SerializeField] private float reloadTime = 1.5f;

    [Header("References")]
    [SerializeField] private Camera playerCamera;

    private int currentAmmo;
    private float nextFireTime;
    private bool isReloading;

    private void Awake()
    {
        currentAmmo = magazineSize;
    }

    public void TryShoot()
    {
        if (isReloading)
            return;

        if (currentAmmo <= 0)
        {
            Reload();
            return;
        }

        if (Time.time < nextFireTime)
            return;

        nextFireTime = Time.time + fireRate;

        Shoot();
    }

    private void Shoot()
    {
        currentAmmo--;

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            Debug.Log($"Hit: {hit.collider.name}");

            Health health = hit.collider.GetComponent<Health>();

            if (health != null)
            {
                health.TakeDamage(damage);
            }
        }

        Debug.Log($"Ammo: {currentAmmo}/{magazineSize}");
    }

    public void Reload()
    {
        if (isReloading)
            return;

        if (currentAmmo == magazineSize)
            return;

        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;

        Debug.Log("Reloading...");

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = magazineSize;

        isReloading = false;

        Debug.Log("Reload complete!");
    }
}