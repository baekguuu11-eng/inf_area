using System;
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

    private int currentHealth;
    private int maxHealth;
    private int baseMaxHealth = 5;

    private bool bonusHeartMode;
    private bool[] bonusHeartVisible;

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

    public void PrepareBonusHeartAnimation(int bonusSlotCount)
    {
        EnsureBonusVisibleArray();

        bonusHeartMode = true;

        int visibleCount = Mathf.Clamp(bonusSlotCount, 0, BonusHeartCount);

        for (int i = 0; i < bonusHeartVisible.Length; i++)
        {
            bonusHeartVisible[i] = false;
        }

        for (int i = 0; i < BonusHeartCount; i++)
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