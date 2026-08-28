// TARGET: Assets/Scripts/Enemy/EnemyTankAI.cs
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyPerception))]
[RequireComponent(typeof(EnemyMotor))]
[DisallowMultipleComponent]
public sealed class EnemyTankAI : MonoBehaviour
{
    private enum State
    {
        Observe,
        Approach,
        SlamWindup,
        Slam,
        ChargeWindup,
        Charge,
        Recovery
    }

    [Header("감지 및 선택")]
    [SerializeField] private float detectRange = 7f;
    [SerializeField] private float slamTriggerRange = 1.92f;
    [SerializeField] private float chargeMinRange = 2.1f;
    [SerializeField] private float chargeMaxRange = 5.2f;
    [SerializeField] private float attackDecisionInterval = 0.18f;
    [SerializeField, Range(0f, 1f)] private float chargeSelectionChance = 0.25f;
    [SerializeField] private float chargeCooldown = 3.2f;

    [Header("이동")]
    [SerializeField] private float moveSpeed = 1.50f;

    [Header("전방 내려찍기")]
    [SerializeField] private float slamWindup = 0.76f;
    [SerializeField] private float slamAimTrackingTime = 0.18f;
    [SerializeField] private float slamRadius = 1.95f;
    [SerializeField, Range(20f, 180f)] private float slamAngle = 105f;
    [SerializeField] private int slamDamage = 2;
    [SerializeField] private float slamRecovery = 0.86f;

    [Header("직선 돌진")]
    [SerializeField] private float chargeWindup = 0.58f;
    [SerializeField] private float chargeAimTrackingTime = 0.18f;
    [SerializeField] private float chargeDistance = 3.15f;
    [SerializeField] private float chargeOvershoot = 0.38f;
    [SerializeField] private float chargeDuration = 0.32f;
    [SerializeField] private float chargeWidth = 0.90f;
    [SerializeField] private int chargeDamage = 2;
    [SerializeField] private float chargeRecovery = 1.02f;
    [SerializeField] private LayerMask wallMask;

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
    private Coroutine attackRoutine;
    private float nextDecisionTime;
    private float nextChargeAllowedTime;
    private bool ownsHeavyToken;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        perception = perception != null ? perception : GetComponent<EnemyPerception>();
        motor = motor != null ? motor : GetComponent<EnemyMotor>();
        visualMotion = visualMotion != null ? visualMotion : GetComponent<EnemyVisualMotion>();
        telegraph = telegraph != null ? telegraph : GetComponentInChildren<EnemyTelegraph>(true);
        health = health != null ? health : GetComponent<EnemyHealth>();
        attackContract = attackContract != null ? attackContract : (GetComponent<EnemyAttackContract>() ?? gameObject.AddComponent<EnemyAttackContract>());
        coordinator = EnemyAttackCoordinator.EnsureInstance();
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
            InterruptAttack();
            motor.StopIntent();
            state = State.Observe;
            return;
        }

        if (motor.IsKnockedBack)
        {
            if (attackRoutine != null)
                InterruptAttack();
            return;
        }

        if (attackRoutine != null || Time.time < nextDecisionTime)
            return;

        nextDecisionTime = Time.time + Mathf.Max(0.05f, attackDecisionInterval);
        perception.RefreshNow();
        if (!perception.HasPlayer || perception.DistanceToPlayer > detectRange)
        {
            state = State.Observe;
            motor.StopIntent();
            return;
        }

        float distance = perception.DistanceToPlayer;
        bool canHeavyAttack = coordinator.TryAcquire(this, EnemyAttackCoordinator.AttackChannel.Heavy, 5f);
        if (canHeavyAttack)
        {
            if (distance <= slamTriggerRange)
            {
                ownsHeavyToken = true;
                attackRoutine = StartCoroutine(SlamRoutine());
                return;
            }

            bool canCharge = distance >= chargeMinRange && distance <= chargeMaxRange && Time.time >= nextChargeAllowedTime;
            if (canCharge && Random.value < chargeSelectionChance)
            {
                ownsHeavyToken = true;
                nextChargeAllowedTime = Time.time + chargeCooldown;
                attackRoutine = StartCoroutine(ChargeRoutine());
                return;
            }

            coordinator.Release(this, EnemyAttackCoordinator.AttackChannel.Heavy);
        }

        state = State.Approach;
        Vector2 direction = perception.DirectionToPlayer;
        motor.SetMoveIntent(direction, moveSpeed);
        visualMotion.SetAction(EnemyVisualMotion.ActionState.None, direction);
    }

    private IEnumerator SlamRoutine()
    {
        state = State.SlamWindup;
        attackContract.BeginTelegraph(telegraph);
        motor.SetMovementEnabled(false, true);
        Vector2 lockedDirection = perception.DirectionToPlayer;
        float elapsed = 0f;

        while (elapsed < slamWindup)
        {
            if (ShouldInterrupt())
            {
                InterruptAttack();
                yield break;
            }

            if (elapsed < slamAimTrackingTime)
                lockedDirection = perception.DirectionToPlayer;

            telegraph?.ShowSector(lockedDirection, slamRadius * 1.07f, slamAngle);
            telegraph?.SetProgress(elapsed / Mathf.Max(0.01f, slamWindup));
            visualMotion.SetAction(EnemyVisualMotion.ActionState.Windup, lockedDirection);

            elapsed += Time.deltaTime;
            yield return null;
        }

        state = State.Slam;
        attackContract.BeginActive(telegraph);
        visualMotion.SetAction(EnemyVisualMotion.ActionState.Attack, lockedDirection);
        attackContract.TryDamageSector(body.position, lockedDirection, slamRadius, slamAngle, slamDamage);

        CameraFeedbackController feedback = CameraFeedbackController.Instance;
        if (feedback != null)
        {
            feedback.HitStop(0.028f);
            feedback.Shake(0.13f, 0.11f, lockedDirection);
        }

        yield return new WaitForSecondsRealtime(0.11f);
        telegraph?.Hide();
        attackContract.BeginRecovery(telegraph);
        state = State.Recovery;
        visualMotion.SetAction(EnemyVisualMotion.ActionState.Recovery, lockedDirection);
        yield return new WaitForSeconds(Mathf.Max(0.01f, slamRecovery));
        FinishAttack();
    }

    private IEnumerator ChargeRoutine()
    {
        state = State.ChargeWindup;
        attackContract.BeginTelegraph(telegraph);
        motor.SetMovementEnabled(false, true);
        Vector2 lockedDirection = perception.DirectionToPlayer;
        float plannedDistance = Mathf.Clamp(
            perception.DistanceToPlayer + Mathf.Max(0f, chargeOvershoot),
            chargeDistance,
            chargeMaxRange + Mathf.Max(0f, chargeOvershoot));
        float elapsed = 0f;

        while (elapsed < chargeWindup)
        {
            if (ShouldInterrupt())
            {
                InterruptAttack();
                yield break;
            }

            if (elapsed < chargeAimTrackingTime)
            {
                lockedDirection = perception.DirectionToPlayer;
                plannedDistance = Mathf.Clamp(
                    perception.DistanceToPlayer + Mathf.Max(0f, chargeOvershoot),
                    chargeDistance,
                    chargeMaxRange + Mathf.Max(0f, chargeOvershoot));
            }

            telegraph?.ShowLine(lockedDirection, plannedDistance, chargeWidth * 1.08f);
            telegraph?.SetProgress(elapsed / Mathf.Max(0.01f, chargeWindup));
            visualMotion.SetAction(EnemyVisualMotion.ActionState.Windup, lockedDirection);

            elapsed += Time.deltaTime;
            yield return null;
        }

        attackContract.BeginActive(telegraph);
        state = State.Charge;
        visualMotion.SetAction(EnemyVisualMotion.ActionState.Attack, lockedDirection);

        float movementElapsed = 0f;
        bool hitPlayer = false;
        bool hitWall = false;
        while (movementElapsed < chargeDuration)
        {
            if (ShouldInterrupt())
            {
                InterruptAttack();
                yield break;
            }

            Vector2 previousPosition = body.position;
            float step = plannedDistance / Mathf.Max(0.01f, chargeDuration) * Time.fixedDeltaTime;
            if (!motor.MoveScripted(lockedDirection * step))
            {
                hitWall = true;
                break;
            }

            if (!hitPlayer)
            {
                hitPlayer = attackContract.TryDamageCapsule(
                    previousPosition,
                    body.position,
                    chargeWidth * 0.62f,
                    chargeDamage);
            }

            movementElapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        telegraph?.Hide();
        if (hitWall)
        {
            CameraFeedbackController feedback = CameraFeedbackController.Instance;
            if (feedback != null)
                feedback.Shake(0.11f, 0.09f, lockedDirection);
            yield return new WaitForSeconds(0.20f);
        }

        attackContract.BeginRecovery(telegraph);
        state = State.Recovery;
        visualMotion.SetAction(EnemyVisualMotion.ActionState.Recovery, lockedDirection);
        yield return new WaitForSeconds(Mathf.Max(0.01f, chargeRecovery));
        FinishAttack();
    }

    private bool ShouldInterrupt()
    {
        return GameInputState.IsLocked || motor.IsKnockedBack || (health != null && health.IsDead) || !isActiveAndEnabled;
    }

    private void FinishAttack()
    {
        attackContract.FinishAttack(telegraph);
        motor.SetMovementEnabled(true, true);
        visualMotion.SetAction(EnemyVisualMotion.ActionState.None, Vector2.down);
        state = State.Approach;
        attackRoutine = null;
        ReleaseToken();
    }

    private void InterruptAttack()
    {
        if (attackRoutine != null)
            StopCoroutine(attackRoutine);
        attackRoutine = null;
        attackContract?.CancelAttack(telegraph);
        motor?.SetMovementEnabled(true, true);
        if (visualMotion != null)
            visualMotion.SetAction(EnemyVisualMotion.ActionState.None, Vector2.down);
        ReleaseToken();
    }

    private void ReleaseToken()
    {
        if (!ownsHeavyToken)
            return;
        ownsHeavyToken = false;
        coordinator?.Release(this, EnemyAttackCoordinator.AttackChannel.Heavy);
    }

    private void OnDisable()
    {
        InterruptAttack();
        coordinator?.ReleaseAll(this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, slamRadius);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, chargeMaxRange);
    }
}
