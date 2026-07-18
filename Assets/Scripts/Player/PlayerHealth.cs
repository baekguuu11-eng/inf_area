<<<<<<< HEAD
=======
using System.Collections;
>>>>>>> test2
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
<<<<<<< HEAD
    [Header("체력 설정")]
    public int maxHealth = 5;
    public int currentHealth;

    [Header("하트 UI")]
    public HeartUI heartUI;

    [Header("사망 UI")]
    public GameObject deathPanel;

    [Header("무적 시간")]
    public float invincibleTime = 0.5f;

    private bool isInvincible = false;

    private SpriteRenderer sr;

    void Start()
    {
        // 시작 체력 설정
        currentHealth = maxHealth;

        // 플레이어 스프라이트 가져오기
        sr = GetComponentInChildren<SpriteRenderer>();

        // 하트 UI 업데이트
        heartUI.UpdateHearts(currentHealth);

        // 사망창 숨기기
        deathPanel.SetActive(false);
    }

    void Update()
    {
        // 테스트용 데미지
=======
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
>>>>>>> test2
        if (Input.GetKeyDown(KeyCode.N))
        {
            TakeDamage(1);
        }
    }

    public void TakeDamage(int damage)
    {
<<<<<<< HEAD
        // 무적 상태면 데미지 무시
        if (isInvincible)
            return;

        // 체력 감소
        currentHealth -= damage;

        // 체력 최소값 제한
        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        Debug.Log("현재 체력 : " + currentHealth);

        // 하트 UI 갱신
        heartUI.UpdateHearts(currentHealth);

        // 피격 효과
        StartCoroutine(HitEffect());

        // 무적 시작
        StartCoroutine(InvincibleCoroutine());

        // 사망 체크
=======
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

>>>>>>> test2
        if (currentHealth <= 0)
        {
            Die();
        }
    }

<<<<<<< HEAD
    System.Collections.IEnumerator HitEffect()
    {
        sr.color = Color.red;

        yield return new WaitForSeconds(0.2f);

        sr.color = Color.white;
    }

    void Die()
    {
        Debug.Log("플레이어 사망!");

        deathPanel.SetActive(true);

        Time.timeScale = 0f;
    }

    System.Collections.IEnumerator InvincibleCoroutine()
=======
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
>>>>>>> test2
    {
        isInvincible = true;

        yield return new WaitForSeconds(invincibleTime);

        isInvincible = false;
    }
<<<<<<< HEAD
=======

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
>>>>>>> test2
}