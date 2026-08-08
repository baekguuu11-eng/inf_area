using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeathSequenceUI : MonoBehaviour
{
    [Header("Main Panel")]
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Intro Background")]
    [SerializeField] private Image dimOverlayImage;
    [Range(0f, 1f)]
    [SerializeField] private float dimOverlayAlpha = 0.65f;

    [Header("Final Death Screen")]
    [SerializeField] private Image deathBackgroundImage;
    [SerializeField] private CanvasGroup finalScreenGroup;
    [Range(0f, 1f)]
    [SerializeField] private float finalBackgroundAlpha = 1f;

    [Header("Dead Player")]
    [SerializeField] private Image deadPlayerImage;
    [SerializeField] private RectTransform deadPlayerRect;

    [Header("Shard Effect")]
    [SerializeField] private RectTransform shardRoot;
    [SerializeField] private int shardColumns = 7;
    [SerializeField] private int shardRows = 5;
    [SerializeField] private float shardSize = 18f;
    [SerializeField] private float shardDuration = 1.3f;
    [SerializeField] private float shardSpread = 360f;
    [SerializeField] private float shardFallDistance = 160f;
    [SerializeField] private float shardRotationSpeed = 420f;

    [Header("Break Effect")]
    [SerializeField] private Image breakFlash;

    [Header("Buttons")]
    [SerializeField] private CanvasGroup restartButtonGroup;
    [SerializeField] private CanvasGroup mainMenuButtonGroup;

    [Header("Timing")]
    [SerializeField] private float dimFadeTime = 0.25f;
    [SerializeField] private float playerZoomTime = 1.2f;
    [SerializeField] private float breakFlashTime = 0.3f;
    [SerializeField] private float delayAfterShatter = 0.35f;
    [SerializeField] private float finalScreenFadeTime = 0.45f;
    [SerializeField] private float buttonFadeTime = 0.3f;

    [Header("Player Zoom")]
    [SerializeField] private float startScale = 0.8f;
    [SerializeField] private float endScale = 3.2f;
    [SerializeField] private float zoomShakeAmount = 6f;
    [SerializeField] private float breakShakeAmount = 15f;

    [Header("Hide UI On Death")]
    [SerializeField] private GameObject[] hideOnDeath;

    [Header("Shard Colors")]
    [SerializeField] private Color shardMainColor = new Color(0.25f, 0.75f, 1f, 1f);
    [SerializeField] private Color shardDarkColor = new Color(0.05f, 0.25f, 0.55f, 1f);
    [SerializeField] private Color shardLightColor = new Color(0.75f, 0.95f, 1f, 1f);

    private Coroutine sequenceCoroutine;
    private static Sprite squareSprite;

    private class ShardData
    {
        public RectTransform rectTransform;
        public Image image;
        public Vector2 startPosition;
        public Vector2 endPosition;
        public float startRotation;
        public float endRotation;
        public Vector3 startScale;
        public Vector3 endScale;
    }

    private void Awake()
    {
        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (deadPlayerRect == null && deadPlayerImage != null)
        {
            deadPlayerRect = deadPlayerImage.GetComponent<RectTransform>();
        }

        if (finalScreenGroup == null && deathBackgroundImage != null)
        {
            finalScreenGroup = deathBackgroundImage.GetComponent<CanvasGroup>();

            if (finalScreenGroup == null)
            {
                finalScreenGroup = deathBackgroundImage.gameObject.AddComponent<CanvasGroup>();
            }
        }

        SetInitialState();
    }

    private void OnEnable()
    {
        SetInitialState();
    }

    private void SetInitialState()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        if (dimOverlayImage != null)
        {
            Color color = dimOverlayImage.color;
            color.a = 0f;
            dimOverlayImage.color = color;
            dimOverlayImage.raycastTarget = false;
            dimOverlayImage.gameObject.SetActive(true);
        }

        if (deathBackgroundImage != null)
        {
            Color color = deathBackgroundImage.color;
            color.a = finalBackgroundAlpha;
            deathBackgroundImage.color = color;
            deathBackgroundImage.raycastTarget = false;
        }

        if (finalScreenGroup != null)
        {
            finalScreenGroup.alpha = 0f;
            finalScreenGroup.interactable = false;
            finalScreenGroup.blocksRaycasts = false;
        }

        if (deadPlayerImage != null)
        {
            Color color = deadPlayerImage.color;
            color.a = 0f;
            deadPlayerImage.color = color;
            deadPlayerImage.raycastTarget = false;
            deadPlayerImage.gameObject.SetActive(true);
        }

        if (deadPlayerRect != null)
        {
            deadPlayerRect.anchoredPosition = Vector2.zero;
            deadPlayerRect.localScale = Vector3.one * startScale;
            deadPlayerRect.rotation = Quaternion.identity;
        }

        if (breakFlash != null)
        {
            Color color = breakFlash.color;
            color.a = 0f;
            breakFlash.color = color;
            breakFlash.raycastTarget = false;
        }

        if (shardRoot != null)
        {
            ClearShardRoot();
            shardRoot.anchoredPosition = Vector2.zero;
            shardRoot.localScale = Vector3.one;
        }

        HideButtonGroup(restartButtonGroup);
        HideButtonGroup(mainMenuButtonGroup);
    }

    public void PlayDeathSequence(SpriteRenderer playerSpriteRenderer)
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        HideGameplayUI();
        HideHeartImagesDirectly();

        if (playerSpriteRenderer != null && deadPlayerImage != null)
        {
            deadPlayerImage.sprite = playerSpriteRenderer.sprite;
            deadPlayerImage.preserveAspect = true;
        }

        sequenceCoroutine = StartCoroutine(DeathSequenceRoutine());
    }

    private IEnumerator DeathSequenceRoutine()
    {
        Time.timeScale = 0f;

        yield return FadeInDimOverlay();
        yield return ZoomPlayerToCenter();
        yield return BreakFlashRoutine();
        yield return ShatterPlayerRoutine();

        yield return new WaitForSecondsRealtime(delayAfterShatter);

        yield return ShowFinalDeathScreen();
        yield return ShowButtons();

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }

        sequenceCoroutine = null;
    }

    private IEnumerator FadeInDimOverlay()
    {
        float elapsed = 0f;

        while (elapsed < dimFadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dimFadeTime);

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = t;
            }

            if (dimOverlayImage != null)
            {
                Color color = dimOverlayImage.color;
                color.a = Mathf.Lerp(0f, dimOverlayAlpha, t);
                dimOverlayImage.color = color;
            }

            if (deadPlayerImage != null)
            {
                Color color = deadPlayerImage.color;
                color.a = t;
                deadPlayerImage.color = color;
            }

            yield return null;
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
        }

        if (dimOverlayImage != null)
        {
            Color color = dimOverlayImage.color;
            color.a = dimOverlayAlpha;
            dimOverlayImage.color = color;
        }

        if (deadPlayerImage != null)
        {
            Color color = deadPlayerImage.color;
            color.a = 1f;
            deadPlayerImage.color = color;
        }
    }

    private IEnumerator ZoomPlayerToCenter()
    {
        if (deadPlayerRect == null)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < playerZoomTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / playerZoomTime);
            float eased = EaseOutCubic(t);

            float scale = Mathf.Lerp(startScale, endScale, eased);
            Vector2 shake = Random.insideUnitCircle * zoomShakeAmount * t;

            deadPlayerRect.localScale = Vector3.one * scale;
            deadPlayerRect.anchoredPosition = shake;

            yield return null;
        }

        deadPlayerRect.localScale = Vector3.one * endScale;
        deadPlayerRect.anchoredPosition = Vector2.zero;
    }

    private IEnumerator BreakFlashRoutine()
    {
        float elapsed = 0f;

        while (elapsed < breakFlashTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / breakFlashTime);

            if (deadPlayerRect != null)
            {
                deadPlayerRect.anchoredPosition = Random.insideUnitCircle * breakShakeAmount;
            }

            if (breakFlash != null)
            {
                Color color = breakFlash.color;
                color.a = Mathf.Sin(t * Mathf.PI) * 0.65f;
                breakFlash.color = color;
            }

            yield return null;
        }

        if (deadPlayerRect != null)
        {
            deadPlayerRect.anchoredPosition = Vector2.zero;
        }

        if (breakFlash != null)
        {
            Color color = breakFlash.color;
            color.a = 0f;
            breakFlash.color = color;
        }
    }

    private IEnumerator ShatterPlayerRoutine()
    {
        if (deadPlayerImage == null || deadPlayerRect == null || shardRoot == null)
        {
            yield break;
        }

        List<ShardData> shards = CreatePlayerShards();

        Color playerColor = deadPlayerImage.color;
        playerColor.a = 0f;
        deadPlayerImage.color = playerColor;

        float elapsed = 0f;

        while (elapsed < shardDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / shardDuration);
            float eased = EaseOutCubic(t);

            for (int i = 0; i < shards.Count; i++)
            {
                ShardData shard = shards[i];

                if (shard == null || shard.rectTransform == null || shard.image == null)
                {
                    continue;
                }

                shard.rectTransform.anchoredPosition = Vector2.Lerp(shard.startPosition, shard.endPosition, eased);
                shard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(shard.startRotation, shard.endRotation, eased));
                shard.rectTransform.localScale = Vector3.Lerp(shard.startScale, shard.endScale, eased);

                Color color = shard.image.color;
                color.a = Mathf.Lerp(1f, 0f, t);
                shard.image.color = color;
            }

            yield return null;
        }

        ClearShardRoot();
    }

    private IEnumerator ShowFinalDeathScreen()
    {
        float elapsed = 0f;

        while (elapsed < finalScreenFadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / finalScreenFadeTime);

            if (finalScreenGroup != null)
            {
                finalScreenGroup.alpha = t;
            }

            if (dimOverlayImage != null)
            {
                Color color = dimOverlayImage.color;
                color.a = Mathf.Lerp(dimOverlayAlpha, 0f, t);
                dimOverlayImage.color = color;
            }

            yield return null;
        }

        if (finalScreenGroup != null)
        {
            finalScreenGroup.alpha = 1f;
        }

        if (dimOverlayImage != null)
        {
            Color color = dimOverlayImage.color;
            color.a = 0f;
            dimOverlayImage.color = color;
        }
    }

    private List<ShardData> CreatePlayerShards()
    {
        ClearShardRoot();

        List<ShardData> shards = new List<ShardData>();

        float visualWidth = deadPlayerRect.rect.width * deadPlayerRect.localScale.x;
        float visualHeight = deadPlayerRect.rect.height * deadPlayerRect.localScale.y;

        float totalWidth = shardColumns * shardSize;
        float totalHeight = shardRows * shardSize;

        for (int y = 0; y < shardRows; y++)
        {
            for (int x = 0; x < shardColumns; x++)
            {
                float centerX = (x - (shardColumns - 1) * 0.5f) * shardSize;
                float centerY = (y - (shardRows - 1) * 0.5f) * shardSize;

                float ellipseX = centerX / (totalWidth * 0.5f);
                float ellipseY = centerY / (totalHeight * 0.5f);

                if ((ellipseX * ellipseX) + (ellipseY * ellipseY) > 1.05f)
                {
                    continue;
                }

                GameObject shardObject = new GameObject("Player_Death_Shard");
                shardObject.transform.SetParent(shardRoot, false);

                Image shardImage = shardObject.AddComponent<Image>();
                shardImage.sprite = GetSquareSprite();
                shardImage.raycastTarget = false;
                shardImage.color = GetRandomShardColor();

                RectTransform rect = shardObject.GetComponent<RectTransform>();
                rect.sizeDelta = Vector2.one * shardSize;

                Vector2 startPosition = new Vector2(
                    centerX * (visualWidth / Mathf.Max(totalWidth, 1f)),
                    centerY * (visualHeight / Mathf.Max(totalHeight, 1f))
                );

                Vector2 direction = startPosition.normalized;

                if (direction.sqrMagnitude <= 0.01f)
                {
                    direction = Random.insideUnitCircle.normalized;
                }

                Vector2 randomPush = Random.insideUnitCircle * 80f;
                Vector2 endPosition = startPosition + direction * Random.Range(shardSpread * 0.45f, shardSpread);
                endPosition += randomPush;
                endPosition.y -= Random.Range(40f, shardFallDistance);

                rect.anchoredPosition = startPosition;
                rect.localScale = Vector3.one * Random.Range(0.85f, 1.25f);

                ShardData shard = new ShardData();
                shard.rectTransform = rect;
                shard.image = shardImage;
                shard.startPosition = startPosition;
                shard.endPosition = endPosition;
                shard.startRotation = Random.Range(-25f, 25f);
                shard.endRotation = Random.Range(-shardRotationSpeed, shardRotationSpeed);
                shard.startScale = rect.localScale;
                shard.endScale = Vector3.one * Random.Range(0.25f, 0.55f);

                shards.Add(shard);
            }
        }

        return shards;
    }

    private Color GetRandomShardColor()
    {
        float value = Random.value;

        if (value < 0.25f)
        {
            return shardLightColor;
        }

        if (value < 0.55f)
        {
            return shardMainColor;
        }

        return shardDarkColor;
    }

    private void ClearShardRoot()
    {
        if (shardRoot == null)
        {
            return;
        }

        for (int i = shardRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(shardRoot.GetChild(i).gameObject);
        }
    }

    private void HideGameplayUI()
    {
        if (hideOnDeath == null)
        {
            return;
        }

        for (int i = 0; i < hideOnDeath.Length; i++)
        {
            if (hideOnDeath[i] != null)
            {
                hideOnDeath[i].SetActive(false);
            }
        }
    }

    private void HideHeartImagesDirectly()
    {
#if UNITY_2023_1_OR_NEWER
        HeartUI[] heartUIs = FindObjectsByType<HeartUI>(FindObjectsInactive.Include);
#else
        HeartUI[] heartUIs = FindObjectsOfType<HeartUI>(true);
#endif

        for (int i = 0; i < heartUIs.Length; i++)
        {
            if (heartUIs[i] == null)
            {
                continue;
            }

            HideImageArray(heartUIs[i].hearts);
            HideImageArray(heartUIs[i].bonusHearts);
        }
    }

    private void HideImageArray(Image[] images)
    {
        if (images == null)
        {
            return;
        }

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null)
            {
                images[i].gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator ShowButtons()
    {
        float elapsed = 0f;

        while (elapsed < buttonFadeTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / buttonFadeTime);

            ShowButtonGroup(restartButtonGroup, t);
            ShowButtonGroup(mainMenuButtonGroup, t);

            yield return null;
        }

        ShowButtonGroup(restartButtonGroup, 1f);
        ShowButtonGroup(mainMenuButtonGroup, 1f);
    }

    private void HideButtonGroup(CanvasGroup group)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void ShowButtonGroup(CanvasGroup group, float alpha)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = alpha;
        group.interactable = alpha >= 1f;
        group.blocksRaycasts = alpha >= 1f;
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null)
        {
            return squareSprite;
        }

        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        squareSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );

        return squareSprite;
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}