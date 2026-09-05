// TARGET: Assets/Scripts/Enemy/EnemyRangedAI.cs
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyPerception))]
[RequireComponent(typeof(EnemyMotor))]
[DisallowMultipleComponent]
public sealed class EnemyRangedAI : MonoBehaviour
{
    private enum State
    {
        Observe,
        Approach,
        Strafe,
        Retreat,
        Windup,
        Burst,
        Recovery
    }

    [Header("거리 구역")]
    [SerializeField] private float detectRange = 7.2f;
    [SerializeField] private float preferredMinRange = 3.2f;
    [SerializeField] private float preferredMaxRange = 5.4f;
    [SerializeField] private float retreatTriggerRange = 2.25f;

    [Header("이동")]
    [SerializeField] private float moveSpeed = 2.05f;
    [SerializeField] private float retreatSpeed = 2.75f;
    [SerializeField] private float retreatDuration = 0.58f;
    [SerializeField] private float retreatCooldown = 2.45f;
    [SerializeField] private float strafeDirectionMinDuration = 0.8f;
    [SerializeField] private float strafeDirectionMaxDuration = 1.35f;
    [SerializeField] private float decisionInterval = 0.08f;

    [Header("2점사")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float fireWindup = 0.42f;
    [SerializeField] private float burstShotInterval = 0.14f;
    [SerializeField] private float fireRecovery = 1.15f;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private int projectileDamage = 1;
    [SerializeField, Range(0f, 1f)] private float predictionChance = 0.2f;
    [SerializeField] private float predictionTime = 0.20f;
    [SerializeField] private float aimLineWidth = 0.055f;
    [SerializeField] private LayerMask lineOfSightMask;

    [Header("참조")]
    [SerializeField] private EnemyPerception perception;
    [SerializeField] private EnemyMotor motor;
    [SerializeField] private EnemyVisualMotion visualMotion;
    [SerializeField] private EnemyTelegraph telegraph;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private EnemyAttackContract attackContract;

    private Rigidbody2D body;
    private EnemyAttackCoordinator coordinator;
    private State state;
    private Coroutine burstRoutine;
    private float nextDecisionTime;
    private float nextBurstAllowedTime;
    private float retreatEndTime;
    private float nextRetreatAllowedTime;
    private float nextStrafeChangeTime;
    private int strafeSign;
    private bool ownsAttackToken;
    private StageEnemyEvolutionV12 evolution;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        perception = perception != null ? perception : GetComponent<EnemyPerception>();
        motor = motor != null ? motor : GetComponent<EnemyMotor>();
        visualMotion = visualMotion != null ? visualMotion : GetComponent<EnemyVisualMotion>();
        telegraph = telegraph != null ? telegraph : GetComponentInChildren<EnemyTelegraph>(true);
        health = health != null ? health : GetComponent<EnemyHealth>();
        attackContract = attackContract != null ? attackContract : (GetComponent<EnemyAttackContract>() ?? gameObject.AddComponent<EnemyAttackContract>());
        if (firePoint == null)
        {
            Transform found = transform.Find("FirePoint");
            firePoint = found != null ? found : transform;
        }

        coordinator = EnemyAttackCoordinator.EnsureInstance();
        strafeSign = Random.value < 0.5f ? -1 : 1;
        ScheduleStrafeChange();
    }

    private void OnEnable()
    {
        coordinator = EnemyAttackCoordinator.EnsureInstance();
        nextDecisionTime = 0f;
    }

    private void Update()
    {
        if (health != null && health.IsDead)
            return;

        if (GameInputState.IsLocked)
        {
            InterruptBurst();
            motor.StopIntent();
            state = State.Observe;
            return;
        }

        if (motor.IsKnockedBack)
        {
            if (burstRoutine != null)
                InterruptBurst();
            return;
        }

        if (burstRoutine != null || Time.time < nextDecisionTime)
            return;

        nextDecisionTime = Time.time + Mathf.Max(0.03f, decisionInterval);
        perception.RefreshNow();
        if (!perception.HasPlayer || perception.DistanceToPlayer > detectRange)
        {
            state = State.Observe;
            motor.StopIntent();
            return;
        }

        float distance = perception.DistanceToPlayer;
        Vector2 toPlayer = perception.PlayerPosition - body.position;

        if (state == State.Retreat && Time.time < retreatEndTime)
        {
            motor.SetMoveIntent(-toPlayer, retreatSpeed);
            visualMotion.SetAction(EnemyVisualMotion.ActionState.None, -toPlayer);
            return;
        }

        if (distance <= retreatTriggerRange && Time.time >= nextRetreatAllowedTime)
        {
            state = State.Retreat;
            retreatEndTime = Time.time + retreatDuration;
            nextRetreatAllowedTime = Time.time + retreatCooldown;
            motor.SetMoveIntent(-toPlayer, retreatSpeed);
            return;
        }

        bool inFireZone = distance >= preferredMinRange && distance <= preferredMaxRange;
        bool hasSight = perception.HasLineOfSight(lineOfSightMask);
        if (inFireZone && hasSight && Time.time >= nextBurstAllowedTime &&
            coordinator.TryAcquire(this, EnemyAttackCoordinator.AttackChannel.Ranged, fireWindup + burstShotInterval + fireRecovery + 1f))
        {
            ownsAttackToken = true;
            burstRoutine = StartCoroutine(BurstRoutine());
            return;
        }

        if (distance > preferredMaxRange || !hasSight)
        {
            state = State.Approach;
            Vector2 targetDirection = toPlayer.normalized;
            if (!hasSight)
                targetDirection = (targetDirection + EnemyAIUtility.Perpendicular(targetDirection, strafeSign) * 0.55f).normalized;
            motor.SetMoveIntent(targetDirection, moveSpeed);
            visualMotion.SetAction(EnemyVisualMotion.ActionState.None, targetDirection);
        }
        else
        {
            state = State.Strafe;
            if (Time.time >= nextStrafeChangeTime)
            {
                strafeSign *= -1;
                ScheduleStrafeChange();
            }

            Vector2 tangent = EnemyAIUtility.Perpendicular(toPlayer, strafeSign);
            float radialCorrection = distance < preferredMinRange ? -0.48f : distance > preferredMaxRange ? 0.42f : 0f;
            Vector2 desired = (tangent + toPlayer.normalized * radialCorrection).normalized;
            motor.SetMoveIntent(desired, moveSpeed);
            visualMotion.SetAction(EnemyVisualMotion.ActionState.None, desired);
        }
    }

    private IEnumerator BurstRoutine()
    {
        if (evolution == null) evolution = GetComponent<StageEnemyEvolutionV12>();
        int evolvedVariant = evolution != null ? evolution.ChooseRangedVariant() : 0;
        state = State.Windup;
        attackContract.BeginTelegraph(telegraph);
        if (evolvedVariant > 0) evolution?.ApplyEvolvedTelegraph(telegraph, evolvedVariant >= 2);
        else evolution?.ResetTelegraph(telegraph);
        motor.SetMovementEnabled(false, true);
        Vector2 lockedDirection = perception.DirectionToPlayer;
        float elapsed = 0f;

        while (elapsed < fireWindup)
        {
            if (ShouldInterrupt())
            {
                InterruptBurst();
                yield break;
            }

            float progress = elapsed / Mathf.Max(0.01f, fireWindup);
            if (progress < 0.62f)
                lockedDirection = GetAimDirection(false);

            float length = Mathf.Min(6.5f, Vector2.Distance(firePoint.position, perception.PlayerPosition));
            telegraph?.ShowLine(lockedDirection, length, aimLineWidth);
            telegraph?.SetProgress(progress);
            visualMotion.SetAction(EnemyVisualMotion.ActionState.Windup, lockedDirection);

            elapsed += Time.deltaTime;
            yield return null;
        }

        state = State.Burst;
        attackContract.BeginActive(telegraph);
        if (evolvedVariant == 1)
        {
            // Stage 2 pattern: a readable three-way fan instead of the normal 2-shot burst.
            Vector2 center = lockedDirection.sqrMagnitude > 0.001f ? lockedDirection.normalized : Vector2.down;
            Vector2[] directions = { Rotate(center, -13f), center, Rotate(center, 13f) };
            for (int shot = 0; shot < directions.Length; shot++)
            {
                if (ShouldInterrupt()) { InterruptBurst(); yield break; }
                visualMotion.SetAction(EnemyVisualMotion.ActionState.Attack, directions[shot]);
                FireProjectile(directions[shot]);
                if (shot < directions.Length - 1)
                    yield return new WaitForSeconds(0.055f);
            }
        }
        else if (evolvedVariant == 2)
        {
            // Stage 3 pattern: three pursuit shots that re-read the player's movement each time.
            for (int shot = 0; shot < 3; shot++)
            {
                if (ShouldInterrupt()) { InterruptBurst(); yield break; }
                Vector2 direction = shot == 0 ? lockedDirection : GetAimDirection(true);
                visualMotion.SetAction(EnemyVisualMotion.ActionState.Attack, direction);
                FireProjectile(direction);
                if (shot < 2) yield return new WaitForSeconds(0.12f);
            }
        }
        else
        {
            for (int shot = 0; shot < 2; shot++)
            {
                if (ShouldInterrupt())
                {
                    InterruptBurst();
                    yield break;
                }

                Vector2 direction = shot == 0 ? lockedDirection : GetAimDirection(true);
                visualMotion.SetAction(EnemyVisualMotion.ActionState.Attack, direction);
                FireProjectile(direction);

                if (shot == 0)
                    yield return new WaitForSeconds(Mathf.Max(0.01f, burstShotInterval));
            }
        }

        telegraph?.Hide();
        evolution?.ResetTelegraph(telegraph);
        attackContract.BeginRecovery(telegraph);
        state = State.Recovery;
        visualMotion.SetAction(EnemyVisualMotion.ActionState.Recovery, lockedDirection);
        yield return new WaitForSeconds(Mathf.Max(0.01f, fireRecovery));
        nextBurstAllowedTime = Time.time + Random.Range(0.2f, 0.5f);
        FinishBurst();
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos).normalized;
    }

    private Vector2 GetAimDirection(bool allowPrediction)
    {
        Vector2 target = perception.PlayerPosition;
        if (allowPrediction && Random.value < predictionChance)
            target = perception.PredictPlayerPosition(predictionTime, 0.95f);
        Vector2 direction = target - (Vector2)firePoint.position;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
    }

    private void FireProjectile(Vector2 direction)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("[EnemyRangedAI] EnemyProjectile 프리팹이 없습니다.", this);
            return;
        }

        GameObject projectileObject = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        EnemyProjectile projectile = projectileObject.GetComponent<EnemyProjectile>();
        if (projectile != null)
            projectile.Setup(direction, projectileSpeed, projectileDamage);
    }

    private bool ShouldInterrupt()
    {
        return GameInputState.IsLocked || motor.IsKnockedBack || (health != null && health.IsDead) || !isActiveAndEnabled;
    }

    private void FinishBurst()
    {
        attackContract.FinishAttack(telegraph);
        motor.SetMovementEnabled(true, true);
        visualMotion.SetAction(EnemyVisualMotion.ActionState.None, Vector2.down);
        state = State.Strafe;
        burstRoutine = null;
        ReleaseToken();
    }

    private void InterruptBurst()
    {
        if (burstRoutine != null)
            StopCoroutine(burstRoutine);
        burstRoutine = null;
        attackContract?.CancelAttack(telegraph);
        motor?.SetMovementEnabled(true, true);
        if (visualMotion != null)
            visualMotion.SetAction(EnemyVisualMotion.ActionState.None, Vector2.down);
        ReleaseToken();
    }

    private void ReleaseToken()
    {
        if (!ownsAttackToken)
            return;
        ownsAttackToken = false;
        coordinator?.Release(this, EnemyAttackCoordinator.AttackChannel.Ranged);
    }

    private void ScheduleStrafeChange()
    {
        nextStrafeChangeTime = Time.time + Random.Range(strafeDirectionMinDuration, strafeDirectionMaxDuration);
    }

    private void OnDisable()
    {
        InterruptBurst();
        coordinator?.ReleaseAll(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredMinRange);
        Gizmos.DrawWireSphere(transform.position, preferredMaxRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, retreatTriggerRange);
    }
}
