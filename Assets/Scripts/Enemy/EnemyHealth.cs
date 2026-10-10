// TARGET: Assets/Scripts/Enemy/EnemyHealth.cs
using System;
using UnityEngine;

public interface IEnemyDeathOverride
{
    bool HandleRequestedDeath(EnemyHealth health, Vector2 hitDirection, EnemyHitKind hitKind);
}

[Serializable]
public struct DamageContext
{
    public int Damage;
    public Vector2 Direction;
    public Vector2 HitPoint;
    public EnemyHitKind HitKind;
    public float KnockbackBonus;
    public bool ChipSecondary;

    public DamageContext(int damage, Vector2 direction, Vector2 hitPoint, EnemyHitKind hitKind, float knockbackBonus = 0f)
    {
        ChipSecondary = false;
        Damage = damage;
        Direction = direction;
        HitPoint = hitPoint;
        HitKind = hitKind;
        KnockbackBonus = knockbackBonus;
    }

    public static DamageContext Simple(int damage, Vector2 direction, EnemyHitKind hitKind, Vector2 fallbackPoint)
    {
        return new DamageContext(damage, direction, fallbackPoint, hitKind, 0f);
    }
}

[DisallowMultipleComponent]
public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    [Header("Linked Effects")]
    [SerializeField] private EnemyCombatFeedback combatFeedback;
    [SerializeField] private EnemyDeathEffect deathEffect;

    [Header("Root Override")]
    [SerializeField] private Transform rootOverride;

    [Header("Optional Animator Hook")]
    [SerializeField] private Animator animator;
    [SerializeField] private string deathTriggerName = "Death";
    [SerializeField] private float deathDestroyDelay = 0f;

    private int currentHealth;
    private bool isDead;
    private bool deathPending;
    private DamageContext lastDamageContext;
    private Transform rootTransform;
    private ByteDropper byteDropper;
    private BossOpeningWindowV15 bossOpeningV15;
    private bool resolvedBossOpeningV15;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead || deathPending;
    public float NormalizedHealth => maxHealth > 0 ? currentHealth / (float)maxHealth : 0f;
    public DamageContext LastDamageContext => lastDamageContext;

    public event Action<EnemyHealth, int, Vector2> Damaged;
    public event Action<EnemyHealth> Died;

    private void Awake()
    {
        ResolveReferences();
        ResetHealth();
    }

    private void ResolveReferences()
    {
        rootTransform = rootOverride != null ? rootOverride : transform;
        if (rootTransform.GetComponent<RoomController>() != null)
        {
            Debug.LogError("[EnemyHealth] Root Override가 방을 가리켜 자기 자신으로 복구했습니다: " + name);
            rootTransform = transform;
        }

        if (combatFeedback == null)
            combatFeedback = rootTransform.GetComponentInChildren<EnemyCombatFeedback>(true);
        if (deathEffect == null)
            deathEffect = rootTransform.GetComponentInChildren<EnemyDeathEffect>(true);
        if (animator == null)
            animator = rootTransform.GetComponentInChildren<Animator>(true);
        byteDropper = rootTransform.GetComponentInChildren<ByteDropper>(true);
    }

    public void ResetHealth()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
        isDead = false;
        deathPending = false;
        lastDamageContext = DamageContext.Simple(0, Vector2.down, EnemyHitKind.Environment, (Vector2)transform.position);
    }

    public void SetMaxHealth(int value, bool healToFull = true)
    {
        maxHealth = Mathf.Max(1, value);
        currentHealth = healToFull ? maxHealth : Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(DamageContext.Simple(damage, Vector2.zero, EnemyHitKind.Melee, (Vector2)transform.position));
    }

    public void TakeDamage(int damage, Vector2 hitDirection)
    {
        TakeDamage(DamageContext.Simple(damage, hitDirection, EnemyHitKind.Melee, (Vector2)transform.position));
    }

    public void TakeDamage(int damage, Vector2 hitDirection, EnemyHitKind hitKind)
    {
        TakeDamage(DamageContext.Simple(damage, hitDirection, hitKind, (Vector2)transform.position));
    }

    public void TakeDamage(DamageContext context) { TryTakeDamage(context); }

    public bool TryTakeDamage(DamageContext context)
    {
        if (isDead || deathPending || context.Damage <= 0)
            return false;
        JHLCombatV25 jhl = GetComponent<JHLCombatV25>();
        if (jhl != null && !jhl.FilterDamage(ref context)) return false;
        ChernobylBossController reactor = GetComponent<ChernobylBossController>();
        if (reactor != null && !reactor.AllowDamageV19(context)) return false;

        if (context.Direction.sqrMagnitude <= 0.0001f)
            context.Direction = Vector2.down;
        else
            context.Direction.Normalize();
        if (context.HitPoint == Vector2.zero)
            context.HitPoint = transform.position;

        // Only the two revised bosses opt into this component. Ordinary enemies are unchanged.
        if (!resolvedBossOpeningV15)
        {
            bossOpeningV15 = GetComponent<BossOpeningWindowV15>();
            resolvedBossOpeningV15 = true;
        }
        if (bossOpeningV15 != null)
            context.Damage = bossOpeningV15.ResolveDamage(context.Damage, context.HitKind);
        lastDamageContext = context;
        int appliedDamage = Mathf.Min(context.Damage, currentHealth);
        currentHealth -= appliedDamage;
        Damaged?.Invoke(this, appliedDamage, context.Direction);

        if(ChipSlotManager.Instance!=null) ChipSlotManager.Instance.EnemyHitV22(this,context,IsMajorBossRoot());
        bool lethal = currentHealth <= 0;
        if (combatFeedback == null)
            combatFeedback = rootTransform.GetComponentInChildren<EnemyCombatFeedback>(true);
        if (combatFeedback != null)
            combatFeedback.PlayHit(context, lethal);

        CombatPostProcessV61.PulseImpact(context.HitKind, lethal);

        if (lethal)
            RequestDeath();
        return true;
    }

    private void RequestDeath()
    {
        if (isDead || deathPending)
            return;

        MonoBehaviour[] behaviours = rootTransform.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            IEnemyDeathOverride deathOverride = behaviours[i] as IEnemyDeathOverride;
            if (deathOverride != null && deathOverride.HandleRequestedDeath(this, lastDamageContext.Direction, lastDamageContext.HitKind))
            {
                deathPending = true;
                return;
            }
        }

        CompleteDeath(lastDamageContext);
    }

    public void ForceDeath(Vector2 hitDirection)
    {
        if (isDead)
            return;
        DamageContext context = lastDamageContext;
        context.Damage = Mathf.Max(1, currentHealth);
        context.Direction = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : lastDamageContext.Direction;
        context.HitPoint = transform.position;
        currentHealth = 0;
        deathPending = false;
        CompleteDeath(context);
    }

    public void CompleteDeferredDeath(Vector2 hitDirection)
    {
        if (isDead)
            return;
        DamageContext context = lastDamageContext;
        if (hitDirection.sqrMagnitude > 0.0001f)
            context.Direction = hitDirection.normalized;
        context.HitPoint = transform.position;
        currentHealth = 0;
        deathPending = false;
        CompleteDeath(context);
    }

    private void CompleteDeath(DamageContext context)
    {
        if (isDead)
            return;

        isDead = true;
        deathPending = false;
        if(ChipSlotManager.Instance!=null) ChipSlotManager.Instance.EnemyKilledV22(this,context,IsMajorBossRoot());
        DisableCombatComponents(true);

        ChernobylGuardUnit chernobylGuard = rootTransform.GetComponent<ChernobylGuardUnit>();
        if (chernobylGuard != null)
            chernobylGuard.ScheduleDeathBurst(context.Direction);

        if (byteDropper != null)
            byteDropper.DropBytes(rootTransform.position);
        if (deathEffect == null)
            deathEffect = rootTransform.GetComponentInChildren<EnemyDeathEffect>(true);
        if (deathEffect != null)
            deathEffect.PlayDeath(rootTransform.position, context);
        if (HasAnimatorTrigger(animator, deathTriggerName))
            animator.SetTrigger(deathTriggerName);

        if (!IsMajorBossRoot())
            TeamGameSFX.PlayEnemyDeath();

        Died?.Invoke(this);

        if (deathDestroyDelay > 0f)
            Destroy(rootTransform.gameObject, deathDestroyDelay);
        else
            Destroy(rootTransform.gameObject);
    }

    private bool IsMajorBossRoot()
    {
        if (rootTransform == null) return false;
        return rootTransform.GetComponentInChildren<ExecutorBossController>(true) != null
            || rootTransform.GetComponentInChildren<ChernobylBossController>(true) != null
            || rootTransform.GetComponentInChildren<JHLBossController>(true) != null
            || rootTransform.GetComponentInChildren<MachineWormBossController>(true) != null;
    }

    private static bool HasAnimatorTrigger(Animator targetAnimator, string parameterName)
    {
        if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName) || targetAnimator.runtimeAnimatorController == null)
            return false;

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
                return true;
        }
        return false;
    }

    private void DisableCombatComponents(bool disableColliders)
    {
        if (disableColliders)
        {
            Collider2D[] colliders = rootTransform.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }

        Rigidbody2D[] rigidbodies = rootTransform.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].linearVelocity = Vector2.zero;
            rigidbodies[i].angularVelocity = 0f;
        }

        EnemyAttackCoordinator coordinator = EnemyAttackCoordinator.Instance;
        MonoBehaviour[] behaviours = rootTransform.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this || behaviour is EnemyDeathEffect || behaviour is EnemyCombatFeedback || behaviour is EnemyVisualMotion)
                continue;

            if (coordinator != null)
                coordinator.ReleaseAll(behaviour);

            string typeName = behaviour.GetType().Name;
            if (typeName.EndsWith("AI", StringComparison.Ordinal) || typeName == "EnemyDamage" || typeName == "EnemyMotor" || typeName == "EnemyPerception")
                behaviour.enabled = false;
        }
    }
}
