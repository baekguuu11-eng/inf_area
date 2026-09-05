using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ShopUIDesignerEditor
{
    private const string RootName = "GeneratedShopUI";

    [MenuItem("Tools/Infected Area/Rebuild Korean Shop UI")]
    public static void RebuildKoreanShopUI()
    {
        GameObject shopSystem = Selection.activeGameObject;

        if (shopSystem == null || shopSystem.name != "ShopSystem")
        {
            shopSystem = GameObject.Find("ShopSystem");
        }

        if (shopSystem == null)
        {
            EditorUtility.DisplayDialog(
                "ShopSystem 없음",
                "Hierarchy에서 ShopSystem 오브젝트를 선택한 뒤 다시 실행하세요.",
                "확인"
            );
            return;
        }

        ShopManager shopManager = shopSystem.GetComponent<ShopManager>();

        if (shopManager == null)
        {
            shopManager = shopSystem.AddComponent<ShopManager>();
        }

        RectTransform shopRootRect = shopSystem.GetComponent<RectTransform>();

        if (shopRootRect == null)
        {
            shopRootRect = shopSystem.AddComponent<RectTransform>();
        }

        StretchFull(shopRootRect);

        CanvasGroup shopCanvasGroup = shopSystem.GetComponent<CanvasGroup>();

        if (shopCanvasGroup == null)
        {
            shopCanvasGroup = shopSystem.AddComponent<CanvasGroup>();
        }

        // 기존 자동 생성 UI 삭제
        Transform oldGenerated = shopSystem.transform.Find(RootName);

        if (oldGenerated != null)
        {
            Object.DestroyImmediate(oldGenerated.gameObject);
        }

        // 예전에 수동으로 만든 ShopWindow / DimOverlay는 삭제하지 않고 비활성화만 함
        Transform oldDim = shopSystem.transform.Find("DimOverlay");

        if (oldDim != null)
        {
            oldDim.gameObject.SetActive(false);
        }

        Transform oldWindow = shopSystem.transform.Find("ShopWindow");

        if (oldWindow != null)
        {
            oldWindow.gameObject.SetActive(false);
        }

        Sprite mainFrame = FindSprite("shop_main_frame");
        Sprite cardFrame = FindSprite("shop_card_frame");
        Sprite buyButtonSprite = FindSprite("shop_buy_button");
        Sprite rerollButtonSprite = FindSprite("shop_reroll_button");
        Sprite closeButtonSprite = FindSprite("shop_close_button");
        Sprite bytePanelSprite = FindSprite("shop_byte_panel");
        Sprite weaponPanelSprite = FindSprite("shop_weapon_panel");

        Sprite iconHeal = FindSprite("icon_heal");
        Sprite iconElectric = FindSprite("icon_electric");
        Sprite iconExplosion = FindSprite("icon_explosion");
        Sprite iconByte = FindSprite("icon_byte");

        TMP_FontAsset font = FindFont();

        GameObject root = CreateUIObject(RootName, shopSystem.transform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        StretchFull(rootRect);

        CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();
        rootGroup.alpha = 1f;
        rootGroup.interactable = true;
        rootGroup.blocksRaycasts = true;

        Image dimOverlay = CreateImage("DimOverlay", root.transform, null);
        StretchFull(dimOverlay.rectTransform);
        dimOverlay.color = new Color(0f, 0.02f, 0.04f, 0.58f);
        dimOverlay.raycastTarget = false;

        Image shopWindow = CreateImage("ShopWindow", root.transform, mainFrame);
        SetCenterRect(shopWindow.rectTransform, new Vector2(0f, 0f), new Vector2(1280f, 760f));
        shopWindow.type = Image.Type.Simple;
        shopWindow.preserveAspect = false;
        shopWindow.raycastTarget = false;

        TMP_Text titleText = CreateText("TitleText", shopWindow.transform, "감염구역 보급 단말", font, 50, TextAlignmentOptions.Center);
        SetCenterRect(titleText.rectTransform, new Vector2(0f, 300f), new Vector2(680f, 70f));
        titleText.color = Hex("00E5FF");

        TMP_Text subtitleText = CreateText("SubtitleText", shopWindow.transform, "필요한 장비를 선택하세요", font, 24, TextAlignmentOptions.Center);
        SetCenterRect(subtitleText.rectTransform, new Vector2(0f, 252f), new Vector2(600f, 40f));
        subtitleText.color = Hex("79F8FF");

        ShopCardUI leftCard = CreateCard(
            "Card_Left",
            shopWindow.transform,
            new Vector2(-375f, -30f),
            cardFrame,
            buyButtonSprite,
            iconElectric,
            "공격 데이터",
            "전 범위 공격력 +15%\n방 클리어까지 지속",
            "15 BYTE",
            font
        );

        ShopCardUI centerCard = CreateCard(
            "Card_Center",
            shopWindow.transform,
            new Vector2(0f, -30f),
            cardFrame,
            buyButtonSprite,
            iconExplosion,
            "기관총",
            "누르고 있으면 발사합니다.\n연사 속도 높음, 탄약 소모 큼.",
            "22 BYTE",
            font
        );

        ShopCardUI rightCard = CreateCard(
            "Card_Right",
            shopWindow.transform,
            new Vector2(375f, -30f),
            cardFrame,
            buyButtonSprite,
            iconHeal,
            "치료 패치",
            "체력을 1 회복합니다.\n즉시 사용됩니다.",
            "8 BYTE",
            font
        );

        Button rerollButton = CreateButton("RerollButton", shopWindow.transform, rerollButtonSprite, font);
        SetCenterRect(rerollButton.GetComponent<RectTransform>(), new Vector2(0f, -336f), new Vector2(420f, 92f));
        SetButtonLabel(rerollButton, "새로고침\n5 BYTE", font, 30, Hex("DFFFFF"));
        SetButtonTransition(rerollButton);

        Image topStatusPanel = CreateImage("TopStatusPanel", root.transform, bytePanelSprite);
        SetAnchorRect(topStatusPanel.rectTransform, new Vector2(0f, 1f), new Vector2(155f, -70f), new Vector2(310f, 120f));
        topStatusPanel.type = Image.Type.Simple;
        topStatusPanel.raycastTarget = false;

        TMP_Text currencyText = CreateText("CurrencyText", topStatusPanel.transform, "BYTE  0000", font, 28, TextAlignmentOptions.Left);
        SetCenterRect(currencyText.rectTransform, new Vector2(18f, 28f), new Vector2(245f, 38f));
        currencyText.color = Hex("00E5FF");

        Image byteIcon = CreateImage("ByteIcon", topStatusPanel.transform, iconByte);
        SetCenterRect(byteIcon.rectTransform, new Vector2(-40f, 28f), new Vector2(28f, 28f));
        byteIcon.raycastTarget = false;

        TMP_Text heartText = CreateText("HeartText", topStatusPanel.transform, "♥ ♥ ♥ ♥ ♥", font, 31, TextAlignmentOptions.Left);
        SetCenterRect(heartText.rectTransform, new Vector2(0f, -27f), new Vector2(260f, 40f));
        heartText.color = Hex("FF4E5D");

        Button closeButton = CreateButton("CloseButton", root.transform, closeButtonSprite, font);
        SetAnchorRect(closeButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-108f, -55f), new Vector2(200f, 80f));
        SetButtonLabel(closeButton, "닫기", font, 34, Hex("FF6868"));
        SetButtonTransition(closeButton);

        Image weaponPanel = CreateImage("WeaponPanel", root.transform, weaponPanelSprite);
        SetAnchorRect(weaponPanel.rectTransform, new Vector2(1f, 0f), new Vector2(-225f, 85f), new Vector2(420f, 150f));
        weaponPanel.type = Image.Type.Simple;
        weaponPanel.raycastTarget = false;

        TMP_Text weaponTitle = CreateText("WeaponNameText", weaponPanel.transform, "디버그 소드", font, 24, TextAlignmentOptions.Center);
        SetCenterRect(weaponTitle.rectTransform, new Vector2(65f, 35f), new Vector2(260f, 36f));
        weaponTitle.color = Hex("DFFFFF");

        TMP_Text weaponType = CreateText("WeaponTypeText", weaponPanel.transform, "근접 무기     ∞", font, 22, TextAlignmentOptions.Center);
        SetCenterRect(weaponType.rectTransform, new Vector2(65f, -15f), new Vector2(260f, 36f));
        weaponType.color = Hex("00E5FF");

        // ShopManager 자동 연결
        SerializedObject so = new SerializedObject(shopManager);

        SetObject(so, "shopPanel", shopSystem);
        SetObject(so, "shopCanvasGroup", shopCanvasGroup);
        SetObject(so, "dimOverlay", dimOverlay);

        SetObject(so, "leftCard", leftCard);
        SetObject(so, "centerCard", centerCard);
        SetObject(so, "rightCard", rightCard);

        SetObject(so, "currencyText", currencyText);
        SetObject(so, "exitButton", closeButton);

        SetObject(so, "rerollButton", rerollButton);
        SetObject(so, "rerollButtonText", rerollButton.GetComponentInChildren<TMP_Text>(true));

        PlayerHealth playerHealth = Object.FindAnyObjectByType<PlayerHealth>();

        if (playerHealth != null)
        {
            SetObject(so, "playerHealth", playerHealth);
        }

        SetVector2(so, "leftCardStartOffset", new Vector2(-900f, 0f));
        SetVector2(so, "centerCardStartOffset", new Vector2(0f, 700f));
        SetVector2(so, "rightCardStartOffset", new Vector2(900f, 0f));
        SetFloat(so, "cardAppearDelay", 0.28f);

        so.ApplyModifiedProperties();

        // 시작 시 안 보이게
        shopCanvasGroup.alpha = 0f;
        shopCanvasGroup.interactable = false;
        shopCanvasGroup.blocksRaycasts = false;

        EditorUtility.SetDirty(shopSystem);
        EditorUtility.SetDirty(shopManager);

        Selection.activeGameObject = shopSystem;

        EditorUtility.DisplayDialog(
            "상점 UI 재생성 완료",
            "기존 GeneratedShopUI를 정리하고 새 한글 상점 UI를 생성했습니다.\nPlay 후 B키로 확인하세요.",
            "확인"
        );
    }

    private static ShopCardUI CreateCard(
        string name,
        Transform parent,
        Vector2 position,
        Sprite cardFrame,
        Sprite buyButtonSprite,
        Sprite iconSprite,
        string itemName,
        string desc,
        string price,
        TMP_FontAsset font
    )
    {
        Image cardImage = CreateImage(name, parent, cardFrame);
        SetCenterRect(cardImage.rectTransform, position, new Vector2(318f, 510f));
        cardImage.type = Image.Type.Simple;
        cardImage.raycastTarget = true;

        CanvasGroup canvasGroup = cardImage.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        ShopCardUI cardUI = cardImage.gameObject.GetComponent<ShopCardUI>();

        if (cardUI == null)
        {
            cardUI = cardImage.gameObject.AddComponent<ShopCardUI>();
        }

        TMP_Text nameText = CreateText("NameText", cardImage.transform, itemName, font, 31, TextAlignmentOptions.Center);
        SetCenterRect(nameText.rectTransform, new Vector2(0f, 200f), new Vector2(270f, 50f));
        nameText.color = Color.white;

        Image iconImage = CreateImage("ItemIcon", cardImage.transform, iconSprite);
        SetCenterRect(iconImage.rectTransform, new Vector2(0f, 90f), new Vector2(125f, 125f));
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        TMP_Text descText = CreateText("DescText", cardImage.transform, desc, font, 21, TextAlignmentOptions.Center);
        SetCenterRect(descText.rectTransform, new Vector2(0f, -45f), new Vector2(245f, 96f));
        descText.color = Hex("EAFBFF");

        TMP_Text priceText = CreateText("PriceText", cardImage.transform, price, font, 27, TextAlignmentOptions.Center);
        SetCenterRect(priceText.rectTransform, new Vector2(0f, -148f), new Vector2(220f, 40f));
        priceText.color = Hex("00E5FF");

        Button buyButton = CreateButton("BuyButton", cardImage.transform, buyButtonSprite, font);
        SetCenterRect(buyButton.GetComponent<RectTransform>(), new Vector2(0f, -212f), new Vector2(235f, 62f));
        SetButtonLabel(buyButton, "구매", font, 32, Color.white);
        SetButtonTransition(buyButton);

        SerializedObject cardSO = new SerializedObject(cardUI);
        SetObject(cardSO, "nameText", nameText);
        SetObject(cardSO, "descText", descText);
        SetObject(cardSO, "priceText", priceText);
        SetObject(cardSO, "buyButton", buyButton);
        SetObject(cardSO, "buttonText", buyButton.GetComponentInChildren<TMP_Text>(true));
        SetFloat(cardSO, "enterDuration", 0.72f);
        cardSO.ApplyModifiedProperties();

        return cardUI;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;

        if (sprite == null)
        {
            image.color = new Color(0.03f, 0.14f, 0.18f, 0.85f);
            Debug.LogWarning(name + "에 연결할 Sprite를 찾지 못했습니다.");
        }

        return image;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string text,
        TMP_FontAsset font,
        int size,
        TextAlignmentOptions alignment
    )
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TMP_Text tmp = go.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;

        if (font != null)
        {
            tmp.font = font;
        }
        else
        {
            Debug.LogWarning("Galmuri11 SDF 폰트를 찾지 못했습니다. TMP 폰트를 직접 확인하세요.");
        }

        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, Sprite sprite, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = true;
        image.type = Image.Type.Simple;

        if (sprite == null)
        {
            image.color = new Color(0f, 0.6f, 0.7f, 0.85f);
            Debug.LogWarning(name + " 버튼 Sprite를 찾지 못했습니다.");
        }

        Button button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;

        return button;
    }

    private static void SetButtonLabel(Button button, string text, TMP_FontAsset font, int size, Color color)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

        if (label == null)
        {
            label = CreateText("ButtonText", button.transform, text, font, size, TextAlignmentOptions.Center);
            StretchFull(label.rectTransform);
        }

        label.name = "ButtonText";
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        if (font != null)
        {
            label.font = font;
        }
    }

    private static void SetButtonTransition(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetCenterRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetAnchorRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static Sprite FindSprite(string spriteName)
    {
        string[] guids = AssetDatabase.FindAssets(spriteName);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

            if (assets == null)
            {
                continue;
            }

            for (int j = 0; j < assets.Length; j++)
            {
                Sprite sprite = assets[j] as Sprite;

                if (sprite != null && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            Sprite singleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (singleSprite != null && singleSprite.name == spriteName)
            {
                return singleSprite;
            }
        }

        Debug.LogWarning("Sprite를 찾지 못함: " + spriteName);
        return null;
    }

    private static TMP_FontAsset FindFont()
    {
        string[] fontNames =
        {
            "Galmuri11 SDF",
            "Galmuri11",
            "NeoDunggeunmo SDF",
            "NeoDunggeunmo"
        };

        for (int i = 0; i < fontNames.Length; i++)
        {
            string[] guids = AssetDatabase.FindAssets(fontNames[i] + " t:TMP_FontAsset");

            for (int j = 0; j < guids.Length; j++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[j]);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

                if (font != null)
                {
                    return font;
                }
            }
        }

        return null;
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetVector2(SerializedObject serializedObject, string propertyName, Vector2 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
        {
            property.vector2Value = value;
        }
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }
}