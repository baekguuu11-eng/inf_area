using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopCardUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Button buyButton;
    [SerializeField] private TMP_Text buttonText;

    [Header("Animation")]
    [SerializeField] private float enterDuration = 0.65f;

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

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        originalPosition = rectTransform.anchoredPosition;
    }

    public void Setup(ShopManager manager, ShopManager.ShopItem item)
    {
        shopManager = manager;
        currentItem = item;
        isSoldOut = false;

        if (nameText != null)
        {
            nameText.text = item.itemName;
        }

        if (descText != null)
        {
            descText.text = item.description;
        }

        if (priceText != null)
        {
            priceText.text = item.price + " Byte";
        }

        if (buttonText != null)
        {
            buttonText.text = "BUY";
        }

        if (buyButton != null)
        {
            buyButton.interactable = true;
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(Buy);
        }
    }

    public void HideInstant()
    {
        StopAllCoroutines();

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 현재 배치된 위치를 이 카드의 최종 위치로 저장
        originalPosition = rectTransform.anchoredPosition;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void PlayEnterAnimation(Vector2 startOffset)
    {
        StopAllCoroutines();
        StartCoroutine(EnterRoutine(startOffset));
    }

    private IEnumerator EnterRoutine(Vector2 startOffset)
    {
        Vector2 startPosition = originalPosition + startOffset;
        Vector2 endPosition = originalPosition;

        rectTransform.anchoredPosition = startPosition;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float elapsed = 0f;

        while (elapsed < enterDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / enterDuration);
            float eased = EaseOutCubic(t);

            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, endPosition, eased);

            yield return null;
        }

        rectTransform.anchoredPosition = endPosition;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    private void Buy()
    {
        if (isSoldOut)
        {
            return;
        }

        if (shopManager == null)
        {
            return;
        }

        bool success = shopManager.TryPurchase(currentItem, this);

        if (success)
        {
            SetSoldOut();
        }
    }

    public void SetSoldOut()
    {
        isSoldOut = true;

        if (buttonText != null)
        {
            buttonText.text = "SOLD OUT";
        }

        if (buyButton != null)
        {
            buyButton.interactable = false;
        }
    }

    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}