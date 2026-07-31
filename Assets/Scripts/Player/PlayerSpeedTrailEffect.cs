using System.Collections;
using UnityEngine;

public class PlayerSpeedTrailEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    [Header("Trail Condition")]
    [SerializeField] private bool onlyWhenMoveSpeedChipEquipped = true;

    [Header("Trail Position")]
    [SerializeField] private float backOffset = 0.28f;
    [SerializeField] private float verticalOffset = 0f;

    [Header("Trail Visual")]
    [SerializeField] private float spawnInterval = 0.045f;
    [SerializeField] private float trailLifeTime = 0.18f;
    [SerializeField] private int lineCount = 4;
    [SerializeField] private float lineLength = 0.38f;
    [SerializeField] private float lineThickness = 0.035f;
    [SerializeField] private float lineSpacing = 0.1f;
    [SerializeField] private float startAlpha = 0.55f;
    [SerializeField] private Color lineColor = new Color(0.4f, 0.9f, 1f, 0.55f);

    [Header("Sorting")]
    [SerializeField] private int sortingOrderOffset = -1;

    private float spawnTimer;
    private static Sprite lineSprite;

    private void Awake()
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
            CreateTrail();
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
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sprite != null)
                {
                    playerSpriteRenderer = renderers[i];
                    break;
                }
            }
        }
    }

    private bool ShouldShowTrail()
    {
        if (playerMovement == null)
        {
            return false;
        }

        if (playerSpriteRenderer == null)
        {
            return false;
        }

        if (GameInputState.IsLocked)
        {
            return false;
        }

        if (playerMovement.MoveInput.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        if (onlyWhenMoveSpeedChipEquipped)
        {
            if (ChipSlotManager.Instance == null)
            {
                return false;
            }

            if (!ChipSlotManager.Instance.IsChipEquipped(ChipSlotManager.ChipType.MoveSpeed))
            {
                return false;
            }
        }

        return true;
    }

    private void CreateTrail()
    {
        Vector2 moveDirection = playerMovement.MoveInput.normalized;

        if (moveDirection == Vector2.zero)
        {
            return;
        }

        Vector2 backDirection = -moveDirection;
        Vector2 sideDirection = new Vector2(-moveDirection.y, moveDirection.x);

        Vector3 basePosition = playerSpriteRenderer.bounds.center;
        basePosition += (Vector3)(backDirection * backOffset);
        basePosition += new Vector3(0f, verticalOffset, 0f);

        GameObject groupObject = new GameObject("Lightning_Afterimage_Trail");
        groupObject.transform.position = basePosition;

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;

        for (int i = 0; i < lineCount; i++)
        {
            float centerIndex = (lineCount - 1) * 0.5f;
            float sideOffset = (i - centerIndex) * lineSpacing;

            Vector3 linePosition = basePosition;
            linePosition += (Vector3)(sideDirection * sideOffset);
            linePosition += (Vector3)(backDirection * Random.Range(-0.04f, 0.08f));

            GameObject lineObject = new GameObject("Lightning_Line");
            lineObject.transform.SetParent(groupObject.transform);
            lineObject.transform.position = linePosition;
            lineObject.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            SpriteRenderer sr = lineObject.AddComponent<SpriteRenderer>();
            sr.sprite = GetLineSprite();

            sr.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            sr.sortingOrder = playerSpriteRenderer.sortingOrder + sortingOrderOffset;

            Color color = lineColor;
            color.a = startAlpha * Random.Range(0.75f, 1f);
            sr.color = color;

            float randomLength = lineLength * Random.Range(0.75f, 1.2f);
            float randomThickness = lineThickness * Random.Range(0.8f, 1.2f);

            lineObject.transform.localScale = new Vector3(randomLength, randomThickness, 1f);
        }

        StartCoroutine(FadeAndDestroy(groupObject));
    }

    private IEnumerator FadeAndDestroy(GameObject groupObject)
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