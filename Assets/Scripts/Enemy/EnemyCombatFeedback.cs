using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyHitKind
{
    Melee,
    Ranged,
    Explosion,
    Environment
}

[DisallowMultipleComponent]
public sealed class EnemyCombatFeedback : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private EnemyVisualMotion visualMotion;
    [SerializeField] private EnemyMotor motor;
    [SerializeField] private EnemyRole role;
    [SerializeField] private ParticleSystem hitParticles;

    [Header("피격 플래시")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField, Min(0.01f)] private float flashDuration = 0.045f;

    [Header("딱딱한 넉백 거리")]
    [SerializeField] private float rangedKnockbackDistance = 0.09f;
    [SerializeField] private float meleeKnockbackDistance = 0.22f;
    [SerializeField] private float explosionKnockbackDistance = 0.34f;
    [SerializeField] private float lethalMultiplier = 1.22f;

    private readonly List<SpriteRenderer> bodyRenderers = new List<SpriteRenderer>();
    private readonly List<Color> baseColors = new List<Color>();
    private Coroutine flashRoutine;

    private void Awake()
    {
        if (visualMotion == null)
            visualMotion = GetComponent<EnemyVisualMotion>();
        if (motor == null)
            motor = GetComponent<EnemyMotor>();
        if (role == null)
            role = GetComponent<EnemyRole>();
        if (hitParticles == null)
            hitParticles = GetComponentInChildren<ParticleSystem>(true);

        CacheBodyRenderers();
    }

    private void OnEnable()
    {
        if (bodyRenderers.Count == 0)
            CacheBodyRenderers();
    }

    public void PlayHit(Vector2 hitDirection, EnemyHitKind kind, bool lethal)
    {
        DamageContext context = DamageContext.Simple(1, hitDirection, kind, (Vector2)transform.position);
        PlayHit(context, lethal);
    }

    public void PlayHit(DamageContext context, bool lethal)
    {
        Vector2 direction = context.Direction.sqrMagnitude > 0.0001f ? context.Direction.normalized : Vector2.up;
        float tankVisualMultiplier = role != null && role.CurrentRole == EnemyRole.Role.Tank ? 0.72f : 1f;

        if (visualMotion != null)
            visualMotion.PlayHit(direction, tankVisualMultiplier * (lethal ? 1.25f : 1f));

        bool isTank = role != null && role.CurrentRole == EnemyRole.Role.Tank;
        if (motor != null && !(isTank && context.HitKind == EnemyHitKind.Ranged && !lethal))
        {
            float distance = GetKnockbackDistance(context.HitKind) + Mathf.Max(0f, context.KnockbackBonus);
            if (lethal)
                distance *= lethalMultiplier;
            float duration = context.HitKind == EnemyHitKind.Explosion ? 0.078f : context.HitKind == EnemyHitKind.Melee ? 0.060f : 0.048f;
            motor.ApplyKnockback(direction, distance, duration);
        }

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(lethal ? flashDuration * 1.25f : flashDuration));

        EmitParticles(direction, lethal, context.HitKind);
        ApplyCameraFeedback(direction, context.HitKind, lethal);
    }

    private float GetKnockbackDistance(EnemyHitKind kind)
    {
        switch (kind)
        {
            case EnemyHitKind.Ranged: return rangedKnockbackDistance;
            case EnemyHitKind.Explosion: return explosionKnockbackDistance;
            case EnemyHitKind.Environment: return rangedKnockbackDistance * 0.8f;
            default: return meleeKnockbackDistance;
        }
    }

    private void ApplyCameraFeedback(Vector2 hitDirection, EnemyHitKind kind, bool lethal)
    {
        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback == null && Camera.main != null)
            feedback = Camera.main.GetComponent<CameraFeedbackController>();
        if (feedback == null) return;

        bool tank = role != null && role.CurrentRole == EnemyRole.Role.Tank;
        CameraImpactLevelV11 level;
        bool hitStop;
        if (lethal && tank)
        {
            level = CameraImpactLevelV11.Heavy;
            hitStop = true;
        }
        else if (kind == EnemyHitKind.Explosion || kind == EnemyHitKind.Melee)
        {
            level = lethal ? CameraImpactLevelV11.Heavy : CameraImpactLevelV11.Medium;
            hitStop = true;
        }
        else
        {
            level = tank && lethal ? CameraImpactLevelV11.Medium : CameraImpactLevelV11.Small;
            hitStop = lethal;
        }
        feedback.Impact(level, hitDirection, hitStop);
    }

    private IEnumerator FlashRoutine(float duration)
    {
        if (bodyRenderers.Count == 0)
            CacheBodyRenderers();

        for (int i = 0; i < bodyRenderers.Count; i++)
        {
            SpriteRenderer renderer = bodyRenderers[i];
            if (renderer == null)
                continue;

            Color original = i < baseColors.Count ? baseColors[i] : renderer.color;
            Color flash = flashColor;
            flash.a = original.a; // 절대 투명하게 만들지 않습니다.
            renderer.color = flash;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, duration));

        for (int i = 0; i < bodyRenderers.Count; i++)
        {
            SpriteRenderer renderer = bodyRenderers[i];
            if (renderer == null)
                continue;

            Color original = i < baseColors.Count ? baseColors[i] : Color.white;
            if (original.a <= 0.001f)
                original.a = 1f;
            renderer.color = original;
        }

        flashRoutine = null;
    }

    private void EmitParticles(Vector2 hitDirection, bool lethal, EnemyHitKind kind)
    {
        if (hitParticles == null)
            return;

        hitParticles.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg);
        int count = lethal ? 12 : kind == EnemyHitKind.Explosion ? 10 : role != null && role.CurrentRole == EnemyRole.Role.Tank ? 7 : 5;
        hitParticles.Emit(count);
    }

    public void RefreshBaseColors()
    {
        CacheBodyRenderers();
    }

    private void CacheBodyRenderers()
    {
        bodyRenderers.Clear();
        baseColors.Clear();

        if (targetRenderer != null && IsBodyRenderer(targetRenderer))
            AddRenderer(targetRenderer);

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || bodyRenderers.Contains(renderer) || !IsBodyRenderer(renderer))
                continue;
            AddRenderer(renderer);
        }
    }

    private void AddRenderer(SpriteRenderer renderer)
    {
        bodyRenderers.Add(renderer);
        Color color = renderer.color;
        if (color.a <= 0.001f)
            color.a = 1f;
        baseColors.Add(color);
    }

    private static bool IsBodyRenderer(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return false;
        string lower = renderer.gameObject.name.ToLowerInvariant();
        if (lower.Contains("shadow") || lower.Contains("telegraph") || lower.Contains("particle") || lower.Contains("health"))
            return false;
        return renderer.enabled;
    }
}
