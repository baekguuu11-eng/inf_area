using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class MachineWormBossController : MonoBehaviour
{
    private enum BossState
    {
        Surface,
        Firing,
        Burrowing,
        Hidden,
        Emerging,
        Dead
    }

    [Header("Visual References")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private Transform firePoint;
    [SerializeField] private SpriteRenderer hitExpressionOverlay;

    [Header("Surface Idle")]
    [SerializeField] private float idleBobHeight = 0.035f;
    [SerializeField] private float idleBobSpeed = 2.1f;
    [SerializeField] private float idleTilt = 0.8f;
    [SerializeField] private float idlePulse = 0.015f;

    [Header("Projectile Pattern")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private int baseProjectileCount = 3;
    [SerializeField] private int projectileDamage = 1;
    [SerializeField] private float projectileSpeed = 7.5f;
    [SerializeField] private float projectileSpread = 18f;
    [SerializeField] private float burstShotDelay = 0.08f;
    [SerializeField] private Vector2 burstIntervalRange = new Vector2(1.15f, 1.75f);
    [SerializeField] private float aimPredictionTime = 0.16f;

    [Header("Burrow Trigger")]
    [SerializeField] private int damageBeforeBurrow = 5;
    [SerializeField] private float minimumSurfaceTime = 2.2f;
    [SerializeField] private float burrowDuration = 0.42f;
    [SerializeField] private float hiddenTrackingDuration = 0.65f;
    [SerializeField] private float warningDuration = 0.95f;
    [SerializeField] private float warningRadius = 1.25f;
    [SerializeField] private float burrowDepth = 2.5f;

    [Header("Emergence")]
    [SerializeField] private float emergeDuration = 0.34f;
    [SerializeField] private float emergeOvershoot = 0.22f;
    [SerializeField] private float emergeDamageRadius = 1.2f;
    [SerializeField] private int emergeDamage = 2;
    [SerializeField] private float postEmergeRecovery = 0.55f;

    [Header("Warning / Impact")]
    [SerializeField] private GameObject warningZonePrefab;
    [SerializeField] private LayerMask playerMask;
    [SerializeField] private float impactShakeDuration = 0.18f;
    [SerializeField] private float impactShakeMagnitude = 0.14f;

    [Header("Hit Expression")]
    [SerializeField] private float hitExpressionDuration = 0.13f;
    [SerializeField] private Vector3 hitExpressionLocalPosition = new Vector3(0f, 0.82f, -0.02f);
    [SerializeField] private Vector2 hitExpressionWorldSize = new Vector2(0.82f, 0.34f);

    [Header("Phase Tuning")]
    [SerializeField, Range(0.1f, 0.95f)] private float phaseTwoHealth = 0.65f;
    [SerializeField, Range(0.05f, 0.9f)] private float phaseThreeHealth = 0.32f;
    [SerializeField] private float phaseTwoIntervalMultiplier = 0.84f;
    [SerializeField] private float phaseThreeIntervalMultiplier = 0.68f;

    private EnemyHealth health;
    private Rigidbody2D body;
    private Collider2D[] bossColliders;
    private Transform player;
    private Rigidbody2D playerBody;
    private RoomController room;

    private BossState state = BossState.Surface;
    private Vector3 visualBaseLocalPosition;
    private Vector3 visualBaseLocalScale;
    private Quaternion visualBaseLocalRotation;
    private Color visualBaseColor = Color.white;
    private int accumulatedDamage;
    private bool burrowRequested;
    private float surfaceEnteredTime;
    private Coroutine mainRoutine;
    private Coroutine expressionRoutine;
    private bool impactApplied;

    public bool IsBurrowed { get { return state == BossState.Burrowing || state == BossState.Hidden || state == BossState.Emerging; } }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        body = GetComponent<Rigidbody2D>();
        bossColliders = GetComponentsInChildren<Collider2D>(true);
        room = GetComponentInParent<RoomController>();

        ResolveVisualReferences();
        ResolvePlayer();
        ConfigureBody();
        EnsureHitExpressionOverlay();

        visualBaseLocalPosition = visualRoot.localPosition;
        visualBaseLocalScale = visualRoot.localScale;
        visualBaseLocalRotation = visualRoot.localRotation;
        if (visualRenderer != null)
            visualBaseColor = visualRenderer.color;

        health.Damaged += OnDamaged;
        health.Died += OnDied;
        surfaceEnteredTime = Time.time;
    }

    private void Start()
    {
        mainRoutine = StartCoroutine(BossLoop());
    }

    private void LateUpdate()
    {
        if (state != BossState.Surface || visualRoot == null)
            return;

        float wave = Mathf.Sin(Time.time * idleBobSpeed);
        visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * (wave * idleBobHeight);
        visualRoot.localScale = visualBaseLocalScale * (1f + wave * idlePulse);
        visualRoot.localRotation = visualBaseLocalRotation * Quaternion.Euler(0f, 0f, wave * idleTilt);
    }

    private IEnumerator BossLoop()
    {
        yield return new WaitForSeconds(0.75f);

        while (state != BossState.Dead)
        {
            if (player == null)
                ResolvePlayer();

            if (GameInputState.IsLocked)
            {
                yield return null;
                continue;
            }

            if (burrowRequested && Time.time - surfaceEnteredTime >= minimumSurfaceTime)
            {
                yield return BurrowCycle();
                continue;
            }

            yield return FireBurst();

            float interval = Random.Range(
                Mathf.Min(burstIntervalRange.x, burstIntervalRange.y),
                Mathf.Max(burstIntervalRange.x, burstIntervalRange.y)
            );
            interval *= GetIntervalMultiplier();

            float elapsed = 0f;
            while (elapsed < interval && state != BossState.Dead)
            {
                if (!GameInputState.IsLocked)
                    elapsed += Time.deltaTime;

                if (burrowRequested && Time.time - surfaceEnteredTime >= minimumSurfaceTime)
                    break;

                yield return null;
            }
        }
    }

    private IEnumerator FireBurst()
    {
        if (state == BossState.Dead || player == null)
            yield break;

        state = BossState.Firing;
        int count = Mathf.Max(1, baseProjectileCount + GetPhaseProjectileBonus());
        Vector2 predictedTarget = (Vector2)player.position;
        if (playerBody != null)
            predictedTarget += playerBody.linearVelocity * Mathf.Max(0f, aimPredictionTime);

        Vector2 centerDirection = predictedTarget - (Vector2)GetFirePosition();
        if (centerDirection.sqrMagnitude < 0.0001f)
            centerDirection = Vector2.down;
        centerDirection.Normalize();

        yield return AnimateFireRecoil(true);

        for (int i = 0; i < count && state != BossState.Dead; i++)
        {
            float centeredIndex = i - (count - 1) * 0.5f;
            float deterministicSpread = count > 1 ? centeredIndex * projectileSpread : 0f;
            float randomJitter = Random.Range(-projectileSpread * 0.22f, projectileSpread * 0.22f);
            Vector2 direction = Rotate(centerDirection, deterministicSpread + randomJitter);
            SpawnProjectile(direction);

            if (i < count - 1)
                yield return new WaitForSeconds(Mathf.Max(0.01f, burstShotDelay));
        }

        yield return AnimateFireRecoil(false);
        ResetVisualTransform();
        state = BossState.Surface;
    }

    private IEnumerator AnimateFireRecoil(bool compress)
    {
        if (visualRoot == null)
            yield break;

        Vector3 startScale = visualRoot.localScale;
        Vector3 targetScale = compress
            ? new Vector3(visualBaseLocalScale.x * 1.08f, visualBaseLocalScale.y * 0.90f, visualBaseLocalScale.z)
            : visualBaseLocalScale;
        Vector3 startPosition = visualRoot.localPosition;
        Vector3 targetPosition = visualBaseLocalPosition + (compress ? Vector3.down * 0.07f : Vector3.zero);
        float duration = compress ? 0.09f : 0.11f;
        float elapsed = 0f;

        while (elapsed < duration && state != BossState.Dead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            visualRoot.localScale = Vector3.Lerp(startScale, targetScale, eased);
            visualRoot.localPosition = Vector3.Lerp(startPosition, targetPosition, eased);
            yield return null;
        }
    }

    private void SpawnProjectile(Vector2 direction)
    {
        Vector3 spawnPosition = GetFirePosition();
        GameObject projectileObject;

        if (projectilePrefab != null)
        {
            projectileObject = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            projectileObject = new GameObject("MachineWormProjectile_Runtime");
            projectileObject.transform.position = spawnPosition;
            projectileObject.AddComponent<SpriteRenderer>();
            projectileObject.AddComponent<Rigidbody2D>();
            projectileObject.AddComponent<CircleCollider2D>();
            projectileObject.AddComponent<MachineWormProjectile>();
        }

        MachineWormProjectile projectile = projectileObject.GetComponent<MachineWormProjectile>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<MachineWormProjectile>();

        float phaseSpeed = projectileSpeed * (GetHealthRatio() <= phaseThreeHealth ? 1.18f : 1f);
        projectile.Setup(direction, phaseSpeed, projectileDamage);
    }

    private IEnumerator BurrowCycle()
    {
        burrowRequested = false;
        accumulatedDamage = 0;
        state = BossState.Burrowing;
        SetBossCollidersEnabled(false);

        Vector3 startPosition = visualRoot.localPosition;
        Vector3 hiddenPosition = visualBaseLocalPosition + Vector3.down * Mathf.Max(0.5f, burrowDepth);
        Vector3 startScale = visualRoot.localScale;
        Vector3 compressedScale = new Vector3(visualBaseLocalScale.x * 0.82f, visualBaseLocalScale.y * 1.12f, visualBaseLocalScale.z);
        float elapsed = 0f;
        float duration = Mathf.Max(0.08f, burrowDuration);

        while (elapsed < duration && state != BossState.Dead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;
            visualRoot.localPosition = Vector3.Lerp(startPosition, hiddenPosition, eased);
            visualRoot.localScale = Vector3.Lerp(startScale, compressedScale, Mathf.Sin(t * Mathf.PI));
            SetVisualAlpha(1f - t);
            yield return null;
        }

        if (state == BossState.Dead)
            yield break;

        state = BossState.Hidden;
        SetVisualAlpha(0f);
        Vector3 targetPosition = transform.position;
        elapsed = 0f;

        while (elapsed < Mathf.Max(0.05f, hiddenTrackingDuration) && state != BossState.Dead)
        {
            if (!GameInputState.IsLocked)
            {
                elapsed += Time.deltaTime;
                if (player == null)
                    ResolvePlayer();
                if (player != null)
                {
                    Vector3 predicted = player.position;
                    if (playerBody != null)
                        predicted += (Vector3)(playerBody.linearVelocity * 0.12f);
                    targetPosition = ClampToRoom(predicted);
                }
            }
            yield return null;
        }

        if (state == BossState.Dead)
            yield break;

        MachineWormWarningZone warning = SpawnWarning(targetPosition);
        float wait = warning != null ? warning.Duration : Mathf.Max(0.1f, warningDuration);
        elapsed = 0f;
        while (elapsed < wait && state != BossState.Dead)
        {
            if (!GameInputState.IsLocked)
                elapsed += Time.deltaTime;
            yield return null;
        }

        if (state == BossState.Dead)
            yield break;

        if (warning != null)
            Destroy(warning.gameObject);

        SetWorldPosition(targetPosition);
        yield return EmergeRoutine();
        surfaceEnteredTime = Time.time;
        state = BossState.Surface;
    }

    private MachineWormWarningZone SpawnWarning(Vector3 targetPosition)
    {
        GameObject warningObject;
        if (warningZonePrefab != null)
        {
            warningObject = Instantiate(warningZonePrefab, targetPosition, Quaternion.identity);
        }
        else
        {
            warningObject = new GameObject("MachineWorm_WarningZone");
            warningObject.transform.position = targetPosition;
            warningObject.AddComponent<SpriteRenderer>();
            warningObject.AddComponent<MachineWormWarningZone>();
        }

        MachineWormWarningZone warning = warningObject.GetComponent<MachineWormWarningZone>();
        if (warning == null)
            warning = warningObject.AddComponent<MachineWormWarningZone>();
        warning.Initialize(targetPosition, warningRadius, warningDuration);
        return warning;
    }

    private IEnumerator EmergeRoutine()
    {
        state = BossState.Emerging;
        impactApplied = false;
        SetVisualAlpha(1f);

        Vector3 hiddenPosition = visualBaseLocalPosition + Vector3.down * Mathf.Max(0.5f, burrowDepth);
        Vector3 overshootPosition = visualBaseLocalPosition + Vector3.up * Mathf.Max(0f, emergeOvershoot);
        visualRoot.localPosition = hiddenPosition;
        visualRoot.localScale = new Vector3(visualBaseLocalScale.x * 0.72f, visualBaseLocalScale.y * 1.32f, visualBaseLocalScale.z);

        float elapsed = 0f;
        float duration = Mathf.Max(0.08f, emergeDuration);
        while (elapsed < duration && state != BossState.Dead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float riseT = 1f - Mathf.Pow(1f - t, 3f);
            visualRoot.localPosition = Vector3.Lerp(hiddenPosition, overshootPosition, riseT);
            visualRoot.localScale = Vector3.Lerp(
                new Vector3(visualBaseLocalScale.x * 0.72f, visualBaseLocalScale.y * 1.32f, visualBaseLocalScale.z),
                new Vector3(visualBaseLocalScale.x * 1.13f, visualBaseLocalScale.y * 0.88f, visualBaseLocalScale.z),
                riseT
            );

            if (!impactApplied && t >= 0.72f)
            {
                impactApplied = true;
                ApplyEmergenceImpact();
            }
            yield return null;
        }

        if (!impactApplied && state != BossState.Dead)
            ApplyEmergenceImpact();

        elapsed = 0f;
        float settleDuration = 0.16f;
        Vector3 settleStartPosition = visualRoot.localPosition;
        Vector3 settleStartScale = visualRoot.localScale;
        while (elapsed < settleDuration && state != BossState.Dead)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            float eased = t * t * (3f - 2f * t);
            visualRoot.localPosition = Vector3.Lerp(settleStartPosition, visualBaseLocalPosition, eased);
            visualRoot.localScale = Vector3.Lerp(settleStartScale, visualBaseLocalScale, eased);
            yield return null;
        }

        ResetVisualTransform();
        SetBossCollidersEnabled(true);
        yield return new WaitForSeconds(Mathf.Max(0f, postEmergeRecovery));
    }

    private void ApplyEmergenceImpact()
    {
        int mask = playerMask.value == 0 ? Physics2D.AllLayers : playerMask.value;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, Mathf.Max(0.1f, emergeDamageRadius), mask);
        HashSet<PlayerHealth> damaged = new HashSet<PlayerHealth>();
        for (int i = 0; i < hits.Length; i++)
        {
            PlayerHealth playerHealth = hits[i] != null ? hits[i].GetComponentInParent<PlayerHealth>() : null;
            if (playerHealth == null || damaged.Contains(playerHealth))
                continue;
            damaged.Add(playerHealth);
            playerHealth.TakeDamage(Mathf.Max(1, emergeDamage));
        }

        if (GameFeelManager.Instance != null)
        {
            GameFeelManager.Instance.DoHitStop(0.045f);
            GameFeelManager.Instance.Shake(impactShakeDuration, impactShakeMagnitude);
        }

        GameObject impactRing = new GameObject("MachineWorm_ImpactRing");
        impactRing.transform.position = transform.position;
        SpriteRenderer ringRenderer = impactRing.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = RuntimePixelSpriteFactory.GetWarningZoneSprite();
        ringRenderer.color = new Color(1f, 0.5f, 0.25f, 0.75f);
        ringRenderer.sortingOrder = 26;
        StartCoroutine(FadeImpactRing(impactRing, ringRenderer));
    }

    private IEnumerator FadeImpactRing(GameObject ring, SpriteRenderer renderer)
    {
        float elapsed = 0f;
        float duration = 0.28f;
        float spriteSize = renderer.sprite != null ? Mathf.Max(0.001f, renderer.sprite.bounds.size.x) : 1f;
        Vector3 startScale = Vector3.one * (emergeDamageRadius * 0.6f / spriteSize);
        Vector3 endScale = Vector3.one * (emergeDamageRadius * 2.4f / spriteSize);
        while (elapsed < duration && ring != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ring.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            Color color = renderer.color;
            color.a = Mathf.Lerp(0.75f, 0f, t);
            renderer.color = color;
            yield return null;
        }
        if (ring != null)
            Destroy(ring);
    }

    private void OnDamaged(EnemyHealth source, int appliedDamage, Vector2 hitDirection)
    {
        if (state == BossState.Dead)
            return;

        accumulatedDamage += Mathf.Max(0, appliedDamage);
        if (accumulatedDamage >= Mathf.Max(1, damageBeforeBurrow))
            burrowRequested = true;

        if (expressionRoutine != null)
            StopCoroutine(expressionRoutine);
        expressionRoutine = StartCoroutine(ShowHitExpression());
    }

    private IEnumerator ShowHitExpression()
    {
        if (hitExpressionOverlay == null)
            yield break;

        hitExpressionOverlay.enabled = true;
        float elapsed = 0f;
        float duration = Mathf.Max(0.04f, hitExpressionDuration);
        Vector3 baseScale = hitExpressionOverlay.transform.localScale;
        while (elapsed < duration && state != BossState.Dead)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.12f;
            hitExpressionOverlay.transform.localScale = baseScale * pulse;
            yield return null;
        }
        hitExpressionOverlay.transform.localScale = baseScale;
        hitExpressionOverlay.enabled = false;
        expressionRoutine = null;
    }

    private void OnDied(EnemyHealth source)
    {
        state = BossState.Dead;
        if (mainRoutine != null)
            StopCoroutine(mainRoutine);
        mainRoutine = null;
        SetBossCollidersEnabled(false);
        if (hitExpressionOverlay != null)
            hitExpressionOverlay.enabled = false;
    }

    private void ResolveVisualReferences()
    {
        if (visualRoot == null)
        {
            Transform found = transform.Find("VisualRoot");
            visualRoot = found != null ? found : transform;
        }

        if (visualRenderer == null)
            visualRenderer = visualRoot.GetComponentInChildren<SpriteRenderer>(true);

        if (firePoint == null)
        {
            Transform found = transform.Find("FirePoint");
            if (found == null)
            {
                GameObject point = new GameObject("FirePoint");
                point.transform.SetParent(transform, false);
                point.transform.localPosition = new Vector3(0f, 1.45f, 0f);
                found = point.transform;
            }
            firePoint = found;
        }
    }

    private void ResolvePlayer()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found == null)
            return;
        player = found.transform;
        playerBody = found.GetComponent<Rigidbody2D>();
    }

    private void ConfigureBody()
    {
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void EnsureHitExpressionOverlay()
    {
        if (hitExpressionOverlay == null)
        {
            Transform existing = visualRoot.Find("HitExpressionOverlay");
            GameObject overlayObject;
            if (existing != null)
                overlayObject = existing.gameObject;
            else
            {
                overlayObject = new GameObject("HitExpressionOverlay");
                overlayObject.transform.SetParent(visualRoot, false);
            }

            hitExpressionOverlay = overlayObject.GetComponent<SpriteRenderer>();
            if (hitExpressionOverlay == null)
                hitExpressionOverlay = overlayObject.AddComponent<SpriteRenderer>();
        }

        hitExpressionOverlay.sprite = RuntimePixelSpriteFactory.GetHitExpressionSprite();
        hitExpressionOverlay.transform.localPosition = hitExpressionLocalPosition;

        Vector2 spriteSize = hitExpressionOverlay.sprite != null ? hitExpressionOverlay.sprite.bounds.size : Vector2.one;
        hitExpressionOverlay.transform.localScale = new Vector3(
            hitExpressionWorldSize.x / Mathf.Max(0.001f, spriteSize.x),
            hitExpressionWorldSize.y / Mathf.Max(0.001f, spriteSize.y),
            1f
        );

        if (visualRenderer != null)
        {
            hitExpressionOverlay.sortingLayerID = visualRenderer.sortingLayerID;
            hitExpressionOverlay.sortingOrder = visualRenderer.sortingOrder + 2;
        }
        hitExpressionOverlay.enabled = false;
    }

    private void SetBossCollidersEnabled(bool enabledState)
    {
        if (bossColliders == null)
            return;
        for (int i = 0; i < bossColliders.Length; i++)
        {
            if (bossColliders[i] != null)
                bossColliders[i].enabled = enabledState;
        }
    }

    private void SetVisualAlpha(float alpha)
    {
        if (visualRenderer == null)
            return;
        Color color = visualBaseColor;
        color.a = Mathf.Clamp01(alpha);
        visualRenderer.color = color;
    }

    private void ResetVisualTransform()
    {
        if (visualRoot == null)
            return;
        visualRoot.localPosition = visualBaseLocalPosition;
        visualRoot.localScale = visualBaseLocalScale;
        visualRoot.localRotation = visualBaseLocalRotation;
        SetVisualAlpha(1f);
    }

    private Vector3 GetFirePosition()
    {
        return firePoint != null ? firePoint.position : transform.position;
    }

    private Vector3 ClampToRoom(Vector3 position)
    {
        if (room == null)
            room = GetComponentInParent<RoomController>();
        if (room == null || room.EnemySpawnArea == null)
            return new Vector3(position.x, position.y, transform.position.z);

        Bounds bounds = room.EnemySpawnArea.bounds;
        float margin = Mathf.Max(0.25f, warningRadius * 0.55f);
        float minX = bounds.min.x + margin;
        float maxX = bounds.max.x - margin;
        float minY = bounds.min.y + margin;
        float maxY = bounds.max.y - margin;

        if (minX > maxX) minX = maxX = bounds.center.x;
        if (minY > maxY) minY = maxY = bounds.center.y;

        return new Vector3(
            Mathf.Clamp(position.x, minX, maxX),
            Mathf.Clamp(position.y, minY, maxY),
            transform.position.z
        );
    }

    private void SetWorldPosition(Vector3 position)
    {
        position.z = transform.position.z;
        body.position = position;
        transform.position = position;
        body.linearVelocity = Vector2.zero;
    }

    private float GetHealthRatio()
    {
        return health != null ? health.NormalizedHealth : 1f;
    }

    private int GetPhaseProjectileBonus()
    {
        float ratio = GetHealthRatio();
        if (ratio <= phaseThreeHealth) return 2;
        if (ratio <= phaseTwoHealth) return 1;
        return 0;
    }

    private float GetIntervalMultiplier()
    {
        float ratio = GetHealthRatio();
        if (ratio <= phaseThreeHealth) return Mathf.Max(0.2f, phaseThreeIntervalMultiplier);
        if (ratio <= phaseTwoHealth) return Mathf.Max(0.2f, phaseTwoIntervalMultiplier);
        return 1f;
    }

    private Vector2 Rotate(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, emergeDamageRadius);
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, warningRadius);
    }
}
