using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    [Header("Linked Effects")]
    [SerializeField] private EnemyHitEffect hitEffect;
    [SerializeField] private EnemyDeathEffect deathEffect;

    private int currentHealth;
    private bool isDead = false;
    private Vector2 lastHitDirection = Vector2.down;
    private Transform rootTransform;

    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
        rootTransform = transform.parent != null ? transform.parent : transform;

        if (hitEffect == null)
            hitEffect = rootTransform.GetComponentInChildren<EnemyHitEffect>(true);

        if (deathEffect == null)
            deathEffect = rootTransform.GetComponentInChildren<EnemyDeathEffect>(true);
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

        if (deathEffect != null)
            deathEffect.PlayDeath(rootTransform.position, lastHitDirection);

        Destroy(rootTransform.gameObject);
    }
}