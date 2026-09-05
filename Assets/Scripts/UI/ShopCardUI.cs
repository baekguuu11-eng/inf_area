using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShopCardUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buttonText;
    [Header("Animation")]
    [SerializeField] private float enterDuration = 0.35f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;
    private ShopManager.ShopItem currentItem;
    private ShopManager shopManager;
    private bool isSoldOut;

    private RectTransform statRoot;
    private TMP_Text[] statLabels;
    private Image[,] statCells;
    private TMP_Text traitText;
    private TMP_FontAsset galmuri;

    private static readonly Color ActiveCell = new Color(0.22f, 0.94f, 1f, 0.96f);
    private static readonly Color ActiveCellMax = new Color(0.62f, 0.98f, 1f, 1f);
    private static readonly Color InactiveCell = new Color(0.055f, 0.15f, 0.20f, 0.92f);
    private static readonly Color CellOutline = new Color(0.06f, 0.42f, 0.52f, 0.95f);

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (rectTransform != null) originalPosition = rectTransform.anchoredPosition;
        galmuri = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");
    }

    public void BindRuntimeReferences(TMP_Text title, TMP_Text description, TMP_Text price, Button button, TMP_Text buttonLabel)
    {
        nameText = title;
        descText = description;
        priceText = price;
        buyButton = button;
        buttonText = buttonLabel;
    }

    public void Setup(ShopManager manager, ShopManager.ShopItem item)
    {
        shopManager = manager;
        currentItem = item;
        isSoldOut = false;
        if (nameText != null) nameText.text = item.itemName;

        ShopWeaponStatsV14.Profile profile = null;
        bool isWeapon = item.itemType == ShopManager.ShopItemType.Weapon && ShopWeaponStatsV14.TryGet(item.weaponId, out profile);
        if (descText != null)
        {
            descText.text = isWeapon
                ? item.description + "\n<color=#65F4FF>고유: " + profile.Trait + "</color>"
                : item.description;
        }

        UpdateWeaponStats(isWeapon ? profile : null);

        if (priceText != null) priceText.text = item.price + " 바이트";
        if (buttonText != null) buttonText.text = "구매";
        if (buyButton != null)
        {
            buyButton.interactable = manager == null || manager.CanPurchase(item);
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(Buy);
        }
    }

    public void RefreshAffordability()
    {
        if (buyButton != null && !isSoldOut && currentItem != null && shopManager != null)
            buyButton.interactable = shopManager.CanPurchase(currentItem);
    }

    public void HideInstant()
    {
        StopAllCoroutines();
        EnsureCached();
        if (rectTransform != null) originalPosition = rectTransform.anchoredPosition;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void PlayEnterAnimation(Vector2 startOffset)
    {
        StopAllCoroutines();
        StartCoroutine(EnterRoutine(startOffset));
    }

    private IEnumerator EnterRoutine(Vector2 startOffset)
    {
        EnsureCached();
        if (rectTransform == null || canvasGroup == null) yield break;
        Vector2 start = originalPosition + startOffset;
        rectTransform.anchoredPosition = start;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        float elapsed = 0f;
        while (elapsed < enterDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, enterDuration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            rectTransform.anchoredPosition = Vector2.Lerp(start, originalPosition, eased);
            yield return null;
        }
        rectTransform.anchoredPosition = originalPosition;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void Buy()
    {
        if (isSoldOut || shopManager == null || currentItem == null) return;
        if (shopManager.TryPurchase(currentItem, this)) SetSoldOut();
    }

    public void SetSoldOut()
    {
        isSoldOut = true;
        if (buttonText != null) buttonText.text = "구매 완료";
        if (buyButton != null) buyButton.interactable = false;
    }

    private void UpdateWeaponStats(ShopWeaponStatsV14.Profile profile)
    {
        EnsureStatUI();
        bool show = profile != null;
        if (statRoot != null) statRoot.gameObject.SetActive(show);
        if (!show) return;

        if (traitText != null) traitText.text = "성능 분석";
        for (int row = 0; row < 5; row++)
        {
            statLabels[row].text = profile.Labels[row];
            int value = Mathf.Clamp(profile.Values[row], 1, 5);
            for (int col = 0; col < 5; col++)
            {
                bool active = col < value;
                statCells[row, col].color = active
                    ? (value == 5 ? ActiveCellMax : ActiveCell)
                    : InactiveCell;
            }
        }
    }

    private void EnsureStatUI()
    {
        if (statRoot != null) return;
        if (galmuri == null) galmuri = Resources.Load<TMP_FontAsset>("Fonts & Materials/Galmuri11 SDF");

        GameObject root = new GameObject("V14_WeaponStats", typeof(RectTransform));
        root.transform.SetParent(transform, false);
        statRoot = root.GetComponent<RectTransform>();
        statRoot.anchorMin = statRoot.anchorMax = new Vector2(0.5f, 0.5f);
        statRoot.pivot = new Vector2(0.5f, 0.5f);
        statRoot.sizeDelta = new Vector2(300f, 168f);
        statRoot.anchoredPosition = new Vector2(0f, -42f);

        traitText = CreateText("Header", statRoot, "성능 분석", 15.5f, TextAlignmentOptions.Left);
        SetRect(traitText.rectTransform, new Vector2(-5f, 70f), new Vector2(280f, 24f), new Vector2(0.5f, 0.5f));
        traitText.color = new Color(0.55f, 0.94f, 1f, 1f);

        statLabels = new TMP_Text[5];
        statCells = new Image[5, 5];
        for (int row = 0; row < 5; row++)
        {
            float y = 42f - row * 27f;
            statLabels[row] = CreateText("Label_" + row, statRoot, "수치", 14f, TextAlignmentOptions.Left);
            SetRect(statLabels[row].rectTransform, new Vector2(-98f, y), new Vector2(86f, 22f), new Vector2(0.5f, 0.5f));
            statLabels[row].color = new Color(0.82f, 0.94f, 0.98f, 1f);

            for (int col = 0; col < 5; col++)
            {
                GameObject frame = new GameObject("CellFrame_" + row + "_" + col, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                frame.transform.SetParent(statRoot, false);
                RectTransform fr = frame.GetComponent<RectTransform>();
                SetRect(fr, new Vector2(-25f + col * 29f, y), new Vector2(22f, 16f), new Vector2(0.5f, 0.5f));
                frame.GetComponent<Image>().color = CellOutline;

                GameObject inner = new GameObject("Cell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                inner.transform.SetParent(frame.transform, false);
                RectTransform ir = inner.GetComponent<RectTransform>();
                ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
                ir.offsetMin = new Vector2(2f, 2f); ir.offsetMax = new Vector2(-2f, -2f);
                Image image = inner.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = InactiveCell;
                statCells[row, col] = image;
            }
        }
        statRoot.gameObject.SetActive(false);
    }

    private TMP_Text CreateText(string name, Transform parent, string content, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        if (galmuri != null) text.font = galmuri;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 anchor)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private void EnsureCached()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}
