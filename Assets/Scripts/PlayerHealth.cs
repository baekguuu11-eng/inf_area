using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("체력 설정")]
    public int maxHealth = 5;
    public int currentHealth;

    [Header("하트 UI")]
    public HeartUI heartUI;

    [Header("사망 UI")]
    public GameObject deathPanel;

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
        if (Input.GetKeyDown(KeyCode.N))
        {
            TakeDamage(1);
        }
    }

    public void TakeDamage(int damage)
    {
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
        StopAllCoroutines();
        StartCoroutine(HitEffect());

        // 사망 체크
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    System.Collections.IEnumerator HitEffect()
    {
        // 빨간색 피격 효과
        sr.color = Color.red;

        yield return new WaitForSeconds(0.2f);

        // 원래 색상 복귀
        sr.color = Color.white;
    }

    void Die()
    {
        Debug.Log("플레이어 사망!");

        // 사망 UI 표시
        deathPanel.SetActive(true);

        // 게임 정지
        Time.timeScale = 0f;
    }
}