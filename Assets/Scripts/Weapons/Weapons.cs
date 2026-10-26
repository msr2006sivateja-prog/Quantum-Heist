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
    [SerializeField] private Animator playerAnimator;

    private int currentAmmo;
    private float nextFireTime;
    private bool isReloading;

    private void Awake()
    {
        currentAmmo = magazineSize;

        // Find the soldier's Animator if it hasn't been assigned.
        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();

        if (playerAnimator == null)
            playerAnimator = GetComponentInParent<Animator>();
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

        // Trigger the soldier's shooting animation.
        if (playerAnimator != null)
        {
            Debug.Log("Weapon Animator: " + playerAnimator.name);
            playerAnimator.SetTrigger("Shoot");
        }
        else
        {
            Debug.LogError("Weapon Animator is NULL!");
        }

        if (playerCamera == null)
        {
            Debug.LogWarning("Weapon: Player Camera is not assigned.", this);
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        Vector3 endPoint = ray.origin + ray.direction * range;

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            endPoint = hit.point;

            Debug.Log($"Hit: {hit.collider.name}");

            TargetHealth targetHealth =
                hit.collider.GetComponent<TargetHealth>();

            if (targetHealth != null)
                targetHealth.TakeDamage(Mathf.RoundToInt(damage));
        }

        DrawTracer(ray.origin, endPoint);

        Debug.Log($"Ammo: {currentAmmo}/{magazineSize}");
    }

    private void DrawTracer(Vector3 startPoint, Vector3 endPoint)
    {
        GameObject tracerObject = new GameObject("BulletTracer");

        LineRenderer line = tracerObject.AddComponent<LineRenderer>();

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            Destroy(tracerObject);
            return;
        }

        line.material = new Material(shader);
        line.positionCount = 2;
        line.useWorldSpace = true;

        line.SetPosition(0, startPoint);
        line.SetPosition(1, endPoint);

        line.startWidth = 0.025f;
        line.endWidth = 0.008f;

        line.startColor = Color.yellow;
        line.endColor = Color.red;

        line.numCapVertices = 4;
        line.numCornerVertices = 2;

        Destroy(tracerObject, 0.06f);
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

        // Start the reload animation.
        if (playerAnimator != null)
            playerAnimator.SetBool("Reloading", true);

        Debug.Log("Reloading...");

        yield return new WaitForSeconds(reloadTime);

        currentAmmo = magazineSize;
        isReloading = false;

        // Return to the normal animation state.
        if (playerAnimator != null)
            playerAnimator.SetBool("Reloading", false);

        Debug.Log("Reload complete!");
    }
}