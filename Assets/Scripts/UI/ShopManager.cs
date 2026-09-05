using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        [TextArea] public string description;
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

    [Header("Runtime Fallback")]
    [SerializeField] private bool autoBuildMissingUI = false;
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

        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        EnsureDefaultItems();

        if (autoBuildMissingUI && !HasCompleteUI())
        {
            BuildRuntimeUI();
        }

        WireButtons();
        HideShopInstant();

        Debug.Log("ShopManager Awake 완료");
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

        bool bPressed = false;
        bool escapePressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.B))
        {
            bPressed = true;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            escapePressed = true;
        }
#endif

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.bKey.wasPressedThisFrame)
            {
                bPressed = true;
            }

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                escapePressed = true;
            }
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (allowDebugBKey && bPressed)
        {
            Debug.Log("B키 입력 감지됨");

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

        if (isOpen && escapePressed)
        {
            CloseShop();
        }
    }

    [ContextMenu("TEST - Open Shop")]
    private void TestOpenShop()
    {
        Debug.Log("Inspector 테스트로 상점 열기 실행");
        OpenShop();
    }

    [ContextMenu("TEST - Close Shop")]
    private void TestCloseShop()
    {
        Debug.Log("Inspector 테스트로 상점 닫기 실행");
        CloseShop();
    }

    public void OpenShop()
    {
        if (isOpen)
        {
            return;
        }

        if (!HasCompleteUI() && autoBuildMissingUI)
        {
            BuildRuntimeUI();
            WireButtons();
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
            Color color = dimOverlay.color;
            color.a = 0.62f;
            dimOverlay.color = color;
            dimOverlay.raycastTarget = false;
        }

        RefreshCurrencyText();
        RollCards();

        Debug.Log("상점 열림");
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
            Debug.LogWarning("ByteCurrencyManager.Instance가 없습니다.");
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
        if (itemPool != null && itemPool.Count > 0) return;

        itemPool = new List<ShopItem>
    {
        NewItem("치료 패치", "체력을 1 회복합니다.", 8, ShopItemType.HealSmall),
        NewItem("고급 치료 패치", "체력을 2 회복합니다.", 14, ShopItemType.HealLarge),
        NewItem("공격 데이터", "이번 런 동안 공격력이 15% 증가합니다.", 15, ShopItemType.AttackUpgrade),
        NewItem("무기 강화 코드", "이번 런 동안 무기 피해가 20% 증가합니다.", 18, ShopItemType.WeaponUpgrade),
        NewItem("가속 칩", "이번 런 동안 스킬 쿨타임이 10% 감소합니다.", 16, ShopItemType.SkillUpgrade),

        NewWeaponItem("디버그 해머", "강한 근접 무기입니다. 높은 피해와 넉백을 가집니다.", 22, "debug_hammer"),
        NewWeaponItem("디버그 채찍", "사거리가 긴 근접 제어 무기입니다.", 20, "debug_whip"),
        NewWeaponItem("파이어월 소드", "넓게 베며 지속 화상 피해를 줍니다.", 26, "firewall_sword"),
        NewWeaponItem("기관총", "누르고 있으면 발사합니다. 연사 속도가 높지만 탄 소모가 큽니다.", 22, "machine_gun"),
        NewWeaponItem("산탄총", "근거리 폭발 화력. 발사 시 에너지를 추가 소모합니다.", 26, "shotgun"),
        NewWeaponItem("레이저 건", "관통형 광선 무기입니다. 에너지를 소모합니다.", 30, "laser_gun")
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
            Debug.LogWarning("상점 아이템 풀이 3개보다 적습니다.");
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

        RefreshCurrencyText();
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
            Debug.LogWarning("ByteCurrencyManager.Instance가 없습니다.");
            return;
        }

        if (!ByteCurrencyManager.Instance.SpendBytes(rerollCost))
        {
            Debug.Log("Byte 부족: 리롤할 수 없습니다.");
            RefreshCurrencyText();
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

        if (current == null)
        {
            return;
        }

        if (subscribedCurrency == current)
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
        if (currencyText == null)
        {
            return;
        }

        int currentByte = 0;

        if (ByteCurrencyManager.Instance != null)
        {
            currentByte = ByteCurrencyManager.Instance.CurrentByte;
        }

        currencyText.text = "BYTE  " + currentByte;
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
        if (shopPanel != null && !shopPanel.activeSelf)
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

    private void ReleaseInputLock()
    {
        if (inputLock != null)
        {
            inputLock.Dispose();
            inputLock = null;
        }
    }

    private bool HasCompleteUI()
    {
        return shopPanel != null &&
               shopCanvasGroup != null &&
               leftCard != null &&
               centerCard != null &&
               rightCard != null &&
               exitButton != null &&
               rerollButton != null;
    }

    private void BuildRuntimeUI()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas == null)
        {
            parentCanvas = FindAnyObjectByType<Canvas>();
        }

        Transform parent = parentCanvas != null ? parentCanvas.transform : transform;

        GameObject panel = CreateUIObject("ShopPanel_Runtime", parent, typeof(Image), typeof(CanvasGroup));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        Stretch(panelRect);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.015f, 0.025f, 0.045f, 0.96f);

        shopPanel = panel;
        shopCanvasGroup = panel.GetComponent<CanvasGroup>();
        dimOverlay = panelImage;

        currencyText = CreateText("Currency", panel.transform, "BYTE  0", 30, TextAlignmentOptions.TopLeft);
        SetRect(currencyText.rectTransform, new Vector2(30, -25), new Vector2(360, 50), new Vector2(0, 1));

        TMP_Text title = CreateText("Title", panel.transform, "ARK SUPPLY TERMINAL", 34, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0, -35), new Vector2(600, 60), new Vector2(0.5f, 1));

        exitButton = CreateButton("Exit", panel.transform, "CLOSE", out TMP_Text exitLabel);
        SetRect(exitButton.GetComponent<RectTransform>(), new Vector2(-30, -25), new Vector2(150, 46), new Vector2(1, 1));

        leftCard = CreateCard("LeftCard", panel.transform, new Vector2(-390, 0));
        centerCard = CreateCard("CenterCard", panel.transform, Vector2.zero);
        rightCard = CreateCard("RightCard", panel.transform, new Vector2(390, 0));

        rerollButton = CreateButton("Reroll", panel.transform, "REROLL", out rerollButtonText);
        SetRect(rerollButton.GetComponent<RectTransform>(), new Vector2(0, 35), new Vector2(250, 50), new Vector2(0.5f, 0));

        WireButtons();
    }

    private ShopCardUI CreateCard(string objectName, Transform parent, Vector2 position)
    {
        GameObject cardObj = CreateUIObject(objectName, parent, typeof(Image), typeof(CanvasGroup));

        RectTransform rect = cardObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(320, 430);
        rect.anchoredPosition = position;

        Image image = cardObj.GetComponent<Image>();
        image.color = new Color(0.04f, 0.11f, 0.16f, 1f);

        ShopCardUI card = cardObj.AddComponent<ShopCardUI>();

        TMP_Text title = CreateText("Name", cardObj.transform, "ITEM", 25, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0, -35), new Vector2(280, 70), new Vector2(0.5f, 1));

        TMP_Text desc = CreateText("Description", cardObj.transform, "Description", 19, TextAlignmentOptions.Top);
        SetRect(desc.rectTransform, new Vector2(0, -130), new Vector2(270, 170), new Vector2(0.5f, 1));

        TMP_Text price = CreateText("Price", cardObj.transform, "0 Byte", 23, TextAlignmentOptions.Center);
        SetRect(price.rectTransform, new Vector2(0, 95), new Vector2(250, 45), new Vector2(0.5f, 0));

        Button buy = CreateButton("Buy", cardObj.transform, "BUY", out TMP_Text label);
        SetRect(buy.GetComponent<RectTransform>(), new Vector2(0, 35), new Vector2(230, 50), new Vector2(0.5f, 0));

        card.BindRuntimeReferences(title, desc, price, buy, label);

        return card;
    }

    private GameObject CreateUIObject(string objectName, Transform parent, params Type[] components)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        for (int i = 0; i < components.Length; i++)
        {
            if (obj.GetComponent(components[i]) == null)
            {
                obj.AddComponent(components[i]);
            }
        }

        return obj;
    }

    private TMP_Text CreateText(string objectName, Transform parent, string content, float size, TextAlignmentOptions alignment)
    {
        GameObject obj = CreateUIObject(objectName, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();

        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;

        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string labelText, out TMP_Text label)
    {
        GameObject obj = CreateUIObject(objectName, parent, typeof(Image), typeof(Button));

        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.05f, 0.45f, 0.55f, 1f);
        image.raycastTarget = true;

        Button button = obj.GetComponent<Button>();

        label = CreateText("Label", obj.transform, labelText, 20, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);

        return button;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }
}