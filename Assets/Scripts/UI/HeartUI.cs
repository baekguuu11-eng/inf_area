using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HeartUI : MonoBehaviour
{
    [Header("Base Hearts")]
    public Image[] hearts;

    [Header("Bonus Hearts")]
    public Image[] bonusHearts;

    [Header("Heart Sprites")]
    public Sprite fullHeart;
    public Sprite emptyHeart;

    [Header("Option")]
    [SerializeField] private bool sortByXPosition = true;

    [Header("Heart Feedback Animation")]
    [SerializeField] private float feedbackScale = 1.25f;
    [SerializeField] private float feedbackDuration = 0.12f;
    [SerializeField] private Color damageFeedbackColor = new Color(1f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color defenseFeedbackColor = new Color(0.45f, 0.8f, 1f, 1f);

    private int currentHealth;
    private int maxHealth;
    private int baseMaxHealth = 5;

    private bool bonusHeartMode;
    private bool[] bonusHeartVisible;

    private Coroutine feedbackCoroutine;

    public int BonusHeartCount
    {
        get
        {
            if (bonusHearts == null)
            {
                return 0;
            }

            return bonusHearts.Length;
        }
    }

    private void Awake()
    {
        NormalizeHeartArrays();
        EnsureBonusVisibleArray();
        HideBonusHearts();
        DrawHearts();
    }

    public void UpdateHearts(int newCurrentHealth)
    {
        int detectedBaseMaxHealth = 5;

        if (hearts != null && hearts.Length > 0)
        {
            detectedBaseMaxHealth = hearts.Length;
        }

        RefreshUI(newCurrentHealth, detectedBaseMaxHealth, detectedBaseMaxHealth);
    }

    public void RefreshUI(int newCurrentHealth, int newMaxHealth)
    {
        int detectedBaseMaxHealth = 5;

        if (hearts != null && hearts.Length > 0)
        {
            detectedBaseMaxHealth = hearts.Length;
        }

        RefreshUI(newCurrentHealth, newMaxHealth, detectedBaseMaxHealth);
    }

    public void RefreshUI(int newCurrentHealth, int newMaxHealth, int newBaseMaxHealth)
    {
        baseMaxHealth = Mathf.Max(1, newBaseMaxHealth);
        maxHealth = Mathf.Max(baseMaxHealth, newMaxHealth);
        currentHealth = Mathf.Clamp(newCurrentHealth, 0, maxHealth);

        bonusHeartMode = maxHealth > baseMaxHealth;

        if (!bonusHeartMode)
        {
            HideBonusHearts();
        }

        DrawHearts();
    }

    public void PlayDamageFeedback(int healthBefore, int healthAfter)
    {
        int changedHeartIndex = Mathf.Clamp(healthAfter, 0, maxHealth - 1);
        Image targetHeart = GetHeartImageByGlobalIndex(changedHeartIndex);

        PlayFeedback(targetHeart, damageFeedbackColor);
    }

    public void PlayDefenseFeedback(int currentHealthValue)
    {
        int targetIndex = Mathf.Clamp(currentHealthValue - 1, 0, maxHealth - 1);
        Image targetHeart = GetHeartImageByGlobalIndex(targetIndex);

        PlayFeedback(targetHeart, defenseFeedbackColor);
    }

    public void PrepareBonusHeartAnimation(int bonusSlotCount)
    {
        EnsureBonusVisibleArray();

        bonusHeartMode = true;

        for (int i = 0; i < bonusHeartVisible.Length; i++)
        {
            bonusHeartVisible[i] = false;
        }

        if (bonusHearts == null)
        {
            return;
        }

        for (int i = 0; i < bonusHearts.Length; i++)
        {
            if (bonusHearts[i] != null)
            {
                bonusHearts[i].gameObject.SetActive(false);
            }
        }

        DrawHearts();
    }

    public void ShowBonusHeart(int index)
    {
        if (bonusHearts == null)
        {
            return;
        }

        if (index < 0 || index >= bonusHearts.Length)
        {
            return;
        }

        EnsureBonusVisibleArray();

        bonusHeartMode = true;
        bonusHeartVisible[index] = true;

        DrawHearts();
    }

    public void ShowAllBonusHearts()
    {
        EnsureBonusVisibleArray();

        bonusHeartMode = true;

        for (int i = 0; i < bonusHeartVisible.Length; i++)
        {
            bonusHeartVisible[i] = true;
        }

        DrawHearts();
    }

    public void HideBonusHearts()
    {
        EnsureBonusVisibleArray();

        bonusHeartMode = false;

        for (int i = 0; i < bonusHeartVisible.Length; i++)
        {
            bonusHeartVisible[i] = false;
        }

        if (bonusHearts == null)
        {
            return;
        }

        for (int i = 0; i < bonusHearts.Length; i++)
        {
            if (bonusHearts[i] != null)
            {
                bonusHearts[i].gameObject.SetActive(false);
            }
        }
    }

    private void DrawHearts()
    {
        DrawBaseHearts();
        DrawBonusHearts();
    }

    private void DrawBaseHearts()
    {
        if (hearts == null)
        {
            return;
        }

        for (int i = 0; i < hearts.Length; i++)
        {
            Image heart = hearts[i];

            if (heart == null)
            {
                continue;
            }

            bool shouldShow = i < baseMaxHealth;
            heart.gameObject.SetActive(shouldShow);

            if (!shouldShow)
            {
                continue;
            }

            heart.sprite = currentHealth > i ? fullHeart : emptyHeart;
        }
    }

    private void DrawBonusHearts()
    {
        if (bonusHearts == null)
        {
            return;
        }

        EnsureBonusVisibleArray();

        int bonusSlotCount = Mathf.Clamp(maxHealth - baseMaxHealth, 0, bonusHearts.Length);

        for (int i = 0; i < bonusHearts.Length; i++)
        {
            Image heart = bonusHearts[i];

            if (heart == null)
            {
                continue;
            }

            bool shouldShow = bonusHeartMode &&
                              i < bonusSlotCount &&
                              i < bonusHeartVisible.Length &&
                              bonusHeartVisible[i];

            heart.gameObject.SetActive(shouldShow);

            if (!shouldShow)
            {
                continue;
            }

            int globalHeartIndex = baseMaxHealth + i;
            heart.sprite = currentHealth > globalHeartIndex ? fullHeart : emptyHeart;
        }
    }

    private Image GetHeartImageByGlobalIndex(int index)
    {
        if (index < baseMaxHealth)
        {
            if (hearts == null)
            {
                return null;
            }

            if (index < 0 || index >= hearts.Length)
            {
                return null;
            }

            return hearts[index];
        }

        int bonusIndex = index - baseMaxHealth;

        if (bonusHearts == null)
        {
            return null;
        }

        if (bonusIndex < 0 || bonusIndex >= bonusHearts.Length)
        {
            return null;
        }

        return bonusHearts[bonusIndex];
    }

    private void PlayFeedback(Image targetHeart, Color feedbackColor)
    {
        if (targetHeart == null)
        {
            return;
        }

        if (!targetHeart.gameObject.activeInHierarchy)
        {
            return;
        }

        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
        }

        feedbackCoroutine = StartCoroutine(FeedbackRoutine(targetHeart, feedbackColor));
    }

    private IEnumerator FeedbackRoutine(Image targetHeart, Color feedbackColor)
    {
        RectTransform rectTransform = targetHeart.rectTransform;

        Vector3 originalScale = rectTransform.localScale;
        Color originalColor = targetHeart.color;

        float halfDuration = feedbackDuration * 0.5f;
        float elapsed = 0f;

        targetHeart.color = feedbackColor;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / halfDuration);
            rectTransform.localScale = Vector3.Lerp(originalScale, originalScale * feedbackScale, t);

            yield return null;
        }

        elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / halfDuration);
            rectTransform.localScale = Vector3.Lerp(originalScale * feedbackScale, originalScale, t);

            yield return null;
        }

        rectTransform.localScale = originalScale;
        targetHeart.color = originalColor;

        feedbackCoroutine = null;
    }

    private void EnsureBonusVisibleArray()
    {
        int length = BonusHeartCount;

        if (bonusHeartVisible != null && bonusHeartVisible.Length == length)
        {
            return;
        }

        bonusHeartVisible = new bool[length];
    }

    private void NormalizeHeartArrays()
    {
        if (!sortByXPosition)
        {
            return;
        }

        SortImagesByAnchoredX(hearts);
        SortImagesByAnchoredX(bonusHearts);
    }

    private void SortImagesByAnchoredX(Image[] images)
    {
        if (images == null || images.Length <= 1)
        {
            return;
        }

        Array.Sort(images, (a, b) =>
        {
            if (a == null && b == null)
            {
                return 0;
            }

            if (a == null)
            {
                return 1;
            }

            if (b == null)
            {
                return -1;
            }

            return a.rectTransform.anchoredPosition.x.CompareTo(b.rectTransform.anchoredPosition.x);
        });
    }
}