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

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (rectTransform != null) originalPosition = rectTransform.anchoredPosition;
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
        if (descText != null) descText.text = item.description;
        if (priceText != null) priceText.text = item.price + " Byte";
        if (buttonText != null) buttonText.text = "BUY";
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
        if (buttonText != null) buttonText.text = "SOLD OUT";
        if (buyButton != null) buyButton.interactable = false;
    }

    private void EnsureCached()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}
