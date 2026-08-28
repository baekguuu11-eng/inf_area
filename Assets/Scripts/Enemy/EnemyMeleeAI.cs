// TARGET: Assets/Scripts/Enemy/EnemyMeleeAI.cs
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyPerception))]
[RequireComponent(typeof(EnemyMotor))]
[DisallowMultipleComponent]
public sealed class EnemyMeleeAI : MonoBehaviour
{
    private enum State
    {
        Observe,
        Approach,
        Reposition,
        Windup,
        Lunge,
        Recovery
    }

    [Header("감지 및 전투 위치")]
    [SerializeField] private float detectRange = 7f;
    [SerializeField] private float combatRingRadius = 0.95f;
    [SerializeField] private float attackStartRange = 1.12f;
    [SerializeField] private float repositionSpeedMultiplier = 0.78f;
    [SerializeField] private float decisionInterval = 0.08f;

    [Header("이동")]
    [SerializeField] private float moveSpeed = 2.45f;

    [Header("짧은 돌진 공격")]
    [SerializeField] private float attackWindup = 0.30f;
    [SerializeField] private float lungeDuration = 0.12f;
    [SerializeField] private float lungeDistance = 0.76f;
    [SerializeField] private float attackHitRadius = 0.68f;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private float recoveryDuration = 0.44f;
    [SerializeField] private float telegraphWidth = 0.28f;

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
    private bool ownsAttackToken;
    private int orbitSign;

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
        orbitSign = Random.value < 0.5f ? -1 : 1;
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
            state = State.Observe;
            motor.StopIntent();
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

        nextDecisionTime = Time.time + Mathf.Max(0.03f, decisionInterval);
        perception.RefreshNow();
        if (!perception.HasPlayer || perception.DistanceToPlayer > detectRange)
        {
            state = State.Observe;
            motor.StopIntent();
            visualMotion.SetAction(EnemyVisualMotion.ActionState.None, Vector2.down);
            return;
        }

        Vector2 toPlayer = perception.PlayerPosition - body.position;
        float distance = toPlayer.magnitude;
        Vector2 slotDirection = coordinator.GetMeleeSlotDirection(this);
        Vector2 slotPoint = perception.PlayerPosition + slotDirection * combatRingRadius;
        Vector2 toSlot = slotPoint - body.position;

        if (distance <= attackStartRange && perception.HasLineOfSight() &&
            coordinator.TryAcquire(this, EnemyAttackCoordinator.AttackChannel.Melee, attackWindup + lungeDuration + recoveryDuration + 1f))
        {
            ownsAttackToken = true;
            attackRoutine = StartCoroutine(AttackRoutine());
            return;
        }

        if (toSlot.magnitude > 0.38f)
        {
            state = State.Approach;
            motor.SetMoveIntent(toSlot, moveSpeed);
            visualMotion.SetAction(EnemyVisualMotion.ActionState.None, toSlot);
        }
        else
        {
            state = State.Reposition;
            Vector2 tangent = EnemyAIUtility.Perpendicular(toPlayer, orbitSign);
            Vector2 radialCorrection = slotPoint - body.position;
            Vector2 desired = (tangent * 0.72f + radialCorrection.normalized * 0.55f).normalized;
            motor.SetMoveIntent(desired, moveSpeed * repositionSpeedMultiplier);
            visualMotion.SetAction(EnemyVisualMotion.ActionState.None, desired);
        }
    }

    private IEnumerator AttackRoutine()
    {
        state = State.Windup;
        attackContract.BeginTelegraph(telegraph);
        motor.SetMovementEnabled(false, true);
        Vector2 lockedDirection = perception.DirectionToPlayer;

        if (telegraph != null)
            telegraph.ShowLine(lockedDirection, lungeDistance + attackHitRadius * 0.85f, telegraphWidth);
        visualMotion.SetAction(EnemyVisualMotion.ActionState.Windup, lockedDirection);

        float elapsed = 0f;
        while (elapsed < attackWindup)
        {
            if (ShouldInterrupt())
            {
                InterruptAttack();
                yield break;
            }

            elapsed += Time.deltaTime;
            telegraph?.SetProgress(elapsed / Mathf.Max(0.01f, attackWindup));
            yield return null;
        }

        attackContract.BeginActive(telegraph);
        state = State.Lunge;
        visualMotion.SetAction(EnemyVisualMotion.ActionState.Attack, lockedDirection);

        float movementElapsed = 0f;
        bool damaged = false;
        while (movementElapsed < lungeDuration)
        {
            if (ShouldInterrupt())
            {
                InterruptAttack();
                yield break;
            }

            float step = lungeDistance / Mathf.Max(0.01f, lungeDuration) * Time.fixedDeltaTime;
            if (!motor.MoveScripted(lockedDirection * step))
                break;

            if (!damaged)
                damaged = attackContract.TryDamageCircle(body.position + lockedDirection * 0.06f, attackHitRadius, attackDamage);

            movementElapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        attackContract.BeginRecovery(telegraph);
        state = State.Recovery;
        visualMotion.SetAction(EnemyVisualMotion.ActionState.Recovery, lockedDirection);
        yield return new WaitForSeconds(Mathf.Max(0.01f, recoveryDuration));
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
        state = State.Reposition;
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
        if (!ownsAttackToken)
            return;
        ownsAttackToken = false;
        coordinator?.Release(this, EnemyAttackCoordinator.AttackChannel.Melee);
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
        Gizmos.DrawWireSphere(transform.position, attackStartRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackHitRadius);
    }
}
