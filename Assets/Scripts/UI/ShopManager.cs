using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShopManager : MonoBehaviour
{
    public enum ShopItemType
    {
        HealSmall,
        HealLarge,
        AttackUpgrade,
        WeaponUpgrade,
        SkillUpgrade,
        Weapon
    }

    [Serializable]
    public class ShopItem
    {
        public string itemName;

        [TextArea]
        public string description;

        public int price;
        public ShopItemType itemType;
        public string weaponId;
    }

    public static ShopManager Instance { get; private set; }

    public bool IsOpen
    {
        get { return isOpen; }
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

    [Header("Animation")]
    [SerializeField] private Vector2 leftCardStartOffset = new Vector2(-600f, 0f);
    [SerializeField] private Vector2 centerCardStartOffset = new Vector2(0f, 500f);
    [SerializeField] private Vector2 rightCardStartOffset = new Vector2(600f, 0f);
    [SerializeField] private float cardAppearDelay = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool allowDebugBKey = true;

    private bool isOpen;
    private Coroutine cardSequenceCoroutine;
    private IDisposable inputLock;
    private ByteCurrencyManager subscribedCurrency;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (shopPanel == null)
        {
            shopPanel = gameObject;
        }

        if (shopCanvasGroup == null)
        {
            shopCanvasGroup = shopPanel.GetComponent<CanvasGroup>();

            if (shopCanvasGroup == null)
            {
                shopCanvasGroup = shopPanel.AddComponent<CanvasGroup>();
            }
        }

        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        EnsureDefaultItems();
        WireButtons();
        HideShopInstant();
    }

    private void OnEnable()
    {
        TrySubscribeCurrency();
    }

    private void Start()
    {
        TrySubscribeCurrency();
        RefreshCurrencyText();
    }

    private void OnDisable()
    {
        UnsubscribeCurrency();
        ReleaseInputLock();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnsubscribeCurrency();
        ReleaseInputLock();
    }

    private void Update()
    {
        if (subscribedCurrency == null)
        {
            TrySubscribeCurrency();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (allowDebugBKey && Input.GetKeyDown(KeyCode.B))
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
#endif

        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseShop();
        }
    }

    public void OpenShop()
    {
        if (isOpen)
        {
            return;
        }

        isOpen = true;

        if (inputLock == null)
        {
            inputLock = GameInputState.Acquire("Shop");
        }

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
            dimOverlay.enabled = true;
        }

        RefreshCurrencyText();
        RollCards();

        Debug.Log("상점 열림");
    }

    [ContextMenu("DEBUG - Replay Open Presentation")]
    public void DebugReplayOpenPresentation()
    {
        if (isOpen)
        {
            if (cardSequenceCoroutine != null)
            {
                StopCoroutine(cardSequenceCoroutine);
                cardSequenceCoroutine = null;
            }

            RollCards();
            Debug.Log("상점 연출 다시 재생");
            return;
        }

        OpenShop();
    }

    public void CloseShop()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        HideShopInstant();
        ReleaseInputLock();

        Debug.Log("상점 닫힘");
    }

    public bool CanAfford(int price)
    {
        return ByteCurrencyManager.Instance != null && ByteCurrencyManager.Instance.CanSpend(price);
    }

    public bool CanPurchase(ShopItem item)
    {
        if (item == null)
        {
            return false;
        }

        if (!CanAfford(item.price))
        {
            return false;
        }

        if (item.itemType == ShopItemType.Weapon)
        {
            PlayerWeaponInventory inventory = PlayerWeaponInventory.Instance;

            if (inventory != null && inventory.Owns(item.weaponId))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryPurchase(ShopItem item, ShopCardUI card)
    {
        if (item == null)
        {
            return false;
        }

        if (ByteCurrencyManager.Instance == null)
        {
            return false;
        }

        if (!CanPurchase(item))
        {
            RefreshCurrencyText();
            RefreshCardAffordability();
            return false;
        }

        if (!ByteCurrencyManager.Instance.SpendBytes(item.price))
        {
            RefreshCurrencyText();
            RefreshCardAffordability();
            return false;
        }

        ApplyItemEffect(item);

        RefreshCurrencyText();
        RefreshCardAffordability();

        return true;
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
                ShopRunUpgradeState.AddAttackDamage(0.15f);
                break;

            case ShopItemType.WeaponUpgrade:
                ShopRunUpgradeState.AddWeaponDamage(0.20f);
                break;

            case ShopItemType.SkillUpgrade:
                ShopRunUpgradeState.MultiplySkillCooldown(0.90f);
                break;

            case ShopItemType.Weapon:
                if (PlayerWeaponInventory.Instance != null)
                {
                    PlayerWeaponInventory.Instance.PurchaseAndEquip(item.weaponId);
                }
                break;
        }
    }

    private void EnsureDefaultItems()
    {
        if (itemPool != null && itemPool.Count > 0)
        {
            return;
        }

        itemPool = new List<ShopItem>
        {
            NewItem("치료 패치", "체력을 1 회복합니다.", 8, ShopItemType.HealSmall),
            NewItem("고급 치료 패치", "체력을 2 회복합니다.", 14, ShopItemType.HealLarge),
            NewItem("공격 데이터", "이번 방 동안 공격력이 15% 증가합니다.", 15, ShopItemType.AttackUpgrade),
            NewItem("무기 강화 코드", "이번 방 동안 무기 피해가 20% 증가합니다.", 18, ShopItemType.WeaponUpgrade),
            NewItem("가속 칩", "이번 방 동안 스킬 쿨타임이 10% 감소합니다.", 16, ShopItemType.SkillUpgrade),

            NewWeaponItem("디버그 해머", "강한 근접 무기입니다.", 22, "debug_hammer"),
            NewWeaponItem("디버그 채찍", "긴 사거리의 근접 무기입니다.", 20, "debug_whip"),
            NewWeaponItem("파이어월 소드", "넓은 화염 베기를 사용합니다.", 26, "firewall_sword"),
            NewWeaponItem("기관총", "누르고 있으면 발사합니다.", 22, "machine_gun"),
            NewWeaponItem("산탄총", "가까운 적에게 강한 탄을 발사합니다.", 26, "shotgun"),
            NewWeaponItem("레이저 건", "관통 레이저를 발사합니다.", 30, "laser_gun")
        };
    }

    private ShopItem NewItem(string title, string desc, int price, ShopItemType type)
    {
        ShopItem item = new ShopItem();
        item.itemName = title;
        item.description = desc;
        item.price = price;
        item.itemType = type;
        return item;
    }

    private ShopItem NewWeaponItem(string title, string desc, int price, string weaponId)
    {
        ShopItem item = NewItem(title, desc, price, ShopItemType.Weapon);
        item.weaponId = weaponId;
        return item;
    }

    private void WireButtons()
    {
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
            rerollButtonText.text = "새로고침  " + rerollCost + " BYTE";
        }
    }

    private void RollCards()
    {
        List<ShopItem> selected = PickRandomItems(3);

        if (selected.Count < 3)
        {
            return;
        }

        SetupCard(leftCard, selected[0]);
        SetupCard(centerCard, selected[1]);
        SetupCard(rightCard, selected[2]);

        if (cardSequenceCoroutine != null)
        {
            StopCoroutine(cardSequenceCoroutine);
        }

        cardSequenceCoroutine = StartCoroutine(PlayCardSequenceRoutine());
    }

    private void SetupCard(ShopCardUI card, ShopItem item)
    {
        if (card == null)
        {
            return;
        }

        card.Setup(this, item);
        card.HideInstant();
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
        List<ShopItem> pool = new List<ShopItem>(itemPool);

        PlayerWeaponInventory inventory = PlayerWeaponInventory.Instance;

        pool.RemoveAll(item =>
            item != null &&
            item.itemType == ShopItemType.Weapon &&
            inventory != null &&
            inventory.Owns(item.weaponId)
        );

        while (result.Count < count && pool.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    private void RerollCards()
    {
        if (!isOpen)
        {
            return;
        }

        if (ByteCurrencyManager.Instance == null)
        {
            return;
        }

        if (!ByteCurrencyManager.Instance.SpendBytes(rerollCost))
        {
            RefreshCardAffordability();
            return;
        }

        RollCards();
        RefreshCurrencyText();
    }

    private void OnCurrencyChanged(int value)
    {
        RefreshCurrencyText();
        RefreshCardAffordability();
    }

    private void TrySubscribeCurrency()
    {
        ByteCurrencyManager current = ByteCurrencyManager.Instance;

        if (current == null || subscribedCurrency == current)
        {
            return;
        }

        UnsubscribeCurrency();

        subscribedCurrency = current;
        subscribedCurrency.CurrencyChanged += OnCurrencyChanged;
    }

    private void UnsubscribeCurrency()
    {
        if (subscribedCurrency == null)
        {
            return;
        }

        subscribedCurrency.CurrencyChanged -= OnCurrencyChanged;
        subscribedCurrency = null;
    }

    private void RefreshCurrencyText()
    {
        if (currencyText != null)
        {
            int value = 0;

            if (ByteCurrencyManager.Instance != null)
            {
                value = ByteCurrencyManager.Instance.CurrentByte;
            }

            currencyText.text = "BYTE  " + value;
        }
    }

    private void RefreshCardAffordability()
    {
        if (leftCard != null)
        {
            leftCard.RefreshAffordability();
        }

        if (centerCard != null)
        {
            centerCard.RefreshAffordability();
        }

        if (rightCard != null)
        {
            rightCard.RefreshAffordability();
        }
    }

    private void HideShopInstant()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }

        if (shopCanvasGroup != null)
        {
            shopCanvasGroup.alpha = 0f;
            shopCanvasGroup.interactable = false;
            shopCanvasGroup.blocksRaycasts = false;
        }

        if (dimOverlay != null)
        {
            dimOverlay.enabled = false;
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

    private void ReleaseInputLock()
    {
        if (inputLock != null)
        {
            inputLock.Dispose();
            inputLock = null;
        }
    }
}