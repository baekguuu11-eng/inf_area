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
    public float invincibleTime = 0.6f;
    public float hitFlashTime = 0.2f;

    [Header("Hit Visual")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private Color hitColor = Color.red;

    [Header("Defense Chip")]
    [SerializeField] private bool showDefenseLog = true;

    [Header("Test")]
    [SerializeField] private int testDamage = 1;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private float hurtVolume = 0.55f;
    [SerializeField] private bool randomizeHurtPitch = true;
    [SerializeField] private Vector2 hurtPitchRange = new Vector2(0.92f, 1.08f);

    private bool isInvincible;
    private bool isDead;

    private int defenseStoredDamage = 0;
    private float lastDamageTime = -999f;

    private Color originalColor = Color.white;
    private Coroutine hitEffectCoroutine;
    private Coroutine invincibleCoroutine;

    public bool IsDead => isDead;
    public bool IsInvincible => isInvincible;

    private void Awake()
    {
        FindSpriteRendererIfNeeded();

        if (playerSpriteRenderer != null)
        {
            originalColor = playerSpriteRenderer.color;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        baseMaxHealth = Mathf.Max(1, baseMaxHealth);
        maxHealth = baseMaxHealth;

        if (currentHealth <= 0)
        {
            currentHealth = baseMaxHealth;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

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
            TakeDamage(testDamage);
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

        // 핵심: 무적시간 중이면 데미지 자체를 완전히 무시
        if (IsInInvincibleTime())
        {
            Debug.Log("무적시간 중: 데미지 무시");

            if (heartUI != null)
            {
                heartUI.PlayDefenseFeedback(currentHealth);
            }

            return;
        }

        int healthBefore = currentHealth;
        int finalDamage = GetFinalDamage(damage);

        // 방어칩 때문에 이번 공격이 막힌 경우
        if (finalDamage <= 0)
        {
            Debug.Log("방어 칩 적용: 이번 공격은 하트가 닳지 않음");

            if (heartUI != null)
            {
                heartUI.PlayDefenseFeedback(currentHealth);
            }

            PlayHitEffect();
            StartInvincible();

            return;
        }

        currentHealth -= finalDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log("현재 체력 : " + currentHealth + " / 받은 데미지 : " + finalDamage);

        RefreshUI();

        if (heartUI != null)
        {
            heartUI.PlayDamageFeedback(healthBefore, currentHealth);
        }

        PlayHurtSound();
        PlayHitEffect();
        StartInvincible();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private bool IsInInvincibleTime()
    {
        if (isInvincible)
        {
            return true;
        }

        if (Time.time - lastDamageTime < invincibleTime)
        {
            return true;
        }

        return false;
    }

    private int GetFinalDamage(int damage)
    {
        if (!IsDefenseChipEquipped())
        {
            defenseStoredDamage = 0;
            return damage;
        }

        defenseStoredDamage += damage;

        int finalDamage = defenseStoredDamage / 2;
        defenseStoredDamage = defenseStoredDamage % 2;

        if (showDefenseLog)
        {
            Debug.Log("방어 칩 누적 데미지 처리 / 이번 최종 데미지: " + finalDamage);
        }

        return finalDamage;
    }

    private bool IsDefenseChipEquipped()
    {
        if (ChipSlotManager.Instance == null)
        {
            return false;
        }

        return ChipSlotManager.Instance.IsChipEquipped(ChipSlotManager.ChipType.Defense);
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

    private void PlayHitEffect()
    {
        FindSpriteRendererIfNeeded();

        if (hitEffectCoroutine != null)
        {
            StopCoroutine(hitEffectCoroutine);
        }

        hitEffectCoroutine = StartCoroutine(HitEffect());
    }

    private IEnumerator HitEffect()
    {
        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.color = hitColor;
        }

        yield return new WaitForSeconds(hitFlashTime);

        if (playerSpriteRenderer != null && !isDead)
        {
            playerSpriteRenderer.color = originalColor;
        }

        hitEffectCoroutine = null;
    }

    private void StartInvincible()
    {
        lastDamageTime = Time.time;

        if (invincibleCoroutine != null)
        {
            StopCoroutine(invincibleCoroutine);
        }

        invincibleCoroutine = StartCoroutine(InvincibleCoroutine());
    }

    private IEnumerator InvincibleCoroutine()
    {
        isInvincible = true;

        yield return new WaitForSeconds(invincibleTime);

        isInvincible = false;
        invincibleCoroutine = null;
    }

    private void PlayHurtSound()
    {
        if (audioSource == null)
        {
            return;
        }

        if (hurtSound == null)
        {
            return;
        }

        float originalPitch = audioSource.pitch;

        if (randomizeHurtPitch)
        {
            audioSource.pitch = Random.Range(hurtPitchRange.x, hurtPitchRange.y);
        }

        audioSource.PlayOneShot(hurtSound, hurtVolume);

        audioSource.pitch = originalPitch;
    }

    private void FindSpriteRendererIfNeeded()
    {
        if (playerSpriteRenderer != null)
        {
            return;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            if (renderers[i].sprite != null)
            {
                playerSpriteRenderer = renderers[i];
                return;
            }
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        Debug.Log("플레이어 사망!");

        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
        }

        Time.timeScale = 0f;
    }
}