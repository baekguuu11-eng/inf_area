using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int baseMaxHealth = 5;
    public int maxHealth = 5;
    public int currentHealth = 5;

    [Header("UI")]
    public HeartUI heartUI;

    [Header("Death UI")]
    public GameObject deathPanel;

    [Header("Hit / Invincible")]
    public float invincibleTime = 0.5f;
    public float hitFlashTime = 0.2f;

    private bool isInvincible;
    private bool isDead;

    private SpriteRenderer spriteRenderer;
    private Color originalColor = Color.white;

    public bool IsDead => isDead;
    public bool IsInvincible => isInvincible;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    private void Start()
    {
        baseMaxHealth = Mathf.Max(1, baseMaxHealth);

        maxHealth = baseMaxHealth;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (currentHealth <= 0)
        {
            currentHealth = baseMaxHealth;
        }

        if (deathPanel != null)
        {
            deathPanel.SetActive(false);
        }

        RefreshUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            TakeDamage(1);
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        if (isDead)
        {
            return;
        }

        if (isInvincible)
        {
            return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        RefreshUI();

        StopCoroutine(nameof(HitEffect));
        StartCoroutine(nameof(HitEffect));

        StopCoroutine(nameof(InvincibleCoroutine));
        StartCoroutine(nameof(InvincibleCoroutine));

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (isDead)
        {
            return;
        }

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        RefreshUI();
    }

    public void ApplyOverclockHealth(int bonusAmount)
    {
        if (bonusAmount <= 0)
        {
            return;
        }

        if (isDead)
        {
            return;
        }

        maxHealth = baseMaxHealth + bonusAmount;

        currentHealth += bonusAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        RefreshUI();
    }

    public void RemoveOverclockHealth()
    {
        maxHealth = baseMaxHealth;

        if (currentHealth > baseMaxHealth)
        {
            currentHealth = baseMaxHealth;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        RefreshUI();
    }

    public void RefreshUI()
    {
        if (heartUI != null)
        {
            heartUI.RefreshUI(currentHealth, maxHealth, baseMaxHealth);
        }
    }

    private IEnumerator HitEffect()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
        }

        yield return new WaitForSeconds(hitFlashTime);

        if (spriteRenderer != null && !isDead)
        {
            spriteRenderer.color = originalColor;
        }
    }

    private IEnumerator InvincibleCoroutine()
    {
        isInvincible = true;

        yield return new WaitForSeconds(invincibleTime);

        isInvincible = false;
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        Debug.Log("Player Dead!");

        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
        }

        Time.timeScale = 0f;
    }
}