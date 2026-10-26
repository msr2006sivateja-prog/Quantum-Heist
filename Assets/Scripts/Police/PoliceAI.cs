
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
            Debug.LogError("PoliceAI: NavMeshAgent is missing!", this);
            enabled = false;
            return;
        }

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogError("PoliceAI: Assign patrol points!", this);
            enabled = false;
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("PoliceAI: Officer is not on the NavMesh!", this);
            enabled = false;
            return;
        }

        agent.speed = patrolSpeed;
        FindPlayer();
        GoToCurrentPoint();
    }

    private void Update()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        if (player == null)
            FindPlayer();

        float distanceToPlayer = player != null
            ? Vector3.Distance(transform.position, player.position)
            : Mathf.Infinity;

        // Start chasing when the player is close enough.
        if (!chasing && distanceToPlayer <= detectionRange)
        {
            chasing = true;
            waiting = false;
            agent.isStopped = false;
            agent.speed = chaseSpeed;

            Debug.Log("Police spotted the player! Chasing.", this);
        }

        // Stop chasing when the player escapes far enough.
        if (chasing && distanceToPlayer >= losePlayerRange)
        {
            chasing = false;
            agent.speed = patrolSpeed;

            Debug.Log("Police lost the player. Returning to patrol.", this);

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

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
            player = playerObject.transform;
    }

    private void GoToCurrentPoint()
    {
        if (patrolPoints[currentPointIndex] == null)
        {
            Debug.LogWarning(
                $"PoliceAI: Patrol point {currentPointIndex} is empty!",
                this
            );

            waiting = true;
            waitTimer = waitAtPoint;
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(patrolPoints[currentPointIndex].position);
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
            normalizedSpeed * animationSpeedMultiplier
        );
    }
}
