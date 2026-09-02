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
    [SerializeField] private bool showDefenseLog = false;

    [Header("Test")]
    [SerializeField] private int testDamage = 1;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hurtSound;
    [SerializeField] private float hurtVolume = 0.55f;
    [SerializeField] private bool randomizeHurtPitch = true;
    [SerializeField] private Vector2 hurtPitchRange = new Vector2(0.92f, 1.08f);

    [Header("Death Sequence")]
    [SerializeField] private DeathSequenceUI deathSequenceUI;

    private PlayerStats stats;
    private PlayerDashController dashController;
    private bool isInvincible;
    private bool isDead;
    private float damageCarry;
    private float lastDamageTime = -999f;
    private Color originalColor = Color.white;
    private Coroutine hitEffectCoroutine;
    private Coroutine invincibleCoroutine;
    private System.IDisposable deathLock;

    public bool IsDead { get { return isDead; } }
    public bool IsInvincible { get { return isInvincible; } }

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        dashController = GetComponent<PlayerDashController>();
        FindSpriteRendererIfNeeded();

        if (playerSpriteRenderer != null)
            originalColor = playerSpriteRenderer.color;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        Time.timeScale = 1f;
        baseMaxHealth = Mathf.Max(1, baseMaxHealth);
        SyncMaxHealth(true);

        if (currentHealth <= 0)
            currentHealth = maxHealth;

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (deathPanel != null)
            deathPanel.SetActive(false);

        RefreshUI();
    }

    private void Update()
    {
        SyncMaxHealth(false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(KeyCode.N))
            TakeDamage(testDamage);
#endif
    }

    private void SyncMaxHealth(bool force)
    {
        int targetMax = stats != null ? stats.MaxHealth : Mathf.Max(1, baseMaxHealth);
        if (!force && targetMax == maxHealth)
            return;

        int previousMax = Mathf.Max(1, maxHealth);
        maxHealth = Mathf.Max(1, targetMax);
        baseMaxHealth = stats != null ? Mathf.Min(baseMaxHealth, maxHealth) : Mathf.Max(1, baseMaxHealth);

        if (force)
            currentHealth = currentHealth <= 0 ? maxHealth : Mathf.Clamp(currentHealth, 0, maxHealth);
        else if (maxHealth > previousMax)
            currentHealth = Mathf.Min(maxHealth, currentHealth + (maxHealth - previousMax));
        else
            currentHealth = Mathf.Min(currentHealth, maxHealth);

        RefreshUI();
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || isDead || IsInInvincibleTime())
            return;

        int healthBefore = currentHealth;
        int finalDamage = CalculateFinalDamage(damage);

        if (finalDamage <= 0)
        {
            if (heartUI != null)
                heartUI.PlayDefenseFeedback(currentHealth);

            PlayHitEffect();
            StartInvincible();
            return;
        }

        currentHealth = Mathf.Clamp(currentHealth - finalDamage, 0, maxHealth);
        RefreshUI();

        if (heartUI != null)
            heartUI.PlayDamageFeedback(healthBefore, currentHealth);

        PlayHurtSound();
        PlayHitEffect();
        CombatPostProcessV61.PulsePlayerDamage();
        StartInvincible();

        if (currentHealth <= 0)
            Die();
    }

    private int CalculateFinalDamage(int incomingDamage)
    {
        float multiplier = stats != null ? stats.DefenseMultiplier : 1f;
        multiplier = Mathf.Clamp(multiplier, 0.05f, 5f);

        damageCarry += incomingDamage * multiplier;
        int result = Mathf.FloorToInt(damageCarry + 0.0001f);
        damageCarry -= result;

        if (showDefenseLog)
            Debug.Log("[PlayerHealth] incoming=" + incomingDamage + ", multiplier=" + multiplier + ", applied=" + result);

        return result;
    }

    private bool IsInInvincibleTime()
    {
        if (dashController == null)
            dashController = GetComponent<PlayerDashController>();
        return (dashController != null && dashController.IsInvulnerable)
            || isInvincible
            || Time.time - lastDamageTime < invincibleTime;
    }

    public void GrantTemporaryInvulnerability(float duration)
    {
        if (duration <= 0f || isDead)
            return;
        lastDamageTime = Time.time;
        if (invincibleCoroutine != null)
            StopCoroutine(invincibleCoroutine);
        invincibleCoroutine = StartCoroutine(ExternalInvincibleCoroutine(duration));
    }

    private IEnumerator ExternalInvincibleCoroutine(float duration)
    {
        isInvincible = true;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, duration));
        isInvincible = false;
        invincibleCoroutine = null;
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || isDead)
            return;

        currentHealth = Mathf.Clamp(currentHealth + amount, 0, maxHealth);
        RefreshUI();
    }

    public void RestoreToFull()
    {
        if (isDead)
            return;

        SyncMaxHealth(false);
        currentHealth = Mathf.Max(1, maxHealth);
        damageCarry = 0f;
        RefreshUI();

        // 보스 처치 직후 남아 있던 짧은 무적/피격 잔여 상태가 다음 구간으로 넘어가지 않게 정리한다.
        if (invincibleCoroutine != null)
        {
            StopCoroutine(invincibleCoroutine);
            invincibleCoroutine = null;
        }
        isInvincible = false;
        lastDamageTime = -999f;
    }

    public void ApplyOverclockHealth(int bonusAmount)
    {
        if (stats != null)
        {
            stats.SetOverclockHealthBonus(bonusAmount);
            SyncMaxHealth(false);
            return;
        }

        maxHealth = Mathf.Max(1, baseMaxHealth + Mathf.Max(0, bonusAmount));
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0, bonusAmount));
        RefreshUI();
    }

    public void RemoveOverclockHealth()
    {
        if (stats != null)
        {
            stats.SetOverclockHealthBonus(0);
            SyncMaxHealth(false);
            return;
        }

        maxHealth = Mathf.Max(1, baseMaxHealth);
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (heartUI != null)
            heartUI.RefreshUI(currentHealth, maxHealth, Mathf.Min(baseMaxHealth, maxHealth));
    }

    private void PlayHitEffect()
    {
        FindSpriteRendererIfNeeded();
        if (hitEffectCoroutine != null)
            StopCoroutine(hitEffectCoroutine);
        hitEffectCoroutine = StartCoroutine(HitEffect());
    }

    private IEnumerator HitEffect()
    {
        if (playerSpriteRenderer != null)
            playerSpriteRenderer.color = hitColor;

        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, hitFlashTime));

        if (playerSpriteRenderer != null && !isDead)
            playerSpriteRenderer.color = originalColor;

        hitEffectCoroutine = null;
    }

    private void StartInvincible()
    {
        lastDamageTime = Time.time;
        if (invincibleCoroutine != null)
            StopCoroutine(invincibleCoroutine);
        invincibleCoroutine = StartCoroutine(InvincibleCoroutine());
    }

    private IEnumerator InvincibleCoroutine()
    {
        isInvincible = true;
        yield return new WaitForSeconds(Mathf.Max(0.01f, invincibleTime));
        isInvincible = false;
        invincibleCoroutine = null;
    }

    private void PlayHurtSound()
    {
        if (audioSource == null || hurtSound == null)
            return;

        float originalPitch = audioSource.pitch;
        if (randomizeHurtPitch)
            audioSource.pitch = Random.Range(hurtPitchRange.x, hurtPitchRange.y);

        audioSource.PlayOneShot(hurtSound, hurtVolume);
        audioSource.pitch = originalPitch;
    }

    private void FindSpriteRendererIfNeeded()
    {
        if (playerSpriteRenderer != null)
            return;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].sprite != null)
            {
                playerSpriteRenderer = renderers[i];
                return;
            }
        }
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        deathLock = GameInputState.Acquire("PlayerDeath");

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
            body.linearVelocity = Vector2.zero;

        if (deathSequenceUI != null)
        {
            deathSequenceUI.PlayDeathSequence(playerSpriteRenderer);
            return;
        }

        if (deathPanel != null)
            deathPanel.SetActive(true);

        Time.timeScale = 0f;
    }

    private void OnDestroy()
    {
        if (deathLock != null)
        {
            deathLock.Dispose();
            deathLock = null;
        }
    }
}
