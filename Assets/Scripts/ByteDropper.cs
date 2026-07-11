using UnityEngine;

public class ByteDropper : MonoBehaviour
{
    [Header("Byte Drop")]
    [SerializeField] private BytePickup bytePrefab;
    [SerializeField] private int minDropCount = 3;
    [SerializeField] private int maxDropCount = 5;
    [SerializeField] private int valuePerByte = 1;

    [Header("Spawn")]
    [SerializeField] private float spawnRadius = 0.15f;
    [SerializeField] private int sortingOrder = 20;

    private ByteCurrencyManager currencyManager;

    private void Awake()
    {
        currencyManager = ByteCurrencyManager.Instance;
    }

    public void DropBytes(Vector3 worldPosition)
    {
        if (currencyManager == null)
            currencyManager = ByteCurrencyManager.Instance;

        int min = Mathf.Max(0, minDropCount);
        int max = Mathf.Max(min, maxDropCount);
        int dropCount = Random.Range(min, max + 1);

        for (int i = 0; i < dropCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPosition = worldPosition + new Vector3(offset.x, offset.y, 0f);
            spawnPosition.z = 0f;

            BytePickup pickup = CreatePickup(spawnPosition);

            if (pickup != null)
                pickup.Initialize(valuePerByte, currencyManager);
        }
    }

    private BytePickup CreatePickup(Vector3 spawnPosition)
    {
        if (bytePrefab != null)
        {
            return Instantiate(bytePrefab, spawnPosition, Quaternion.identity);
        }

        GameObject byteObject = new GameObject("BytePickup_Runtime");
        byteObject.transform.position = spawnPosition;
        byteObject.transform.localScale = Vector3.one * 0.45f;

        SpriteRenderer spriteRenderer = byteObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = sortingOrder;

        return byteObject.AddComponent<BytePickup>();
    }
}