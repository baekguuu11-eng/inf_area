using UnityEngine;

public class ByteDropper : MonoBehaviour
{
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

    private void Awake()
    {
        currencyManager = ByteCurrencyManager.Instance;
        FindPlayerRendererIfNeeded();
    }

    public void DropBytes(Vector3 worldPosition)
    {
        if (currencyManager == null)
        {
            currencyManager = ByteCurrencyManager.Instance;
        }

        FindPlayerRendererIfNeeded();

        int min = Mathf.Max(0, minDropCount);
        int max = Mathf.Max(min, maxDropCount);
        int dropCount = Random.Range(min, max + 1);

        float angleStep = 360f / Mathf.Max(1, dropCount);
        float startAngle = Random.Range(0f, 360f);

        for (int i = 0; i < dropCount; i++)
        {
            float angle = startAngle + angleStep * i + Random.Range(-18f, 18f);
            float radian = angle * Mathf.Deg2Rad;

            Vector2 direction = new Vector2(Mathf.Cos(radian), Mathf.Sin(radian));
            float distance = Random.Range(spawnRadius * 0.4f, spawnRadius);

            Vector3 spawnPosition = worldPosition + new Vector3(direction.x, direction.y, 0f) * distance;
            spawnPosition.z = 0f;

            BytePickup pickup = CreatePickup(spawnPosition);

            if (pickup != null)
            {
                pickup.Initialize(valuePerByte, currencyManager, scatterRadius);
            }
        }
    }

    private BytePickup CreatePickup(Vector3 spawnPosition)
    {
        BytePickup pickup;

        if (bytePrefab != null)
        {
            pickup = Instantiate(bytePrefab, spawnPosition, Quaternion.identity);
            pickup.transform.localScale = Vector3.one * pickupScale;

            ApplySorting(pickup.gameObject);

            return pickup;
        }

        GameObject byteObject = new GameObject("BytePickup_Runtime");
        byteObject.transform.position = spawnPosition;
        byteObject.transform.localScale = Vector3.one * pickupScale;

        SpriteRenderer spriteRenderer = byteObject.AddComponent<SpriteRenderer>();
        ApplySorting(spriteRenderer);

        pickup = byteObject.AddComponent<BytePickup>();

        return pickup;
    }

    private void ApplySorting(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return;
        }

        SpriteRenderer spriteRenderer = targetObject.GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            spriteRenderer = targetObject.GetComponentInChildren<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            return;
        }

        ApplySorting(spriteRenderer);
    }

    private void ApplySorting(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (placeBelowPlayer && playerSpriteRenderer != null)
        {
            spriteRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = playerSpriteRenderer.sortingOrder - 1;
        }
        else
        {
            spriteRenderer.sortingOrder = manualSortingOrder;
        }
    }

    private void FindPlayerRendererIfNeeded()
    {
        if (playerSpriteRenderer != null)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            playerSpriteRenderer = player.GetComponentInChildren<SpriteRenderer>();
        }
    }
}