using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// V13 stage escalation for normal enemies.
/// Stage 1 keeps the base kits. Stage 2/3 make evolved patterns intentionally visible and
/// frequent enough that the player can immediately feel the stage prefix change.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageEnemyEvolutionV12 : MonoBehaviour
{
    [SerializeField, Min(1)] private int stageNumber = 1;
    [SerializeField] private EnemyRole role;

    private readonly List<SpriteRenderer> bodyRenderers = new List<SpriteRenderer>();
    private readonly List<Color> baseColors = new List<Color>();
    private readonly Dictionary<SpriteRenderer, Color> originalColorMap = new Dictionary<SpriteRenderer, Color>();
    private bool visualsApplied;
    private int meleeCalls;
    private int rangedCalls;
    private int tankSlamCalls;
    private int tankChargeCalls;
    private int bomberCalls;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static int DebugStageOverride { get; private set; }
    public static bool DebugForceEvolvedPatterns { get; private set; }
#endif

    public int StageNumber
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugStageOverride > 0) return DebugStageOverride;
#endif
            return Mathf.Max(1, stageNumber);
        }
    }
    public int PatternTier => Mathf.Max(0, StageNumber - 1);

    public void Configure(int stage)
    {
        stageNumber = Mathf.Max(1, stage);
        if (role == null) role = GetComponent<EnemyRole>();
        ApplyStageVisuals();
    }

    private void Awake()
    {
        if (role == null) role = GetComponent<EnemyRole>();
    }

    private void Start()
    {
        if (!visualsApplied) ApplyStageVisuals();
    }

    public void RefreshForDebugStage()
    {
        visualsApplied = false;
        ApplyStageVisuals();
    }

    private void ApplyStageVisuals()
    {
        visualsApplied = true;
        CacheBodyRenderers();
        int stage = StageNumber;

        Color multiplier = stage <= 1
            ? Color.white
            : stage == 2
                ? new Color(0.70f, 0.79f, 0.90f, 1f)
                : stage == 3
                    ? new Color(0.50f, 0.59f, 0.76f, 1f)
                    : new Color(0.42f, 0.49f, 0.69f, 1f);

        for (int i = 0; i < bodyRenderers.Count; i++)
        {
            SpriteRenderer renderer = bodyRenderers[i];
            if (renderer == null) continue;
            Color source = i < baseColors.Count ? baseColors[i] : Color.white;
            renderer.color = new Color(
                Mathf.Clamp01(source.r * multiplier.r),
                Mathf.Clamp01(source.g * multiplier.g),
                Mathf.Clamp01(source.b * multiplier.b),
                source.a);
        }

        EnemyCombatFeedback feedback = GetComponent<EnemyCombatFeedback>();
        if (feedback != null) feedback.RefreshBaseColors();
        EnemyHitEffect legacyHit = GetComponent<EnemyHitEffect>();
        if (legacyHit != null) legacyHit.RefreshOriginalColor();
    }

    private void CacheBodyRenderers()
    {
        bodyRenderers.Clear();
        baseColors.Clear();
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (!IsBodyRenderer(renderer)) continue;
            bodyRenderers.Add(renderer);
            if (!originalColorMap.TryGetValue(renderer, out Color original))
            {
                original = renderer.color;
                originalColorMap[renderer] = original;
            }
            baseColors.Add(original);
        }
    }

    private static bool IsBodyRenderer(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null) return false;
        string lower = renderer.gameObject.name.ToLowerInvariant();
        return !lower.Contains("shadow") && !lower.Contains("telegraph") &&
               !lower.Contains("outline") && !lower.Contains("particle") &&
               !lower.Contains("health") && !lower.Contains("spawn") &&
               !lower.Contains("warning");
    }

    public void ApplyEvolvedTelegraph(EnemyTelegraph telegraph, bool strongest = false)
    {
        if (telegraph == null) return;
        int stage = StageNumber;
        if (stage <= 1) { telegraph.ResetPalette(); return; }
        if (stage >= 3 || strongest)
            telegraph.SetPalette(new Color(0.52f, 0.03f, 0.72f, 0.20f), new Color(0.95f, 0.20f, 1f, 0.92f), new Color(0.45f, 0.85f, 1f, 0.72f));
        else
            telegraph.SetPalette(new Color(0.12f, 0.20f, 0.78f, 0.19f), new Color(0.26f, 0.74f, 1f, 0.90f), new Color(0.78f, 0.92f, 1f, 0.68f));
    }

    public void ResetTelegraph(EnemyTelegraph telegraph)
    {
        if (telegraph != null) telegraph.ResetPalette();
    }

    public int ChooseMeleeVariant()
    {
        int stage = StageNumber;
        if (stage < 2) return 0;
        meleeCalls++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugForceEvolvedPatterns) return stage >= 3 ? 2 : 1;
#endif
        if (stage == 2)
        {
            if (meleeCalls == 1) return 1; // stage change is immediately visible
            return Random.value < 0.70f ? 1 : 0;
        }
        if (meleeCalls == 1) return 2;
        if (meleeCalls == 2) return 1;
        float roll = Random.value;
        if (roll < 0.45f) return 2;
        if (roll < 0.84f) return 1;
        return 0;
    }

    public int ChooseRangedVariant()
    {
        int stage = StageNumber;
        if (stage < 2) return 0;
        rangedCalls++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugForceEvolvedPatterns) return stage >= 3 ? 2 : 1;
#endif
        if (stage == 2)
        {
            if (rangedCalls == 1) return 1;
            return Random.value < 0.72f ? 1 : 0;
        }
        if (rangedCalls == 1) return 2;
        if (rangedCalls == 2) return 1;
        float roll = Random.value;
        if (roll < 0.46f) return 2;
        if (roll < 0.85f) return 1;
        return 0;
    }

    public bool ShouldTankAftershock()
    {
        int stage = StageNumber;
        if (stage < 2) return false;
        tankSlamCalls++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugForceEvolvedPatterns) return true;
#endif
        if (tankSlamCalls == 1) return true;
        return Random.value < (stage >= 3 ? 0.82f : 0.70f);
    }

    public bool ShouldTankDoubleCharge()
    {
        int stage = StageNumber;
        if (stage < 3) return false;
        tankChargeCalls++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugForceEvolvedPatterns) return true;
#endif
        if (tankChargeCalls == 1) return true;
        return Random.value < 0.64f;
    }

    public void TrySpawnBomberEvolution(Vector2 origin, float sourceRadius, int damage, Vector2 referenceDirection)
    {
        int stage = StageNumber;
        if (stage < 2) return;
        bomberCalls++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DebugForceEvolvedPatterns)
        {
            SpawnSplit(origin, sourceRadius, damage, referenceDirection);
            return;
        }
#endif
        if (stage >= 3 && (bomberCalls == 1 || Random.value < 0.52f))
        {
            SpawnSplit(origin, sourceRadius, damage, referenceDirection);
            return;
        }
        if ((stage == 2 && bomberCalls == 1) || Random.value < (stage >= 3 ? 0.82f : 0.72f))
            EvolutionBlastV12.Create(origin, sourceRadius * 0.72f, Mathf.Max(1, damage), 0.48f, stage);
    }

    private void SpawnSplit(Vector2 origin, float sourceRadius, int damage, Vector2 referenceDirection)
    {
        Vector2 dir = referenceDirection.sqrMagnitude > 0.001f ? referenceDirection.normalized : Vector2.up;
        Vector2 perpendicular = new Vector2(-dir.y, dir.x);
        EvolutionBlastV12.Create(origin + perpendicular * 0.86f, sourceRadius * 0.56f, Mathf.Max(1, damage), 0.46f, StageNumber);
        EvolutionBlastV12.Create(origin - perpendicular * 0.86f, sourceRadius * 0.56f, Mathf.Max(1, damage), 0.60f, StageNumber);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void DebugSetStageOverride(int stage)
    {
        DebugStageOverride = Mathf.Clamp(stage, 0, 9);
        StageEnemyEvolutionV12[] all = Object.FindObjectsByType<StageEnemyEvolutionV12>(FindObjectsInactive.Exclude);
        for (int i = 0; i < all.Length; i++) all[i].RefreshForDebugStage();
    }

    public static void DebugToggleForceEvolved()
    {
        DebugForceEvolvedPatterns = !DebugForceEvolvedPatterns;
    }

    public static void DebugResetOverrides()
    {
        DebugStageOverride = 0;
        DebugForceEvolvedPatterns = false;
        StageEnemyEvolutionV12[] all = Object.FindObjectsByType<StageEnemyEvolutionV12>(FindObjectsInactive.Exclude);
        for (int i = 0; i < all.Length; i++) all[i].RefreshForDebugStage();
    }
#endif
}

internal sealed class EvolutionBlastV12 : MonoBehaviour
{
    private float radius;
    private int damage;
    private float delay;
    private int stage;
    private EnemyTelegraph telegraph;

    public static void Create(Vector2 position, float radius, int damage, float delay, int stageNumber = 2)
    {
        GameObject root = new GameObject("EnemyEvolution_EchoBlast");
        root.transform.position = position;
        EvolutionBlastV12 blast = root.AddComponent<EvolutionBlastV12>();
        blast.radius = Mathf.Max(0.25f, radius);
        blast.damage = Mathf.Max(1, damage);
        blast.delay = Mathf.Max(0.18f, delay);
        blast.stage = Mathf.Max(2, stageNumber);
        GameObject warning = new GameObject("Telegraph");
        warning.transform.SetParent(root.transform, false);
        blast.telegraph = warning.AddComponent<EnemyTelegraph>();
        blast.telegraph.SetPalette(
            blast.stage >= 3 ? new Color(0.50f, 0.02f, 0.70f, 0.20f) : new Color(0.10f, 0.20f, 0.75f, 0.18f),
            blast.stage >= 3 ? new Color(0.95f, 0.20f, 1f, 0.92f) : new Color(0.25f, 0.75f, 1f, 0.90f),
            new Color(0.80f, 0.95f, 1f, 0.72f));
        blast.StartCoroutine(blast.Run());
    }

    private IEnumerator Run()
    {
        if (telegraph != null) telegraph.ShowCircle(radius);
        float elapsed = 0f;
        while (elapsed < delay)
        {
            if (GameInputState.IsLocked) { yield return null; continue; }
            elapsed += Time.deltaTime;
            if (telegraph != null) telegraph.SetProgress(elapsed / Mathf.Max(0.01f, delay));
            yield return null;
        }
        if (telegraph != null) telegraph.Hide();
        ResolvePlayerDamage();
        ExecutorExplosionAnimator.Create(null, transform.position, radius, false);
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null) feedback.Shake(0.08f, 0.06f, Vector2.zero);
        Destroy(gameObject);
    }

    private void ResolvePlayerDamage()
    {
        PlayerHealth player = FindAnyObjectByType<PlayerHealth>();
        if (player == null || player.IsDead) return;
        Collider2D body = PlayerDamageQuery.ResolveBodyCollider(player);
        Vector2 point = player.transform.position;
        if (body != null) point = body.ClosestPoint(transform.position);
        if (Vector2.Distance(point, transform.position) <= radius) player.TakeDamage(damage);
    }
}
