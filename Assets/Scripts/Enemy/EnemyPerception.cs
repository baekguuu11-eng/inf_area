using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPerception : MonoBehaviour
{
    [Header("감지 갱신")]
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.08f;
    [SerializeField] private LayerMask sightBlockMask;

    private Transform player;
    private Rigidbody2D playerBody;
    private float nextRefreshTime;
    private Vector2 lastPlayerPosition;
    private Vector2 estimatedVelocity;

    public Transform Player => player;
    public bool HasPlayer => player != null;
    public Vector2 PlayerPosition => player != null ? (Vector2)player.position : (Vector2)transform.position;
    public Vector2 PlayerVelocity => playerBody != null ? playerBody.linearVelocity : estimatedVelocity;
    public Vector2 DirectionToPlayer
    {
        get
        {
            Vector2 delta = PlayerPosition - (Vector2)transform.position;
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down;
        }
    }
    public float DistanceToPlayer => player != null ? Vector2.Distance(transform.position, player.position) : float.PositiveInfinity;

    private void Awake()
    {
        ResolvePlayer();
    }

    private void OnEnable()
    {
        ResolvePlayer();
        nextRefreshTime = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);
        RefreshNow();
    }

    public void RefreshNow()
    {
        if (player == null)
        {
            ResolvePlayer();
            return;
        }

        float dt = Mathf.Max(0.02f, refreshInterval);
        Vector2 current = player.position;
        Vector2 measured = (current - lastPlayerPosition) / dt;
        estimatedVelocity = Vector2.Lerp(estimatedVelocity, measured, 0.35f);
        lastPlayerPosition = current;
    }

    public bool HasLineOfSight()
    {
        return HasLineOfSight(sightBlockMask);
    }

    public bool HasLineOfSight(LayerMask overrideMask)
    {
        if (player == null)
            return false;
        if (overrideMask.value == 0)
            return true;

        Vector2 origin = transform.position;
        Vector2 target = player.position;
        Vector2 delta = target - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return true;

        RaycastHit2D hit = Physics2D.Raycast(origin, delta / distance, distance, overrideMask);
        return hit.collider == null;
    }

    public Vector2 PredictPlayerPosition(float seconds, float maxPredictionDistance = 1.2f)
    {
        if (player == null)
            return transform.position;

        Vector2 offset = PlayerVelocity * Mathf.Max(0f, seconds);
        if (offset.magnitude > maxPredictionDistance)
            offset = offset.normalized * maxPredictionDistance;
        return PlayerPosition + offset;
    }

    private void ResolvePlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
            return;

        player = playerObject.transform;
        playerBody = playerObject.GetComponent<Rigidbody2D>();
        lastPlayerPosition = player.position;
    }
}
