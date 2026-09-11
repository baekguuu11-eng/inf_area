using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShopManager : MonoBehaviour
{
    public enum ShopItemType { HealSmall, HealLarge, AttackUpgrade, WeaponUpgrade, SkillUpgrade, Weapon, Chip }

    [Serializable]
    public class ShopItem
    {
        public string itemName;
        [TextArea] public string description;
        public int price;
        public ShopItemType itemType;
        public string weaponId;
        public ChipSlotManager.ChipType chipType = ChipSlotManager.ChipType.None;
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
    private ShopPresentationV13 presentationV13;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
        EnsureDefaultItems();
        if (autoBuildMissingUI && !HasCompleteUI()) BuildRuntimeUI();
        WireButtons();
        presentationV13 = GetComponent<ShopPresentationV13>();
        if (presentationV13 == null) presentationV13 = gameObject.AddComponent<ShopPresentationV13>();
        presentationV13.Bind(shopPanel, shopCanvasGroup);
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
            shopCanvasGroup.interactable = false;
            shopCanvasGroup.blocksRaycasts = false;
        }
        if (presentationV13 == null)
        {
            presentationV13 = GetComponent<ShopPresentationV13>();
            if (presentationV13 == null) presentationV13 = gameObject.AddComponent<ShopPresentationV13>();
            presentationV13.Bind(shopPanel, shopCanvasGroup);
        }
        presentationV13.PlayOpen();
        RefreshCurrencyText();
        RollCards();
    }

    public void CloseShop()
    {
        if (!isOpen) return;
        isOpen = false;
        presentationV13?.StopPresentation();
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
        else if (item.itemType == ShopItemType.Chip)
        {
            ChipSlotManager chips = ChipSlotManager.Instance;
            if (chips != null && chips.OwnsChip(item.chipType))
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
            case ShopItemType.Chip:
                if (ChipSlotManager.Instance != null)
                    ChipSlotManager.Instance.PurchaseChip(item.chipType);
                break;
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
            NewItem("소형 복구 패치", "체력을 1 회복합니다.", 8, ShopItemType.HealSmall),
            NewItem("고급 복구 패치", "체력을 2 회복합니다.", 14, ShopItemType.HealLarge),
            // 1~9 number-key chip system. These are the real gameplay chips used by
            // ChipSlotManager rather than disconnected generic shop buffs.
            NewChipItem("[1] 절단 증폭 칩", "근접 무기의 출력 제한을 완화합니다.\n효과: 근접 공격력 +20%", 12, ChipSlotManager.ChipType.MeleeDamage),
            NewChipItem("[2] 서보 오버클럭 칩", "근접 구동계의 반응 속도를 높입니다.\n효과: 근접 공격속도 +15%", 12, ChipSlotManager.ChipType.MeleeAttackSpeed),
            NewChipItem("[3] 신장 프로토콜 칩", "근접 무기의 유효 공격 길이를 확장합니다.\n효과: 근접 사거리 +15%", 13, ChipSlotManager.ChipType.MeleeRange),
            NewChipItem("[4] 탄도 증폭 칩", "발사체 출력 계산을 강화합니다.\n효과: 원거리 공격력 +18%", 14, ChipSlotManager.ChipType.RangedDamage),
            NewChipItem("[5] 급속 순환 칩", "무기 순환 시간을 단축합니다.\n효과: 원거리 연사속도 +12%", 13, ChipSlotManager.ChipType.RangedAttackSpeed),
            NewChipItem("[6] 관통 연산 칩", "충돌 연산을 재구성해 발사체가 적을 한 번 더 통과합니다.\n효과: 관통 +1회", 18, ChipSlotManager.ChipType.RangedPierce),
            NewChipItem("[7] 방벽 프로토콜 칩", "피격 데이터를 분산해 피해를 줄입니다.\n효과: 받는 피해 18% 감소", 17, ChipSlotManager.ChipType.Defense),
            NewChipItem("[8] 생체 확장 칩", "손상 허용치를 한 단계 확장합니다.\n효과: 최대 체력 +1", 18, ChipSlotManager.ChipType.MaxHealth),
            NewChipItem("[9] 기동 최적화 칩", "이동 제어 연산을 최적화합니다.\n효과: 이동속도 +12%", 14, ChipSlotManager.ChipType.MoveSpeed),
            NewWeaponItem("중량 해머", "느리지만 강력한 피해와 높은 저지력을 가진 근접 무기입니다.", 22, "debug_hammer"),
            NewWeaponItem("데이터 채찍", "긴 공격 범위로 다수의 적을 견제하며 적중한 적의 이동을 잠시 늦춥니다.", 20, "debug_whip"),
            NewWeaponItem("방화벽 검", "넓은 참격과 지속 피해를 남기는 특수 근접 무기입니다.", 26, "firewall_sword"),
            NewWeaponItem("기관총", "탄약을 빠르게 소비하는 대신 매우 높은 지속 화력을 냅니다.", 22, "machine_gun"),
            NewWeaponItem("샷건", "근거리에서 압도적인 순간 화력을 발휘합니다.", 26, "shotgun"),
            NewWeaponItem("레이저 건", "높은 정확도와 직선 관통 능력을 가진 에너지 화기입니다.", 30, "laser_gun")
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

    private ShopItem NewChipItem(string title, string desc, int price, ChipSlotManager.ChipType chipType)
    {
        ShopItem item = NewItem(title, desc, price, ShopItemType.Chip);
        item.chipType = chipType;
        return item;
    }

    private void WireButtons()
    {
        if (exitButton != null) { exitButton.onClick.RemoveAllListeners(); exitButton.onClick.AddListener(CloseShop); }
        if (rerollButton != null) { rerollButton.onClick.RemoveAllListeners(); rerollButton.onClick.AddListener(RerollCards); }
        if (rerollButtonText != null) rerollButtonText.text = "상품 재검색  " + rerollCost + " 바이트";
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
        ChipSlotManager chips = ChipSlotManager.Instance;

        pool.RemoveAll(item =>
            item == null ||
            (item.itemType == ShopItemType.Weapon && inventory != null && inventory.Owns(item.weaponId)) ||
            (item.itemType == ShopItemType.Chip && chips != null && chips.OwnsChip(item.chipType)));

        // Every shop roll should expose the real 1~9 chip system clearly. If there is any
        // unowned chip left, reserve one card for a chip instead of letting pure RNG hide it.
        if (result.Count < count)
        {
            List<ShopItem> chipPool = pool.FindAll(item => item.itemType == ShopItemType.Chip);
            if (chipPool.Count > 0)
            {
                ShopItem chip = chipPool[UnityEngine.Random.Range(0, chipPool.Count)];
                result.Add(chip);
                pool.Remove(chip);
            }
        }

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
        if (currencyText != null) currencyText.text = "보유 바이트  " + (ByteCurrencyManager.Instance != null ? ByteCurrencyManager.Instance.CurrentByte : 0);
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

        currencyText = CreateText("Currency", panel.transform, "보유 바이트  0", 30, TextAlignmentOptions.TopLeft);
        SetRect(currencyText.rectTransform, new Vector2(30, -25), new Vector2(360, 50), new Vector2(0,1));

        TMP_Text title = CreateText("Title", panel.transform, "보급 단말", 34, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0, -35), new Vector2(600, 60), new Vector2(0.5f,1));

        exitButton = CreateButton("Exit", panel.transform, "상점 닫기", out TMP_Text exitLabel);
        SetRect(exitButton.GetComponent<RectTransform>(), new Vector2(-30, -25), new Vector2(150, 46), new Vector2(1,1));

        leftCard = CreateCard("LeftCard", panel.transform, new Vector2(-390, 0));
        centerCard = CreateCard("CenterCard", panel.transform, Vector2.zero);
        rightCard = CreateCard("RightCard", panel.transform, new Vector2(390, 0));

        rerollButton = CreateButton("Reroll", panel.transform, "상품 재검색", out rerollButtonText);
        SetRect(rerollButton.GetComponent<RectTransform>(), new Vector2(0, 35), new Vector2(250, 50), new Vector2(0.5f,0));
        WireButtons();
        if (presentationV13 == null) presentationV13 = GetComponent<ShopPresentationV13>();
        if (presentationV13 == null) presentationV13 = gameObject.AddComponent<ShopPresentationV13>();
        presentationV13.Bind(shopPanel, shopCanvasGroup);
    }

    public void DebugReplayOpenPresentation()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (isOpen) CloseShop(); else OpenShop();
#endif
    }

    private ShopCardUI CreateCard(string objectName, Transform parent, Vector2 position)
    {
        GameObject cardObj = CreateUIObject(objectName, parent, typeof(Image), typeof(CanvasGroup));
        RectTransform rect = cardObj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f,0.5f); rect.pivot = new Vector2(0.5f,0.5f);
        rect.sizeDelta = new Vector2(340, 520); rect.anchoredPosition = position;
        cardObj.GetComponent<Image>().color = new Color(0.04f, 0.11f, 0.16f, 1f);
        ShopCardUI card = cardObj.AddComponent<ShopCardUI>();
        TMP_Text title = CreateText("Name", cardObj.transform, "상품", 25, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0,-28), new Vector2(300,60), new Vector2(0.5f,1));
        TMP_Text desc = CreateText("상품 설명", cardObj.transform, "상품 설명", 19, TextAlignmentOptions.Top);
        SetRect(desc.rectTransform, new Vector2(0,-92), new Vector2(300,112), new Vector2(0.5f,1));
        TMP_Text price = CreateText("Price", cardObj.transform, "0 바이트", 23, TextAlignmentOptions.Center);
        SetRect(price.rectTransform, new Vector2(0,92), new Vector2(250,42), new Vector2(0.5f,0));
        Button buy = CreateButton("Buy", cardObj.transform, "구매", out TMP_Text label);
        SetRect(buy.GetComponent<RectTransform>(), new Vector2(0,30), new Vector2(230,48), new Vector2(0.5f,0));
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
