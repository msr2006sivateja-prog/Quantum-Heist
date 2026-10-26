
using UnityEngine;
using UnityEngine.AI;

public class PoliceAI : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float arrivalDistance = 1.0f;
    [SerializeField] private float waitAtPoint = 1.5f;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float losePlayerRange = 12f;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 5.5f;
    [SerializeField] private float patrolSpeed = 3.0f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float animationSpeedMultiplier = 1.0f;

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
            Debug.LogError(
                "PoliceAI: NavMeshAgent is missing!", this);
            enabled = false;
            return;
        }

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogError(
                "PoliceAI: Assign patrol points!", this);
            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                "PoliceAI: Officer is not on the NavMesh!", this);
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

        // Search for all players, not just the first one.
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

                Debug.Log(
                    "Police spotted a player! Chasing: " +
                    closestPlayer.name, this);
            }

            // Switch to the closest available player.
            if (player != closestPlayer)
            {
                player = closestPlayer;

                Debug.Log(
                    "Police target changed to: " +
                    player.name, this);
            }
        }
        else if (chasing)
        {
            // No player remains within the allowed chase range.
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
            {
                continue;
            }

            // Ignore the police itself if it was tagged accidentally.
            if (playerObject.transform == transform ||
                playerObject.transform.IsChildOf(transform))
            {
                continue;
            }

            float distance = Vector3.Distance(
                transform.position,
                playerObject.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = playerObject.transform;
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
        if (patrolPoints == null ||
            patrolPoints.Length == 0)
        {
            return;
        }

        if (patrolPoints[currentPointIndex] == null)
        {
            Debug.LogWarning(
                $"PoliceAI: Patrol point {currentPointIndex} is empty!",
                this);

            waiting = true;
            waitTimer = waitAtPoint;
            agent.isStopped = true;
            return;
        }

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
}
