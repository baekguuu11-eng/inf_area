using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    public enum ShopItemType
    {
        HealSmall,
        HealLarge,
        AttackUpgrade,
        WeaponUpgrade,
        SkillUpgrade
    }

    [Serializable]
    public class ShopItem
    {
        public string itemName;
        [TextArea] public string description;
        public int price;
        public ShopItemType itemType;
    }

    [Header("Panel")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private CanvasGroup shopCanvasGroup;
    [SerializeField] private Image dimOverlay;

    [Header("Cards")]
    [SerializeField] private ShopCardUI leftCard;
    [SerializeField] private ShopCardUI centerCard;
    [SerializeField] private ShopCardUI rightCard;

    [Header("Top UI")]
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private Button exitButton;

    [Header("Reroll")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private TMP_Text rerollButtonText;
    [SerializeField] private int rerollCost = 5;

    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Items")]
    [SerializeField] private List<ShopItem> itemPool = new List<ShopItem>();

    [Header("Animation Offset")]
    [SerializeField] private Vector2 leftCardStartOffset = new Vector2(-900f, 0f);
    [SerializeField] private Vector2 centerCardStartOffset = new Vector2(0f, 750f);
    [SerializeField] private Vector2 rightCardStartOffset = new Vector2(900f, 0f);

    [Header("Card Sequence Animation")]
    [SerializeField] private float cardAppearDelay = 0.25f;

    private bool isOpen;
    private Coroutine cardSequenceCoroutine;

    private void Awake()
    {
        if (shopPanel == null)
        {
            shopPanel = gameObject;
        }

        if (shopCanvasGroup == null)
        {
            shopCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (shopCanvasGroup == null && shopPanel != null)
        {
            shopCanvasGroup = shopPanel.GetComponent<CanvasGroup>();
        }

        if (shopCanvasGroup == null && shopPanel != null)
        {
            shopCanvasGroup = shopPanel.AddComponent<CanvasGroup>();
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(CloseShop);
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(RerollCards);
        }

        if (rerollButtonText != null)
        {
            rerollButtonText.text = "REROLL  " + rerollCost + " Byte";
        }

        EnsureDefaultItems();
        HideShopInstant();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (isOpen)
            {
                CloseShop();
            }
            else
            {
                OpenShop();
            }
        }
    }

    private void EnsureDefaultItems()
    {
        if (itemPool != null && itemPool.Count > 0)
        {
            return;
        }

        itemPool = new List<ShopItem>();

        itemPool.Add(new ShopItem
        {
            itemName = "패치 파일",
            description = "체력을 1칸 회복합니다.",
            price = 8,
            itemType = ShopItemType.HealSmall
        });

        itemPool.Add(new ShopItem
        {
            itemName = "고급 패치 파일",
            description = "체력을 2칸 회복합니다.",
            price = 14,
            itemType = ShopItemType.HealLarge
        });

        itemPool.Add(new ShopItem
        {
            itemName = "공격 데이터",
            description = "이번 런 동안 공격력이 증가합니다.",
            price = 15,
            itemType = ShopItemType.AttackUpgrade
        });

        itemPool.Add(new ShopItem
        {
            itemName = "무기 강화 코드",
            description = "이번 런 동안 무기 데미지가 증가합니다.",
            price = 18,
            itemType = ShopItemType.WeaponUpgrade
        });

        itemPool.Add(new ShopItem
        {
            itemName = "기술 가속 칩",
            description = "이번 런 동안 기술 쿨타임이 감소합니다.",
            price = 16,
            itemType = ShopItemType.SkillUpgrade
        });
    }

    public void OpenShop()
    {
        if (isOpen)
        {
            return;
        }

        isOpen = true;
        GameInputState.IsLocked = true;

        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }

        if (shopCanvasGroup != null)
        {
            shopCanvasGroup.alpha = 1f;
            shopCanvasGroup.interactable = true;
            shopCanvasGroup.blocksRaycasts = true;
        }

        if (dimOverlay != null)
        {
            Color color = dimOverlay.color;
            color.a = 0.55f;
            dimOverlay.color = color;
            dimOverlay.raycastTarget = false;
        }

        RefreshCurrencyText();
        RollCards();

        Debug.Log("상점 열림");
    }

    public void CloseShop()
    {
        isOpen = false;
        GameInputState.IsLocked = false;

        HideShopInstant();

        Debug.Log("상점 닫힘");
    }

    private void HideShopInstant()
    {
        if (shopCanvasGroup != null)
        {
            shopCanvasGroup.alpha = 0f;
            shopCanvasGroup.interactable = false;
            shopCanvasGroup.blocksRaycasts = false;
        }

        if (dimOverlay != null)
        {
            Color color = dimOverlay.color;
            color.a = 0f;
            dimOverlay.color = color;
            dimOverlay.raycastTarget = false;
        }

        if (leftCard != null)
        {
            leftCard.HideInstant();
        }

        if (centerCard != null)
        {
            centerCard.HideInstant();
        }

        if (rightCard != null)
        {
            rightCard.HideInstant();
        }
    }

    private void RollCards()
    {
        List<ShopItem> selectedItems = PickRandomItems(3);

        if (selectedItems.Count < 3)
        {
            Debug.LogWarning("상점 아이템 풀이 3개보다 적습니다.");
            return;
        }

        if (leftCard != null)
        {
            leftCard.Setup(this, selectedItems[0]);
            leftCard.HideInstant();
        }
        else
        {
            Debug.LogWarning("Left Card가 연결되지 않았습니다.");
        }

        if (centerCard != null)
        {
            centerCard.Setup(this, selectedItems[1]);
            centerCard.HideInstant();
        }
        else
        {
            Debug.LogWarning("Center Card가 연결되지 않았습니다.");
        }

        if (rightCard != null)
        {
            rightCard.Setup(this, selectedItems[2]);
            rightCard.HideInstant();
        }
        else
        {
            Debug.LogWarning("Right Card가 연결되지 않았습니다.");
        }

        if (cardSequenceCoroutine != null)
        {
            StopCoroutine(cardSequenceCoroutine);
        }

        cardSequenceCoroutine = StartCoroutine(PlayCardSequenceRoutine());

        RefreshCurrencyText();
    }

    private IEnumerator PlayCardSequenceRoutine()
    {
        if (centerCard != null)
        {
            centerCard.PlayEnterAnimation(centerCardStartOffset);
        }

        yield return new WaitForSecondsRealtime(cardAppearDelay);

        if (leftCard != null)
        {
            leftCard.PlayEnterAnimation(leftCardStartOffset);
        }

        yield return new WaitForSecondsRealtime(cardAppearDelay);

        if (rightCard != null)
        {
            rightCard.PlayEnterAnimation(rightCardStartOffset);
        }

        cardSequenceCoroutine = null;
    }

    private List<ShopItem> PickRandomItems(int count)
    {
        List<ShopItem> result = new List<ShopItem>();
        List<ShopItem> tempPool = new List<ShopItem>(itemPool);

        while (result.Count < count && tempPool.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, tempPool.Count);
            result.Add(tempPool[randomIndex]);
            tempPool.RemoveAt(randomIndex);
        }

        return result;
    }

    public bool TryPurchase(ShopItem item, ShopCardUI card)
    {
        if (item == null)
        {
            return false;
        }

        if (!TrySpendByte(item.price))
        {
            Debug.Log("Byte 부족: 구매할 수 없습니다.");
            RefreshCurrencyText();
            return false;
        }

        ApplyItemEffect(item);
        RefreshCurrencyText();

        return true;
    }

    private void RerollCards()
    {
        if (!isOpen)
        {
            return;
        }

        if (!TrySpendByte(rerollCost))
        {
            Debug.Log("Byte 부족: 리롤할 수 없습니다.");
            RefreshCurrencyText();
            return;
        }

        RollCards();
    }

    private void ApplyItemEffect(ShopItem item)
    {
        switch (item.itemType)
        {
            case ShopItemType.HealSmall:
                if (playerHealth != null)
                {
                    playerHealth.Heal(1);
                }
                break;

            case ShopItemType.HealLarge:
                if (playerHealth != null)
                {
                    playerHealth.Heal(2);
                }
                break;

            case ShopItemType.AttackUpgrade:
                ShopRunUpgradeState.AttackDamageMultiplier += 0.15f;
                Debug.Log("공격력 증가. 현재 배율: " + ShopRunUpgradeState.AttackDamageMultiplier);
                break;

            case ShopItemType.WeaponUpgrade:
                ShopRunUpgradeState.WeaponDamageMultiplier += 0.2f;
                Debug.Log("무기 데미지 증가. 현재 배율: " + ShopRunUpgradeState.WeaponDamageMultiplier);
                break;

            case ShopItemType.SkillUpgrade:
                ShopRunUpgradeState.SkillCooldownMultiplier *= 0.9f;
                Debug.Log("기술 쿨타임 감소. 현재 배율: " + ShopRunUpgradeState.SkillCooldownMultiplier);
                break;
        }
    }

    private bool TrySpendByte(int amount)
    {
        if (ByteCurrencyManager.Instance == null)
        {
            Debug.LogWarning("ByteCurrencyManager.Instance가 없습니다.");
            return false;
        }

        return ByteCurrencyManager.Instance.SpendBytes(amount);
    }

    private int GetCurrentByte()
    {
        if (ByteCurrencyManager.Instance == null)
        {
            return 0;
        }

        return ByteCurrencyManager.Instance.CurrentByte;
    }

    private void RefreshCurrencyText()
    {
        if (currencyText != null)
        {
            currencyText.text = "Byte : " + GetCurrentByte();
        }
    }
}