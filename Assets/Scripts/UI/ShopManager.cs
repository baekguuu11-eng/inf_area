using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShopManager : MonoBehaviour
{
    public enum ShopItemType { HealSmall, HealLarge, AttackUpgrade, WeaponUpgrade, SkillUpgrade, Weapon }

    [Serializable]
    public class ShopItem
    {
        public string itemName;
        [TextArea] public string description;
        public int price;
        public ShopItemType itemType;
        public string weaponId;
    }

    public static ShopManager Instance { get; private set; }
    public bool IsOpen { get { return isOpen; } }

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
    [Header("Runtime Fallback")]
    [SerializeField] private bool autoBuildMissingUI = true;
    [SerializeField] private bool allowDebugBKey = true;

    private bool isOpen;
    private Coroutine cardSequenceCoroutine;
    private IDisposable inputLock;
    private ByteCurrencyManager subscribedCurrency;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
        EnsureDefaultItems();
        if (autoBuildMissingUI && !HasCompleteUI()) BuildRuntimeUI();
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
        if (Instance == this) Instance = null;
        UnsubscribeCurrency();
        ReleaseInputLock();
    }

    private void Update()
    {
        if (subscribedCurrency == null)
            TrySubscribeCurrency();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (allowDebugBKey && Input.GetKeyDown(KeyCode.B))
        {
            if (isOpen) CloseShop(); else OpenShop();
        }
#endif
        if (isOpen && Input.GetKeyDown(KeyCode.Escape)) CloseShop();
    }

    public void OpenShop()
    {
        if (isOpen) return;
        if (!HasCompleteUI() && autoBuildMissingUI) BuildRuntimeUI();
        isOpen = true;
        inputLock = GameInputState.Acquire("Shop");
        if (shopPanel != null) shopPanel.SetActive(true);
        if (shopCanvasGroup != null)
        {
            shopCanvasGroup.alpha = 1f;
            shopCanvasGroup.interactable = true;
            shopCanvasGroup.blocksRaycasts = true;
        }
        RefreshCurrencyText();
        RollCards();
    }

    public void CloseShop()
    {
        if (!isOpen) return;
        isOpen = false;
        HideShopInstant();
        ReleaseInputLock();
    }

    public bool CanAfford(int price)
    {
        return ByteCurrencyManager.Instance != null && ByteCurrencyManager.Instance.CanSpend(price);
    }

    public bool CanPurchase(ShopItem item)
    {
        if (item == null || !CanAfford(item.price))
            return false;
        if (item.itemType == ShopItemType.Weapon)
        {
            PlayerWeaponInventory inventory = PlayerWeaponInventory.Instance;
            if (inventory != null && inventory.Owns(item.weaponId))
                return false;
        }
        return true;
    }

    public bool TryPurchase(ShopItem item, ShopCardUI card)
    {
        if (item == null || ByteCurrencyManager.Instance == null || !CanPurchase(item)) return false;
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
            case ShopItemType.HealSmall: if (playerHealth != null) playerHealth.Heal(1); break;
            case ShopItemType.HealLarge: if (playerHealth != null) playerHealth.Heal(2); break;
            case ShopItemType.AttackUpgrade: ShopRunUpgradeState.AddAttackDamage(0.15f); break;
            case ShopItemType.WeaponUpgrade: ShopRunUpgradeState.AddWeaponDamage(0.20f); break;
            case ShopItemType.SkillUpgrade: ShopRunUpgradeState.MultiplySkillCooldown(0.90f); break;
            case ShopItemType.Weapon:
                if (PlayerWeaponInventory.Instance != null)
                    PlayerWeaponInventory.Instance.PurchaseAndEquip(item.weaponId);
                break;
        }
    }

    private void EnsureDefaultItems()
    {
        if (itemPool != null && itemPool.Count > 0) return;
        itemPool = new List<ShopItem>
        {
            NewItem("PATCH FILE", "Restore 1 health.", 8, ShopItemType.HealSmall),
            NewItem("ADVANCED PATCH", "Restore 2 health.", 14, ShopItemType.HealLarge),
            NewItem("ATTACK DATA", "Run-wide damage +15%.", 15, ShopItemType.AttackUpgrade),
            NewItem("WEAPON CODE", "Run-wide weapon damage +20%.", 18, ShopItemType.WeaponUpgrade),
            NewItem("ACCEL CHIP", "Run-wide skill cooldown -10%.", 16, ShopItemType.SkillUpgrade),
            NewWeaponItem("DEBUG HAMMER", "Heavy melee: maximum damage and knockback.", 22, "debug_hammer"),
            NewWeaponItem("DEBUG WHIP", "Long-range melee control with low stopping power.", 20, "debug_whip"),
            NewWeaponItem("FIREWALL SWORD", "Wide burning slash with damage over time.", 26, "firewall_sword"),
            NewWeaponItem("MACHINE GUN", "Hold to fire. High rate, high ammo consumption.", 22, "machine_gun"),
            NewWeaponItem("SHOTGUN", "Close burst. Consumes 3 EN per shot.", 26, "shotgun"),
            NewWeaponItem("LASER GUN", "Piercing hitscan beam. Consumes 4 EN per shot.", 30, "laser_gun")
        };
    }

    private ShopItem NewItem(string title, string desc, int price, ShopItemType type)
    {
        ShopItem item = new ShopItem(); item.itemName = title; item.description = desc; item.price = price; item.itemType = type; return item;
    }

    private ShopItem NewWeaponItem(string title, string desc, int price, string weaponId)
    {
        ShopItem item = NewItem(title, desc, price, ShopItemType.Weapon);
        item.weaponId = weaponId;
        return item;
    }

    private void WireButtons()
    {
        if (exitButton != null) { exitButton.onClick.RemoveAllListeners(); exitButton.onClick.AddListener(CloseShop); }
        if (rerollButton != null) { rerollButton.onClick.RemoveAllListeners(); rerollButton.onClick.AddListener(RerollCards); }
        if (rerollButtonText != null) rerollButtonText.text = "REROLL  " + rerollCost + " Byte";
    }

    private void RollCards()
    {
        List<ShopItem> selected = PickRandomItems(3);
        if (selected.Count < 3) return;
        SetupCard(leftCard, selected[0]); SetupCard(centerCard, selected[1]); SetupCard(rightCard, selected[2]);
        if (cardSequenceCoroutine != null) StopCoroutine(cardSequenceCoroutine);
        cardSequenceCoroutine = StartCoroutine(PlayCardSequenceRoutine());
    }

    private void SetupCard(ShopCardUI card, ShopItem item)
    {
        if (card == null) return;
        card.Setup(this, item); card.HideInstant();
    }

    private IEnumerator PlayCardSequenceRoutine()
    {
        if (centerCard != null) centerCard.PlayEnterAnimation(centerCardStartOffset);
        yield return new WaitForSecondsRealtime(cardAppearDelay);
        if (leftCard != null) leftCard.PlayEnterAnimation(leftCardStartOffset);
        yield return new WaitForSecondsRealtime(cardAppearDelay);
        if (rightCard != null) rightCard.PlayEnterAnimation(rightCardStartOffset);
        cardSequenceCoroutine = null;
    }

    private List<ShopItem> PickRandomItems(int count)
    {
        List<ShopItem> result = new List<ShopItem>();
        List<ShopItem> pool = new List<ShopItem>(itemPool);
        PlayerWeaponInventory inventory = PlayerWeaponInventory.Instance;
        pool.RemoveAll(item => item != null && item.itemType == ShopItemType.Weapon && inventory != null && inventory.Owns(item.weaponId));
        while (result.Count < count && pool.Count > 0)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            result.Add(pool[index]); pool.RemoveAt(index);
        }
        return result;
    }

    private void RerollCards()
    {
        if (!isOpen || ByteCurrencyManager.Instance == null) return;
        if (!ByteCurrencyManager.Instance.SpendBytes(rerollCost)) { RefreshCardAffordability(); return; }
        RollCards(); RefreshCurrencyText();
    }

    private void OnCurrencyChanged(int value)
    {
        RefreshCurrencyText(); RefreshCardAffordability();
    }

    private void TrySubscribeCurrency()
    {
        ByteCurrencyManager current = ByteCurrencyManager.Instance;
        if (current == null || subscribedCurrency == current) return;
        UnsubscribeCurrency();
        subscribedCurrency = current;
        subscribedCurrency.CurrencyChanged += OnCurrencyChanged;
    }

    private void UnsubscribeCurrency()
    {
        if (subscribedCurrency == null) return;
        subscribedCurrency.CurrencyChanged -= OnCurrencyChanged;
        subscribedCurrency = null;
    }

    private void RefreshCurrencyText()
    {
        if (currencyText != null) currencyText.text = "BYTE  " + (ByteCurrencyManager.Instance != null ? ByteCurrencyManager.Instance.CurrentByte : 0);
    }

    private void RefreshCardAffordability()
    {
        if (leftCard != null) leftCard.RefreshAffordability();
        if (centerCard != null) centerCard.RefreshAffordability();
        if (rightCard != null) rightCard.RefreshAffordability();
    }

    private void HideShopInstant()
    {
        if (shopCanvasGroup != null)
        {
            shopCanvasGroup.alpha = 0f;
            shopCanvasGroup.interactable = false;
            shopCanvasGroup.blocksRaycasts = false;
        }
        if (shopPanel != null) shopPanel.SetActive(false);
    }

    private void ReleaseInputLock()
    {
        if (inputLock != null) { inputLock.Dispose(); inputLock = null; }
    }

    private bool HasCompleteUI()
    {
        return shopPanel != null && leftCard != null && centerCard != null && rightCard != null && exitButton != null;
    }

    private void BuildRuntimeUI()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) parentCanvas = FindAnyObjectByType<Canvas>();
        Transform parent = parentCanvas != null ? parentCanvas.transform : transform;

        GameObject panel = CreateUIObject("ShopPanel_Runtime", parent, typeof(Image), typeof(CanvasGroup));
        RectTransform panelRect = panel.GetComponent<RectTransform>(); Stretch(panelRect);
        Image panelImage = panel.GetComponent<Image>(); panelImage.color = new Color(0.015f, 0.025f, 0.045f, 0.96f);
        shopPanel = panel; shopCanvasGroup = panel.GetComponent<CanvasGroup>(); dimOverlay = panelImage;

        currencyText = CreateText("Currency", panel.transform, "BYTE  0", 30, TextAlignmentOptions.TopLeft);
        SetRect(currencyText.rectTransform, new Vector2(30, -25), new Vector2(360, 50), new Vector2(0,1));

        TMP_Text title = CreateText("Title", panel.transform, "ARK SUPPLY TERMINAL", 34, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0, -35), new Vector2(600, 60), new Vector2(0.5f,1));

        exitButton = CreateButton("Exit", panel.transform, "CLOSE", out TMP_Text exitLabel);
        SetRect(exitButton.GetComponent<RectTransform>(), new Vector2(-30, -25), new Vector2(150, 46), new Vector2(1,1));

        leftCard = CreateCard("LeftCard", panel.transform, new Vector2(-390, 0));
        centerCard = CreateCard("CenterCard", panel.transform, Vector2.zero);
        rightCard = CreateCard("RightCard", panel.transform, new Vector2(390, 0));

        rerollButton = CreateButton("Reroll", panel.transform, "REROLL", out rerollButtonText);
        SetRect(rerollButton.GetComponent<RectTransform>(), new Vector2(0, 35), new Vector2(250, 50), new Vector2(0.5f,0));
        WireButtons();
    }

    private ShopCardUI CreateCard(string objectName, Transform parent, Vector2 position)
    {
        GameObject cardObj = CreateUIObject(objectName, parent, typeof(Image), typeof(CanvasGroup));
        RectTransform rect = cardObj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f,0.5f); rect.pivot = new Vector2(0.5f,0.5f);
        rect.sizeDelta = new Vector2(320, 430); rect.anchoredPosition = position;
        cardObj.GetComponent<Image>().color = new Color(0.04f, 0.11f, 0.16f, 1f);
        ShopCardUI card = cardObj.AddComponent<ShopCardUI>();
        TMP_Text title = CreateText("Name", cardObj.transform, "ITEM", 25, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0,-35), new Vector2(280,70), new Vector2(0.5f,1));
        TMP_Text desc = CreateText("Description", cardObj.transform, "Description", 19, TextAlignmentOptions.Top);
        SetRect(desc.rectTransform, new Vector2(0,-130), new Vector2(270,170), new Vector2(0.5f,1));
        TMP_Text price = CreateText("Price", cardObj.transform, "0 Byte", 23, TextAlignmentOptions.Center);
        SetRect(price.rectTransform, new Vector2(0,95), new Vector2(250,45), new Vector2(0.5f,0));
        Button buy = CreateButton("Buy", cardObj.transform, "BUY", out TMP_Text label);
        SetRect(buy.GetComponent<RectTransform>(), new Vector2(0,35), new Vector2(230,50), new Vector2(0.5f,0));
        card.BindRuntimeReferences(title, desc, price, buy, label);
        return card;
    }

    private GameObject CreateUIObject(string objectName, Transform parent, params Type[] components)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        for (int i=0;i<components.Length;i++) if (obj.GetComponent(components[i]) == null) obj.AddComponent(components[i]);
        return obj;
    }

    private TMP_Text CreateText(string objectName, Transform parent, string content, float size, TextAlignmentOptions alignment)
    {
        GameObject obj = CreateUIObject(objectName, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.text = content; text.fontSize = size; text.alignment = alignment; text.color = Color.white;
        text.enableWordWrapping = true; text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string labelText, out TMP_Text label)
    {
        GameObject obj = CreateUIObject(objectName, parent, typeof(Image), typeof(Button));
        obj.GetComponent<Image>().color = new Color(0.05f,0.45f,0.55f,1f);
        label = CreateText("Label", obj.transform, labelText, 20, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return obj.GetComponent<Button>();
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    private void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
    {
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor; rect.anchoredPosition = anchoredPosition; rect.sizeDelta = size;
    }
}
