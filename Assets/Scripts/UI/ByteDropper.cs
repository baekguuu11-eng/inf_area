using UnityEngine;

[DisallowMultipleComponent]
public class ByteDropper : MonoBehaviour
{
    private const float V620VisualMultiplier = 1.25f;
    [Header("Byte Drop")]
    [SerializeField] private BytePickup bytePrefab;
    [SerializeField] private int minDropCount = 3;
    [SerializeField] private int maxDropCount = 5;
    [SerializeField] private int valuePerByte = 1;

    [Header("Spawn")]
    [SerializeField] private float spawnRadius = 0.35f;
    [SerializeField] private float scatterRadius = 1.4f;
    [SerializeField] private float pickupScale = 0.22f;

    [Header("Sorting")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private bool placeBelowPlayer = true;
    [SerializeField] private int manualSortingOrder = -1;

    private ByteCurrencyManager currencyManager;
    private RoomController ownerRoom;
    private bool hasDropped;

    public int MinDropCount => minDropCount;
    public int MaxDropCount => maxDropCount;

    private void Awake()
    {
        currencyManager = ByteCurrencyManager.Instance;
        ownerRoom = GetComponentInParent<RoomController>();
        FindPlayerRendererIfNeeded();
    }

    public void ConfigureDrops(int min, int max, int value = 1)
    {
        minDropCount = Mathf.Max(0, min);
        maxDropCount = Mathf.Max(minDropCount, max);
        valuePerByte = Mathf.Max(1, value);
    }

    public void DropBytes(Vector3 worldPosition)
    {
        if (hasDropped)
            return;

        hasDropped = true;
        if (currencyManager == null)
            currencyManager = ByteCurrencyManager.Instance;
        if (ownerRoom == null)
            ownerRoom = GetComponentInParent<RoomController>();
        FindPlayerRendererIfNeeded();

        int min = Mathf.Max(0, minDropCount);
        int max = Mathf.Max(min, maxDropCount);
        int dropCount = Random.Range(min, max + 1);
        if (dropCount <= 0)
            return;

        float angleStep = 360f / dropCount;
        float startAngle = Random.Range(0f, 360f);
        for (int i = 0; i < dropCount; i++)
        {
            float angle = startAngle + angleStep * i + Random.Range(-18f, 18f);
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            float distance = Random.Range(spawnRadius * 0.4f, Mathf.Max(spawnRadius * 0.4f, spawnRadius));
            Vector3 spawnPosition = worldPosition + (Vector3)(direction * distance);
            spawnPosition.z = 0f;
            spawnPosition = FindSafeSpawnPosition(spawnPosition, worldPosition);

            BytePickup pickup = CreatePickup(spawnPosition);
            if (pickup != null)
                pickup.Initialize(valuePerByte, currencyManager, scatterRadius, ownerRoom);
        }
    }

    private BytePickup CreatePickup(Vector3 spawnPosition)
    {
        Transform parent = ownerRoom != null ? ownerRoom.transform : null;
        BytePickup pickup;

        if (bytePrefab != null)
        {
            pickup = Instantiate(bytePrefab, spawnPosition, Quaternion.identity, parent);
            pickup.transform.localScale = Vector3.one * (pickupScale * V620VisualMultiplier);
            ApplySorting(pickup.gameObject);
            return pickup;
        }

        GameObject obj = new GameObject("BytePickup_Runtime");
        if (parent != null)
            obj.transform.SetParent(parent, true);
        obj.transform.position = spawnPosition;
        obj.transform.localScale = Vector3.one * (pickupScale * V620VisualMultiplier);
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        ApplySorting(renderer);
        pickup = obj.AddComponent<BytePickup>();
        return pickup;
    }

    private static Vector3 FindSafeSpawnPosition(Vector3 candidate, Vector3 fallback)
    {
        int mask = 0;
        int wall = LayerMask.NameToLayer("Wall");
        int obstacle = LayerMask.NameToLayer("Obstacle");
        int boundary = LayerMask.NameToLayer("RoomBoundary");
        if (wall >= 0) mask |= 1 << wall;
        if (obstacle >= 0) mask |= 1 << obstacle;
        if (boundary >= 0) mask |= 1 << boundary;

        if (mask == 0 || Physics2D.OverlapCircle(candidate, 0.10f, mask) == null)
            return candidate;

        Vector2 back = ((Vector2)fallback - (Vector2)candidate).normalized;
        for (int i = 1; i <= 5; i++)
        {
            Vector3 test = candidate + (Vector3)(back * (0.08f * i));
            test.z = 0f;
            if (Physics2D.OverlapCircle(test, 0.10f, mask) == null)
                return test;
        }

        fallback.z = 0f;
        return fallback;
    }

    private void ApplySorting(GameObject target)
    {
        if (target == null)
            return;

        SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = target.GetComponentInChildren<SpriteRenderer>();
        ApplySorting(renderer);
    }

    private void ApplySorting(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        if (placeBelowPlayer && playerSpriteRenderer != null)
        {
            renderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            renderer.sortingOrder = playerSpriteRenderer.sortingOrder - 1;
        }
        else
        {
            renderer.sortingOrder = manualSortingOrder;
        }
    }

    private void FindPlayerRendererIfNeeded()
    {
        if (playerSpriteRenderer != null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerSpriteRenderer = player.GetComponentInChildren<SpriteRenderer>();
    }
}
