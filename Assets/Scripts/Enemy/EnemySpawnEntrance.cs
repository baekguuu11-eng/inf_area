using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class EnemySpawnEntrance : MonoBehaviour
{
    private const int MinimumBodySortingOrder = 3;
    [Header("Timing")]
    [SerializeField, Min(0f)] private float startDelay = 0f;
    [SerializeField, Min(0.05f)] private float duration = 0.48f;

    [Header("Visual")]
    [SerializeField] private EnemyVisualMotion visualMotion;
    [SerializeField] private Color cueColor = new Color(0.1f, 0.95f, 1f, 0.72f);
    [SerializeField] private float cueWorldSize = 0.72f;
    [SerializeField] private int cueSortingOrder = 14;

    private readonly List<MonoBehaviour> disabledBehaviours = new List<MonoBehaviour>();
    private readonly List<Collider2D> disabledColliders = new List<Collider2D>();
    private readonly List<SpriteRenderer> bodyRenderers = new List<SpriteRenderer>();
    private readonly List<Color> originalColors = new List<Color>();
    private readonly List<bool> originalEnabledStates = new List<bool>();

    private RoomController ownerRoom;
    private GameObject cueRoot;
    private bool initialized;
    private bool completed;

    private static Sprite ringSprite;
    private static Sprite scanSprite;

    public void Configure(float delay, float entranceDuration, RoomController room)
    {
        startDelay = Mathf.Max(0f, delay);
        duration = Mathf.Max(0.05f, entranceDuration);
        ownerRoom = room != null ? room : GetComponentInParent<RoomController>();
    }

    private void Awake()
    {
        ownerRoom = GetComponentInParent<RoomController>();
        if (visualMotion == null)
            visualMotion = GetComponent<EnemyVisualMotion>();

        CacheAndDisableGameplay();
        CacheBodyRenderers();
        CreateCue();
        initialized = true;
    }

    public void RefreshStageColorsFromCurrent()
    {
        int count = Mathf.Min(bodyRenderers.Count, originalColors.Count);
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = bodyRenderers[i];
            if (renderer == null) continue;
            Color staged = renderer.color;
            Color original = originalColors[i];
            original.r = staged.r;
            original.g = staged.g;
            original.b = staged.b;
            originalColors[i] = original;
        }
    }

    private IEnumerator Start()
    {
        if (!initialized)
            yield break;

        float delayElapsed = 0f;
        while (delayElapsed < startDelay)
        {
            delayElapsed += Time.unscaledDeltaTime;
            AnimateCue(delayElapsed / Mathf.Max(0.001f, startDelay), true);
            yield return null;
        }

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            for (int i = 0; i < bodyRenderers.Count; i++)
            {
                SpriteRenderer renderer = bodyRenderers[i];
                if (renderer == null)
                    continue;

                renderer.enabled = originalEnabledStates[i];
                Color color = originalColors[i];
                color.a *= eased;
                renderer.color = color;
            }

            if (visualMotion != null)
                visualMotion.SetSpawnProgress(eased);

            AnimateCue(t, false);
            yield return null;
        }

        CompleteEntrance();
    }

    private void CacheAndDisableGameplay()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this || behaviour is EnemyVisualMotion ||
                behaviour is EnemyCombatFeedback || behaviour is EnemyTelegraph || behaviour is EnemyHealth)
                continue;

            string typeName = behaviour.GetType().Name;
            bool shouldDisable = typeName.EndsWith("AI") || typeName == "EnemyDamage" ||
                                 typeName == "EnemyMotor" || typeName == "EnemyPerception";
            if (shouldDisable && behaviour.enabled)
            {
                disabledBehaviours.Add(behaviour);
                behaviour.enabled = false;
            }
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider != null && collider.enabled)
            {
                disabledColliders.Add(collider);
                collider.enabled = false;
            }
        }
    }

    private void CacheBodyRenderers()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || IsExcludedRenderer(renderer))
                continue;

            bodyRenderers.Add(renderer);
            originalColors.Add(renderer.color);
            originalEnabledStates.Add(renderer.enabled);

            if (IsPrimaryBodyRenderer(renderer) && renderer.sortingOrder < MinimumBodySortingOrder)
                renderer.sortingOrder = MinimumBodySortingOrder;

            if (renderer.enabled)
            {
                Color faded = renderer.color;
                faded.a = 0f;
                renderer.color = faded;
            }
        }

        if (visualMotion != null)
            visualMotion.SetSpawnProgress(0f);
    }

    private bool IsExcludedRenderer(SpriteRenderer renderer)
    {
        string lower = renderer.gameObject.name.ToLowerInvariant();
        return lower.Contains("shadow") || lower.Contains("telegraph") ||
               lower.Contains("warning") || lower.Contains("spawncue");
    }

    private void CreateCue()
    {
        cueRoot = new GameObject("SpawnCue_Runtime");
        cueRoot.transform.SetParent(transform, false);
        cueRoot.transform.localPosition = Vector3.zero;

        GameObject ring = new GameObject("Ring");
        ring.transform.SetParent(cueRoot.transform, false);
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = GetRingSprite();
        ringRenderer.color = cueColor;
        ringRenderer.sortingOrder = cueSortingOrder;
        SetRendererWorldSize(ringRenderer, new Vector2(cueWorldSize, cueWorldSize));

        GameObject scan = new GameObject("Scan");
        scan.transform.SetParent(cueRoot.transform, false);
        SpriteRenderer scanRenderer = scan.AddComponent<SpriteRenderer>();
        scanRenderer.sprite = GetScanSprite();
        Color scanColor = cueColor;
        scanColor.a *= 0.55f;
        scanRenderer.color = scanColor;
        scanRenderer.sortingOrder = cueSortingOrder + 1;
        SetRendererWorldSize(scanRenderer, new Vector2(cueWorldSize * 0.82f, cueWorldSize * 0.10f));
    }

    private void AnimateCue(float progress, bool waiting)
    {
        if (cueRoot == null)
            return;

        float time = Time.unscaledTime;
        float pulse = waiting ? 0.82f + Mathf.Sin(time * 7f) * 0.06f : Mathf.Lerp(1f, 0.68f, progress);
        cueRoot.transform.localScale = Vector3.one * pulse;

        Transform scan = cueRoot.transform.Find("Scan");
        if (scan != null)
        {
            float y = Mathf.Lerp(-cueWorldSize * 0.28f, cueWorldSize * 0.28f,
                (Mathf.Sin(time * 9f) + 1f) * 0.5f);
            scan.localPosition = new Vector3(0f, y, 0f);
        }
    }

    private void CompleteEntrance()
    {
        if (completed)
            return;
        completed = true;

        for (int i = 0; i < bodyRenderers.Count; i++)
        {
            SpriteRenderer renderer = bodyRenderers[i];
            if (renderer == null)
                continue;
            renderer.color = originalColors[i];
            renderer.enabled = originalEnabledStates[i];
        }

        if (visualMotion != null)
        {
            visualMotion.SetSpawnProgress(1f);
            visualMotion.SetAction(EnemyVisualMotion.ActionState.None, Vector2.down);
        }

        EnsurePrimaryBodyVisible();

        for (int i = 0; i < disabledColliders.Count; i++)
            if (disabledColliders[i] != null) disabledColliders[i].enabled = true;

        for (int i = 0; i < disabledBehaviours.Count; i++)
            if (disabledBehaviours[i] != null) disabledBehaviours[i].enabled = true;

        if (cueRoot != null)
            Destroy(cueRoot);
    }

    private static bool IsPrimaryBodyRenderer(SpriteRenderer renderer)
    {
        if (renderer == null)
            return false;

        string lower = renderer.gameObject.name.ToLowerInvariant();
        return lower == "bodyvisual" || lower.Contains("bodyvisual");
    }

    private void EnsurePrimaryBodyVisible()
    {
        Transform body = transform.Find("VisualRoot/BodyVisual");
        SpriteRenderer renderer = body != null ? body.GetComponent<SpriteRenderer>() : null;

        if (renderer == null)
        {
            for (int i = 0; i < bodyRenderers.Count; i++)
            {
                if (IsPrimaryBodyRenderer(bodyRenderers[i]))
                {
                    renderer = bodyRenderers[i];
                    break;
                }
            }
        }

        if (renderer == null)
            return;

        renderer.enabled = true;
        renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, MinimumBodySortingOrder);

        Color color = renderer.color;
        if (color.a <= 0.001f)
            color.a = 1f;
        renderer.color = color;

        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Update(0f);
        }
    }

    private void OnDisable()
    {
        if (initialized && !completed)
            CompleteEntrance();
    }

    private static void SetRendererWorldSize(SpriteRenderer renderer, Vector2 worldSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Vector2 spriteSize = renderer.sprite.bounds.size;
        Vector3 parentScale = renderer.transform.parent != null ? renderer.transform.parent.lossyScale : Vector3.one;
        renderer.transform.localScale = new Vector3(
            worldSize.x / Mathf.Max(0.0001f, spriteSize.x * Mathf.Abs(parentScale.x)),
            worldSize.y / Mathf.Max(0.0001f, spriteSize.y * Mathf.Abs(parentScale.y)),
            1f);
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null)
            return ringSprite;

        const int size = 48;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outer = 21f;
        float inner = 18f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                bool ring = distance <= outer && distance >= inner;
                bool tick = false;
                if (distance >= inner - 1f && distance <= outer + 1f)
                {
                    float angle = Mathf.Atan2(y - center.y, x - center.x) * Mathf.Rad2Deg;
                    float mod = Mathf.Repeat(angle + 360f, 45f);
                    tick = mod < 5f || mod > 40f;
                }
                texture.SetPixel(x, y, ring || tick ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        ringSprite.name = "EnemySpawnCueRing";
        return ringSprite;
    }

    private static Sprite GetScanSprite()
    {
        if (scanSprite != null)
            return scanSprite;

        const int width = 32;
        const int height = 4;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, Color.white);
        texture.Apply();
        scanSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
        scanSprite.name = "EnemySpawnCueScan";
        return scanSprite;
    }
}
