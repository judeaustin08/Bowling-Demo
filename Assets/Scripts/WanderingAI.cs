using Pathfinding;
using UnityEngine;

using static Utils;

[RequireComponent(typeof(CharacterController), typeof(Seeker))]
public class WanderingAI : MonoBehaviour
{
    private AstarPath asp;

    [SerializeField] private float speed = 2;

    private float distanceToWaypoint = 0;
    [Tooltip("The radius around a waypoint which will be counted as having reached the waypoint")]
    [SerializeField] private float nextWaypointDistance = 1;
    private int currentWaypoint = 0;
    [HideInInspector] public bool reachedEndOfPath = false;
    private Seeker seeker;
    private CharacterController _cc;

    [SerializeField] private float minTargetInterval = 0.4f;
    private float timer;
    [SerializeField] private double targetChance = 0.04f;

    private Path path;
    private readonly System.Random rand = new();

    [Tooltip("The radius around this NPC's initial position in which it will wander")]
    [SerializeField] private float territoryRadius = 10f;
    private Vector3 initialPosition;

    private Vector3 dir = new();
    private Vector3 movement = new();
    private float angle = 0;

    private void Awake()
    {
        seeker = GetComponent<Seeker>();
        _cc = GetComponent<CharacterController>();
        asp = AstarPath.active;
    }

    private void OnDisable()
    {
        // Ensure the callback is removed
        seeker.pathCallback -= OnPathComplete;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        initialPosition = transform.position;

        // Path requests are asynchronous, so the function will run a callback on completion. Assign the callback here to apply to every request.
        seeker.pathCallback += OnPathComplete;

        ChooseTarget();
    }

    private void Update()
    {
        if (reachedEndOfPath && Time.time - timer >= minTargetInterval && rand.NextDouble() < targetChance)
            ChooseTarget();

        // Return early if there is no path
        if (path == null) return;

        //Debug.Log("Following path");

        Follow();

        // Slow the seeker down gradually as it approaches the end of the path
        float speedFactor = reachedEndOfPath ? Mathf.Sqrt(distanceToWaypoint / nextWaypointDistance) : 1f;

        if (reachedEndOfPath) return;

        dir = (path.vectorPath[currentWaypoint] - transform.position).normalized;
        movement = dir * speed * speedFactor * Time.deltaTime;
        _cc.Move(movement);
    }

    public void OnPathComplete(Path p)
    {
        //Debug.Log(p.error ? "There was an error generating the path!" : "Successfully returned a path.");

        if (!p.error)
        {
            path = p;
            currentWaypoint = 0;
        }
    }

    private void Follow()
    {
        // Check if we have reached the next waypoint in a loop.
        // Check this in a loop because there may be multiple waypoints close together, in which case
        // we want to reach them all in the same frame.
        while (true)
        {
            distanceToWaypoint = Vector3.Distance(transform.position, path.vectorPath[currentWaypoint]);
            if (distanceToWaypoint < nextWaypointDistance)
                if (currentWaypoint + 1 < path.vectorPath.Count)
                {
                    currentWaypoint++;

                    dir = (path.vectorPath[currentWaypoint] - transform.position).normalized;
                    angle = (angle + 180) % 360 - 180;
                    StartCoroutine(LerpAsync(
                        angle,
                        Vector3.SignedAngle(Vector3.forward, dir, Vector3.up),
                        0.1f,
                        (nv) => {
                            angle = nv;

                            transform.rotation = Quaternion.Euler(
                                0,
                                angle,
                                0
                            );
                        }
                    ));
                }
                else
                {
                    // Set the status variable in case the game has code which needs to know
                    reachedEndOfPath = true;
                    path = null;
                    timer = Time.time;
                    break;
                }
            else break;
        }
    }

    private void ChooseTarget()
    {
        reachedEndOfPath = false;
        GridGraph grid = asp.data.gridGraph;
        GridNode randomNode = null;

        while (randomNode == null || !randomNode.Walkable || !PathUtilities.IsPathPossible(
            asp.GetNearest(transform.position, NNConstraint.Default).node,
            randomNode
        ))
        {
            // Select from a radius
            Vector3 point = Random.insideUnitSphere * territoryRadius;
            point.y = 0;
            point += initialPosition;
            randomNode = (GridNode)grid.GetNearest(point, NNConstraint.Default).node;
            // Select from the whole grid
            //randomNode = grid.nodes[Random.Range(0, grid.nodes.Length)];
        }

        // Request to seeker to begin calculating a path.
        seeker.StartPath(transform.position, grid.nodeSize * (Vector3)randomNode.position);
    }

    /*
    void OnDrawGizmos()
    {
        UnityEditor.Handles.DrawWireDisc(
            Application.isPlaying ? initialPosition : transform.position,
            Vector3.up,
            territoryRadius
        );
    }
    */
}
