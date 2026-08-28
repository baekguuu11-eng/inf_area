using UnityEngine;

/// <summary>
/// v6.2 재화 드롭.
/// 적 처치 후 Rigidbody2D로 튀어나와 벽에 물리적으로 반사되고,
/// 플레이어가 직접 접근하거나 닿아야 획득됩니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[DisallowMultipleComponent]
public sealed class BytePickup : MonoBehaviour
{
    [Header("Value")]
    [SerializeField] private int value = 1;

    [Header("Physical Scatter")]
    [SerializeField] private float scatterRadius = 1.4f;
    [SerializeField] private float minLaunchSpeed = 2.2f;
    [SerializeField] private float maxLaunchSpeed = 4.1f;
    [SerializeField] private float linearDamping = 3.0f;
    [SerializeField] private float angularSpeed = 420f;
    [SerializeField, Range(0f, 1f)] private float wallBounciness = 0.46f;
    [SerializeField, Range(0f, 1f)] private float wallFriction = 0.10f;
    [SerializeField] private float maximumSpeed = 5.2f;

    [Header("Direct Pickup / Magnet")]
    [SerializeField] private float pickupLockTime = 0.08f;
    [SerializeField] private float directPickupRadius = 0.65f;
    [SerializeField] private float magnetRadius = 2.0f;
    [SerializeField] private float magnetSpeed = 7.5f;
    [SerializeField] private float magnetAcceleration = 45f;
    [SerializeField] private float playerSearchInterval = 0.25f;

    private ByteCurrencyManager currencyManager;
    private RoomController ownerRoom;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private CircleCollider2D pickupCollider;
    private Transform player;
    private float age;
    private float nextPlayerSearchTime;
    private bool isInitialized;
    private bool isCollected;
    private bool magnetLocked;

    private static Sprite fallbackSprite;
    private static PhysicsMaterial2D bounceMaterial;

    public bool IsCollected => isCollected;
    public RoomController OwnerRoom => ownerRoom;
    public int Value => value;

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnEnable()
    {
        age = 0f;
        isCollected = false;
        magnetLocked = false;
        EnsureComponents();
    }

    private void EnsureComponents()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (pickupCollider == null)
            pickupCollider = GetComponent<CircleCollider2D>();

        if (spriteRenderer != null)
        {
            if (spriteRenderer.sprite == null)
                spriteRenderer.sprite = GetFallbackSprite();
            spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, 90);
            V620SpriteMaterialUtility.Apply(spriteRenderer);
        }

        if (body != null)
        {
            body.simulated = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.linearDamping = linearDamping;
            body.angularDamping = 1.6f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.freezeRotation = false;
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = true;
            pickupCollider.isTrigger = false;
            pickupCollider.radius = Mathf.Max(0.08f, pickupCollider.radius > 0f ? pickupCollider.radius : 0.30f);
            pickupCollider.sharedMaterial = GetBounceMaterial();
        }

        int pickupLayer = LayerMask.NameToLayer("Pickup");
        if (pickupLayer >= 0)
            gameObject.layer = pickupLayer;
    }

    public void Initialize(int byteValue, ByteCurrencyManager manager)
    {
        Initialize(byteValue, manager, scatterRadius, GetComponentInParent<RoomController>());
    }

    public void Initialize(int byteValue, ByteCurrencyManager manager, float newScatterRadius)
    {
        Initialize(byteValue, manager, newScatterRadius, GetComponentInParent<RoomController>());
    }

    public void Initialize(int byteValue, ByteCurrencyManager manager, float newScatterRadius, RoomController room)
    {
        if (isInitialized)
            return;

        EnsureComponents();
        value = Mathf.Max(1, byteValue);
        currencyManager = manager != null ? manager : ByteCurrencyManager.Instance;
        scatterRadius = Mathf.Max(0.1f, newScatterRadius);
        ownerRoom = room != null ? room : GetComponentInParent<RoomController>();
        isInitialized = true;
        age = 0f;

        if (ownerRoom != null)
        {
            transform.SetParent(ownerRoom.transform, true);
            ByteRoomRegistry.Register(this, ownerRoom);
        }

        ResolvePlayer(true);
        IgnorePlayerPhysicalCollision();
        Launch();
    }

    private void Launch()
    {
        if (body == null)
            return;

        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.up;

        float radiusFactor = Mathf.Clamp(scatterRadius / 1.4f, 0.65f, 1.5f);
        float speed = Random.Range(minLaunchSpeed, maxLaunchSpeed) * radiusFactor;
        body.linearVelocity = direction * speed;
        body.angularVelocity = Random.Range(-angularSpeed, angularSpeed);
    }

    private void FixedUpdate()
    {
        if (isCollected || body == null)
            return;

        age += Time.fixedDeltaTime;
        ResolvePlayer(false);

        Vector2 velocity = body.linearVelocity;
        if (velocity.magnitude > maximumSpeed)
            body.linearVelocity = velocity.normalized * maximumSpeed;

        if (age < pickupLockTime || player == null)
            return;

        Vector2 toPlayer = (Vector2)player.position - body.position;
        float distance = toPlayer.magnitude;
        bool clearPath = HasClearPathToPlayer();

        if (distance <= directPickupRadius && clearPath)
        {
            CollectImmediately();
            return;
        }

        float activeMagnetRadius = magnetLocked ? magnetRadius * 1.35f : magnetRadius;
        if (distance <= activeMagnetRadius && distance > 0.001f && clearPath)
        {
            if (!magnetLocked)
            {
                // Entering pickup range cancels scatter inertia immediately, so the pickup
                // cannot bounce away just as the player reaches it.
                magnetLocked = true;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.linearDamping = Mathf.Max(linearDamping, 7f);
            }

            Vector2 desiredVelocity = toPlayer / distance * magnetSpeed;
            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity,
                desiredVelocity,
                magnetAcceleration * Time.fixedDeltaTime);
            body.angularVelocity = Mathf.MoveTowards(body.angularVelocity, 0f, 900f * Time.fixedDeltaTime);

            // At close range, collect in the same physics step rather than allowing another bounce.
            float closeEnough = directPickupRadius + body.linearVelocity.magnitude * Time.fixedDeltaTime;
            if (distance <= closeEnough)
                CollectImmediately();
        }
        else if (magnetLocked)
        {
            // Never restore the old outward launch momentum after the player has engaged the pickup.
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void ResolvePlayer(bool force)
    {
        if (!force && player != null)
            return;
        if (!force && Time.unscaledTime < nextPlayerSearchTime)
            return;

        nextPlayerSearchTime = Time.unscaledTime + playerSearchInterval;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
    }

    private void IgnorePlayerPhysicalCollision()
    {
        if (pickupCollider == null || player == null)
            return;

        Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (playerCollider != null)
                Physics2D.IgnoreCollision(pickupCollider, playerCollider, true);
        }
    }

    private bool HasClearPathToPlayer()
    {
        if (player == null)
            return false;

        int mask = BuildWallMask();
        if (mask == 0)
            return true;

        RaycastHit2D hit = Physics2D.Linecast(body.position, player.position, mask);
        return hit.collider == null;
    }

    public void ForceCollect(float duration = 0.16f)
    {
        // 방 전환 직전의 분실 방지용 호출. 플레이 중에는 직접 획득 방식을 유지합니다.
        CollectImmediately();
    }

    public void CollectImmediately()
    {
        if (isCollected)
            return;

        isCollected = true;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }
        if (pickupCollider != null)
            pickupCollider.enabled = false;

        ByteRoomRegistry.Unregister(this, ownerRoom);
        if (currencyManager == null)
            currencyManager = ByteCurrencyManager.Instance;
        if (currencyManager != null)
            currencyManager.AddBytes(value);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected || other == null)
            return;

        if (other.GetComponentInParent<PlayerHealth>() != null && age >= pickupLockTime)
        {
            CollectImmediately();
            return;
        }

        if (IsWall(other.gameObject))
            ReflectFromTrigger(other);
    }

    private void ReflectFromTrigger(Collider2D wall)
    {
        if (body == null || body.linearVelocity.sqrMagnitude < 0.001f)
            return;

        Vector2 closest = wall.ClosestPoint(body.position);
        Vector2 normal = body.position - closest;
        if (normal.sqrMagnitude < 0.0001f)
            normal = body.position - (Vector2)wall.bounds.center;
        if (normal.sqrMagnitude < 0.0001f)
            normal = -body.linearVelocity.normalized;

        normal.Normalize();
        body.position += normal * 0.025f;
        body.linearVelocity = Vector2.Reflect(body.linearVelocity, normal) * Mathf.Max(0.2f, wallBounciness);
    }

    private static int BuildWallMask()
    {
        int mask = 0;
        int wall = LayerMask.NameToLayer("Wall");
        int obstacle = LayerMask.NameToLayer("Obstacle");
        int boundary = LayerMask.NameToLayer("RoomBoundary");
        if (wall >= 0) mask |= 1 << wall;
        if (obstacle >= 0) mask |= 1 << obstacle;
        if (boundary >= 0) mask |= 1 << boundary;
        return mask;
    }

    private static bool IsWall(GameObject target)
    {
        if (target == null)
            return false;

        int wall = LayerMask.NameToLayer("Wall");
        int obstacle = LayerMask.NameToLayer("Obstacle");
        int boundary = LayerMask.NameToLayer("RoomBoundary");
        Transform current = target.transform;
        while (current != null)
        {
            int layer = current.gameObject.layer;
            if ((wall >= 0 && layer == wall) ||
                (obstacle >= 0 && layer == obstacle) ||
                (boundary >= 0 && layer == boundary))
                return true;

            string lower = current.name.ToLowerInvariant();
            if (lower.Contains("wall") || lower.Contains("boundary") || lower.Contains("obstacle"))
                return true;
            current = current.parent;
        }
        return false;
    }

    private PhysicsMaterial2D GetBounceMaterial()
    {
        if (bounceMaterial == null)
        {
            bounceMaterial = new PhysicsMaterial2D("IA_V620_ByteBounce")
            {
                bounciness = wallBounciness,
                friction = wallFriction,
                hideFlags = HideFlags.HideAndDontSave
            };
        }
        return bounceMaterial;
    }

    private void OnDestroy()
    {
        ByteRoomRegistry.Unregister(this, ownerRoom);
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
            return fallbackSprite;

        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "BytePickup_V620_Texture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color clear = Color.clear;
        Color main = new Color(0.15f, 1.35f, 1.18f, 1f);
        Color edge = new Color(0.015f, 0.12f, 0.16f, 1f);
        Color highlight = new Color(1.45f, 1.65f, 1.55f, 1f);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                Color color = clear;
                if (distance <= radius)
                    color = distance > radius - 2f ? edge : main;
                if ((x == 6 || x == 7) && (y == 10 || y == 11))
                    color = highlight;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, true);
        fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 16f, 0u, SpriteMeshType.FullRect);
        fallbackSprite.name = "BytePickup_V620_Sprite";
        fallbackSprite.hideFlags = HideFlags.HideAndDontSave;
        return fallbackSprite;
    }
}
