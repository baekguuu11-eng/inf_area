using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("체력 설정")]
    public int maxHealth = 5;

    private int currentHealth;

    [Header("체력바")]
    public Image healthBar;

    private SpriteRenderer spriteRenderer;

    void Start()
    {
        currentHealth = maxHealth;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        UpdateHealthBar();
    }

    void Update()
    {
        // N키 누르면 데미지 테스트
        if (Input.GetKeyDown(KeyCode.N))
        {
            TakeDamage(1);
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        // 체력이 0 밑으로 안 내려가게
        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        Debug.Log("현재 체력 : " + currentHealth);

        // 체력바 업데이트
        UpdateHealthBar();

        // 피격 이펙트 실행
        StartCoroutine(DamageEffect());
    }

    void UpdateHealthBar()
    {
        healthBar.fillAmount =
            (float)currentHealth / maxHealth;
    }

    IEnumerator DamageEffect()
    {
        spriteRenderer.color = Color.red;

        yield return new WaitForSeconds(0.2f);

        spriteRenderer.color = Color.white;
    }
}