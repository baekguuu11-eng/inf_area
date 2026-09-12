using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a normal enemy instance as a Chernobyl core guard.
/// Keeps the original enemy kit while applying Chernobyl visuals, no-loot rules,
/// green telegraphs/projectiles and a player-only death burst.
/// </summary>
[DisallowMultipleComponent]
public sealed class ChernobylGuardUnit : MonoBehaviour
{
    private readonly List<SpriteRenderer> bodyRenderers = new List<SpriteRenderer>();
    private ChernobylGuardDirector director;
    private ChernobylBossController boss;
    private EnemyType enemyType;
    private int page;
    private bool deathBurstScheduled;
    private bool suppressDeathDamage;
    private Color guardColor;

    public EnemyType GuardType => enemyType;
    public Color ProjectileColor => new Color(0.40f, 1.18f, 0.20f, 1f);

    public void Configure(ChernobylGuardDirector owner, ChernobylBossController ownerBoss, EnemyType type, int bossPage)
    {
        director = owner;
        boss = ownerBoss;
        enemyType = type;
        page = Mathf.Clamp(bossPage, 1, 3);
        guardColor = page == 1
            ? new Color(0.68f, 1f, 0.56f, 1f)
            : page == 2 ? new Color(0.50f, 1f, 0.34f, 1f)
                        : new Color(0.38f, 1f, 0.20f, 1f);

        ByteDropper byteDropper = GetComponentInChildren<ByteDropper>(true);
        if (byteDropper != null) byteDropper.ConfigureDrops(0, 0, 1);

        StageEnemyEvolutionV12 evolution = GetComponent<StageEnemyEvolutionV12>();
        if (evolution == null) evolution = gameObject.AddComponent<StageEnemyEvolutionV12>();
        evolution.Configure(page);

        ApplyGuardPalette();
        StartCoroutine(ReapplyPaletteNextFrame());
    }

    private IEnumerator ReapplyPaletteNextFrame()
    {
        yield return null;
        ApplyGuardPalette();
    }

    public void ApplyGuardPalette()
    {
        bodyRenderers.Clear();
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null) continue;
            string n = renderer.gameObject.name.ToLowerInvariant();
            if (n.Contains("shadow") || n.Contains("telegraph") || n.Contains("warning") ||
                n.Contains("spawn") || n.Contains("outline") || n.Contains("health"))
                continue;

            bodyRenderers.Add(renderer);
            Color src = renderer.color;
            float amount = page == 1 ? 0.22f : page == 2 ? 0.31f : 0.39f;
            Color mixed = Color.Lerp(src, guardColor, amount);
            mixed.a = src.a;
            renderer.color = mixed;
        }

        EnemyTelegraph telegraph = GetComponentInChildren<EnemyTelegraph>(true);
        if (telegraph != null)
            telegraph.SetPalette(
                new Color(0.12f, 0.72f, 0.16f, 0.17f),
                new Color(0.50f, 1f, 0.24f, 0.94f),
                new Color(0.78f, 1f, 0.42f, 0.72f));

        EnemyCombatFeedback feedback = GetComponent<EnemyCombatFeedback>();
        if (feedback != null) feedback.RefreshBaseColors();
        EnemyHitEffect hit = GetComponent<EnemyHitEffect>();
        if (hit != null) hit.RefreshOriginalColor();
    }

    public void ScheduleDeathBurst(Vector2 hitDirection)
    {
        if (deathBurstScheduled || suppressDeathDamage) return;
        deathBurstScheduled = true;
        float radius;
        int damage;
        switch (enemyType)
        {
            case EnemyType.Tank: radius = 1.25f; damage = 2; break;
            case EnemyType.Bomber: radius = 1.35f; damage = 2; break;
            case EnemyType.Ranged: radius = 0.85f; damage = 1; break;
            default: radius = 0.90f; damage = 1; break;
        }
        float delay = page >= 3 ? 0.58f : page == 2 ? 0.64f : 0.72f;
        Transform parent = boss != null && boss.OwnerRoom != null ? boss.OwnerRoom.transform : null;
        ChernobylGuardDeathBurst.Create(transform.position, radius, damage, delay,
            page >= 3 ? new Color(0.70f, 1f, 0.12f, 0.94f) : new Color(0.40f, 1f, 0.20f, 0.90f), parent);
    }

    public void ResolveBomberExplosion(Vector2 origin, float radius, int damage)
    {
        if (deathBurstScheduled || suppressDeathDamage) return;
        deathBurstScheduled = true;
        Transform parent = boss != null && boss.OwnerRoom != null ? boss.OwnerRoom.transform : null;
        ChernobylGuardDeathBurst.Create(origin, radius, Mathf.Max(1, damage), 0.04f,
            new Color(0.68f, 1f, 0.10f, 0.98f), parent);
    }

    public void ShutdownWithoutDamage()
    {
        if (suppressDeathDamage) return;
        suppressDeathDamage = true;
        StartCoroutine(ShutdownRoutine());
    }

    private IEnumerator ShutdownRoutine()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b = behaviours[i];
            if (b == null || b == this || b is EnemyHealth || b is EnemyVisualMotion || b is EnemyCombatFeedback) continue;
            string n = b.GetType().Name;
            if (n.EndsWith("AI") || n == "EnemyMotor" || n == "EnemyDamage" || n == "EnemyPerception")
                b.enabled = false;
        }
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = false;

        ChernobylBossEffects.SpawnCoreSparks(transform.position, new Color(0.52f, 1f, 0.22f, 1f), 8, 2.2f);
        float elapsed = 0f;
        const float duration = 0.36f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < bodyRenderers.Count; i++)
            {
                SpriteRenderer r = bodyRenderers[i];
                if (r == null) continue;
                Color c = r.color;
                c.a = 1f - t;
                r.color = c;
            }
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, t);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (director != null) director.NotifyGuardDestroyed(this);
    }
}
