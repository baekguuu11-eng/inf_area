using System.Collections;
using UnityEngine;

public class OverclockChip : MonoBehaviour
{
    [Header("Player")]
    public PlayerHealth playerHealth;

    [Header("Buff Icon")]
    public GameObject buffIcon;

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.F;

    [Header("Overclock Settings")]
    public int bonusHealthAmount = 3;
    public float heartDelay = 0.15f;

    private bool isOverclockActive;
    private Coroutine overclockCoroutine;

    public bool IsOverclockActive => isOverclockActive;

    private void Awake()
    {
        FindPlayerHealthIfNeeded();
    }

    private void Start()
    {
        if (buffIcon != null)
        {
            buffIcon.SetActive(false);
        }

        if (playerHealth != null && playerHealth.heartUI != null)
        {
            playerHealth.heartUI.HideBonusHearts();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleOverclock();
        }
    }

    public void ToggleOverclock()
    {
        FindPlayerHealthIfNeeded();

        if (playerHealth == null)
        {
            Debug.LogWarning("OverclockChip: PlayerHealth를 찾을 수 없습니다.");
            return;
        }

        if (playerHealth.IsDead)
        {
            return;
        }

        if (isOverclockActive)
        {
            DisableOverclock();
        }
        else
        {
            EnableOverclock();
        }
    }

    public void EnableOverclock()
    {
        if (isOverclockActive)
        {
            return;
        }

        if (overclockCoroutine != null)
        {
            StopCoroutine(overclockCoroutine);
        }

        overclockCoroutine = StartCoroutine(EnableOverclockRoutine());
    }

    public void DisableOverclock()
    {
        if (!isOverclockActive && overclockCoroutine == null)
        {
            return;
        }

        if (overclockCoroutine != null)
        {
            StopCoroutine(overclockCoroutine);
            overclockCoroutine = null;
        }

        isOverclockActive = false;

        if (buffIcon != null)
        {
            buffIcon.SetActive(false);
        }

        if (playerHealth != null)
        {
            playerHealth.RemoveOverclockHealth();

            if (playerHealth.heartUI != null)
            {
                playerHealth.heartUI.HideBonusHearts();
            }

            playerHealth.RefreshUI();
        }
    }

    private IEnumerator EnableOverclockRoutine()
    {
        FindPlayerHealthIfNeeded();

        if (playerHealth == null)
        {
            yield break;
        }

        isOverclockActive = true;

        if (buffIcon != null)
        {
            buffIcon.SetActive(true);
        }

        HeartUI heartUI = playerHealth.heartUI;

        if (heartUI != null)
        {
            heartUI.PrepareBonusHeartAnimation(bonusHealthAmount);
        }

        playerHealth.ApplyOverclockHealth(bonusHealthAmount);

        if (heartUI != null)
        {
            int visibleHeartCount = Mathf.Min(bonusHealthAmount, heartUI.BonusHeartCount);

            for (int i = 0; i < visibleHeartCount; i++)
            {
                if (!isOverclockActive)
                {
                    yield break;
                }

                heartUI.ShowBonusHeart(i);

                if (i < visibleHeartCount - 1)
                {
                    yield return new WaitForSeconds(heartDelay);
                }
            }
        }

        overclockCoroutine = null;
    }

    private void FindPlayerHealthIfNeeded()
    {
        if (playerHealth != null)
        {
            return;
        }

#if UNITY_2023_1_OR_NEWER
        playerHealth = FindAnyObjectByType<PlayerHealth>();
#else
        playerHealth = FindObjectOfType<PlayerHealth>();
#endif
    }
}