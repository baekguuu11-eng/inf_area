using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    [Header("Linked Effects")]
    [SerializeField] private EnemyHitEffect hitEffect;
    [SerializeField] private EnemyDeathEffect deathEffect;

    [Header("Root Override (선택사항)")]
    [Tooltip("이 적의 비주얼+로직 전체를 감싸는 별도의 상위 래퍼 오브젝트가 있는 특수한 구조일 때만 지정. " +
             "비워두면 이 오브젝트 자기 자신을 기준으로 삼음 (대부분의 평범한 적 프리팹은 이게 맞음)")]
    [SerializeField] private Transform rootOverride;

    [Header("Optional Animator Hook")]
    [SerializeField] private Animator animator;
    [SerializeField] private string deathTriggerName = "Death";

    [Tooltip("사망 애니메이션을 재생할 시간을 주고 싶으면 0보다 큰 값으로 설정 (기본 0 = 즉시 삭제)")]
    [SerializeField] private float deathDestroyDelay = 0f;

    private int currentHealth;
    private bool isDead = false;
    private Vector2 lastHitDirection = Vector2.down;
    private Transform rootTransform;

    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;

        rootTransform = rootOverride != null ? rootOverride : transform;

        // [안전장치] 절대로 RoomController(방)를 파괴 대상으로 잡으면 안 됨.
        // 혹시라도 잘못 설정되어 있으면 강제로 자기 자신으로 되돌리고 크게 경고함.
        // (이 체크 덕분에 설정 실수가 있어도 최소한 방 전체가 파괴되는 참사는 절대 발생하지 않음)
        if (rootTransform.GetComponent<RoomController>() != null)
        {
            Debug.LogError(
                $"[{name}] rootTransform이 RoomController(방)를 가리키고 있어 강제로 자기 자신으로 재설정했습니다. " +
                "Root Override 필드나 부모 구조를 확인하세요."
            );
            rootTransform = transform;
        }

        if (hitEffect == null)
            hitEffect = rootTransform.GetComponentInChildren<EnemyHitEffect>(true);

        if (deathEffect == null)
            deathEffect = rootTransform.GetComponentInChildren<EnemyDeathEffect>(true);

        if (animator == null)
            animator = GetComponent<Animator>();
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, Vector2.zero);
    }

    public void TakeDamage(int damage, Vector2 hitDirection)
    {
        if (isDead)
            return;

        if (hitDirection != Vector2.zero)
            lastHitDirection = hitDirection.normalized;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        if (hitEffect != null)
            hitEffect.PlayHit();
    }

    private void Die()
    {
        isDead = true;

        Collider2D[] colliders = rootTransform.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody2D[] rigidbodies = rootTransform.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
            rigidbodies[i].linearVelocity = Vector2.zero;

        if (deathEffect != null)
            deathEffect.PlayDeath(rootTransform.position, lastHitDirection);

        if (animator != null && !string.IsNullOrEmpty(deathTriggerName))
            animator.SetTrigger(deathTriggerName);

        if (deathDestroyDelay > 0f)
            Destroy(rootTransform.gameObject, deathDestroyDelay);
        else
            Destroy(rootTransform.gameObject);
    }
}
