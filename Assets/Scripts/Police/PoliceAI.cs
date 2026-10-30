
using UnityEngine;
using UnityEngine.AI;

public class PoliceAI : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float arrivalDistance = 1f;
    [SerializeField] private float waitAtPoint = 1.5f;

    [Header("Bank Detection")]
    [SerializeField] private Transform bankDetectionCenter;
    [SerializeField] private float bankDetectionRadius = 100f;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float losePlayerRange = 12f;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 5.5f;
    [SerializeField] private float patrolSpeed = 3f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float animationSpeedMultiplier = 1f;

    private NavMeshAgent agent;
    private Transform player;

    private int currentPointIndex;
    private float waitTimer;

    private bool waiting;
    private bool chasing;

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private void Start()
    {
        if (agent == null)
        {
            Debug.LogError("PoliceAI: NavMeshAgent missing!", this);
            enabled = false;
            return;
        }

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogError("PoliceAI: Assign patrol points!", this);
            enabled = false;
            return;
        }

        if (bankDetectionCenter == null)
        {
            Debug.LogError(
                "PoliceAI: Assign Bank Detection Center!", this);
            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("PoliceAI: Officer is not on NavMesh!", this);
            enabled = false;
            return;
        }

        agent.speed = patrolSpeed;
        GoToCurrentPoint();
    }

    private void Update()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        float searchRange = chasing
            ? losePlayerRange
            : detectionRange;

        Transform closestPlayer = FindClosestPlayer(searchRange);

        if (closestPlayer != null)
        {
            if (!chasing)
            {
                chasing = true;
                waiting = false;

                agent.isStopped = false;
                agent.speed = chaseSpeed;

                Debug.Log("Police spotted a player!", this);
            }

            player = closestPlayer;
        }
        else if (chasing)
        {
            chasing = false;
            player = null;

            agent.speed = patrolSpeed;

            Debug.Log(
                "Police lost all players. Returning to patrol.",
                this);

            GoToCurrentPoint();
        }

        if (chasing && player != null)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            UpdatePatrol();
        }

        UpdateAnimation(agent.velocity.magnitude);
    }

    private Transform FindClosestPlayer(float maxDistance)
    {
        GameObject[] players =
            GameObject.FindGameObjectsWithTag(playerTag);

        Transform closest = null;
        float closestDistance = maxDistance;

        foreach (GameObject playerObject in players)
        {
            if (playerObject == null ||
                !playerObject.activeInHierarchy)
                continue;

            Transform candidate = playerObject.transform;

            // Ignore players outside this bank's area.
            float bankDistance = Vector3.Distance(
                bankDetectionCenter.position,
                candidate.position);

            if (bankDistance > bankDetectionRadius)
                continue;

            // Check distance from this police officer.
            float distance = Vector3.Distance(
                transform.position,
                candidate.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    private void UpdatePatrol()
    {
        if (waiting)
        {
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
            {
                waiting = false;

                currentPointIndex =
                    (currentPointIndex + 1) % patrolPoints.Length;

                GoToCurrentPoint();
            }

            return;
        }

        if (!agent.pathPending &&
            agent.hasPath &&
            agent.remainingDistance <= arrivalDistance)
        {
            waiting = true;
            waitTimer = waitAtPoint;
            agent.isStopped = true;
        }
    }

    private void GoToCurrentPoint()
    {
        if (patrolPoints[currentPointIndex] == null)
            return;

        agent.isStopped = false;
        agent.speed = patrolSpeed;

        agent.SetDestination(
            patrolPoints[currentPointIndex].position);
    }

    private void UpdateAnimation(float currentSpeed)
    {
        if (animator == null)
            return;

        float normalizedSpeed = agent.speed > 0f
            ? currentSpeed / agent.speed
            : 0f;

        animator.SetFloat(
            SpeedHash,
            normalizedSpeed * animationSpeedMultiplier);
    }

    private void OnDrawGizmosSelected()
    {
        if (bankDetectionCenter == null)
            return;

        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            bankDetectionCenter.position,
            bankDetectionRadius);
    }
}
