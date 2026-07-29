using System.Collections;
using UnityEngine;

public class PlayerSpeedTrailEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    [Header("Trail Settings")]
    [SerializeField] private float spawnInterval = 0.045f;
    [SerializeField] private float trailLifeTime = 0.18f;
    [SerializeField] private float backOffset = 0.35f;

    [Header("Wind Line Visual")]
    [SerializeField] private int lineCount = 4;
    [SerializeField] private float lineLength = 0.45f;
    [SerializeField] private float lineThickness = 0.045f;
    [SerializeField] private float lineSpacing = 0.12f;
    [SerializeField] private float startAlpha = 0.55f;
    [SerializeField] private Color lineColor = new Color(0.45f, 0.95f, 1f, 0.55f);
    [SerializeField] private int sortingOrderOffset = -1;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    private float spawnTimer;
    private static Sprite lineSprite;

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        FindReferences();
    }

    private void Update()
    {
        FindReferences();

        if (!ShouldShowTrail())
        {
            spawnTimer = 0f;
            return;
        }

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer = 0f;
            CreateWindTrail();
        }
    }

    private void FindReferences()
    {
        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement>();
        }

        if (playerSpriteRenderer == null)
        {
            playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (playerMovement == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                playerMovement = playerObject.GetComponent<PlayerMovement>();
            }
        }

        if (playerSpriteRenderer == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                playerSpriteRenderer = playerObject.GetComponentInChildren<SpriteRenderer>();
            }
        }
    }

    private bool ShouldShowTrail()
    {
        if (ChipSlotManager.Instance == null)
        {
            if (showDebugLog)
            {
                Debug.Log("SpeedTrail ¾È ³ª¿È: ChipSlotManager.Instance ¾øÀ½");
            }

            return false;
        }

        if (!ChipSlotManager.Instance.IsChipEquipped(ChipSlotManager.ChipType.MoveSpeed))
        {
            return false;
        }

        if (playerMovement == null)
        {
            if (showDebugLog)
            {
                Debug.Log("SpeedTrail ¾È ³ª¿È: PlayerMovement ¾øÀ½");
            }

            return false;
        }

        if (playerSpriteRenderer == null)
        {
            if (showDebugLog)
            {
                Debug.Log("SpeedTrail ¾È ³ª¿È: Player SpriteRenderer ¾øÀ½");
            }

            return false;
        }

        if (playerMovement.MoveInput.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        if (GameInputState.IsLocked)
        {
            return false;
        }

        return true;
    }

    private void CreateWindTrail()
    {
        Vector2 moveDirection = playerMovement.MoveInput.normalized;

        if (moveDirection == Vector2.zero)
        {
            return;
        }

        Vector2 backDirection = -moveDirection;
        Vector2 sideDirection = new Vector2(-moveDirection.y, moveDirection.x);

        Vector3 centerPosition = playerSpriteRenderer.transform.position + (Vector3)(backDirection * backOffset);

        GameObject groupObject = new GameObject("Speed_Wind_Trail");
        groupObject.transform.position = centerPosition;

        for (int i = 0; i < lineCount; i++)
        {
            float centerIndex = (lineCount - 1) * 0.5f;
            float sideOffset = (i - centerIndex) * lineSpacing;

            Vector3 linePosition = centerPosition + (Vector3)(sideDirection * sideOffset);
            linePosition += (Vector3)(backDirection * Random.Range(-0.05f, 0.08f));

            GameObject lineObject = new GameObject("Wind_Line");
            lineObject.transform.SetParent(groupObject.transform);
            lineObject.transform.position = linePosition;

            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            lineObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            SpriteRenderer lineRenderer = lineObject.AddComponent<SpriteRenderer>();
            lineRenderer.sprite = GetLineSprite();

            lineRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            lineRenderer.sortingOrder = playerSpriteRenderer.sortingOrder + sortingOrderOffset;

            Color color = lineColor;
            color.a = startAlpha * Random.Range(0.75f, 1f);
            lineRenderer.color = color;

            float randomLength = lineLength * Random.Range(0.75f, 1.15f);
            float randomThickness = lineThickness * Random.Range(0.8f, 1.15f);

            lineObject.transform.localScale = new Vector3(randomLength, randomThickness, 1f);
        }

        StartCoroutine(FadeAndDestroyWindTrail(groupObject));
    }

    private IEnumerator FadeAndDestroyWindTrail(GameObject groupObject)
    {
        float elapsed = 0f;

        SpriteRenderer[] renderers = groupObject.GetComponentsInChildren<SpriteRenderer>();
        Vector3 startScale = groupObject.transform.localScale;
        Vector3 endScale = startScale * 0.85f;

        while (elapsed < trailLifeTime)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / trailLifeTime);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    continue;
                }

                Color color = renderers[i].color;
                color.a = Mathf.Lerp(startAlpha, 0f, t);
                renderers[i].color = color;
            }

            if (groupObject != null)
            {
                groupObject.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            }

            yield return null;
        }

        if (groupObject != null)
        {
            Destroy(groupObject);
        }
    }

    private static Sprite GetLineSprite()
    {
        if (lineSprite != null)
        {
            return lineSprite;
        }

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        lineSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );

        return lineSprite;
    }
}