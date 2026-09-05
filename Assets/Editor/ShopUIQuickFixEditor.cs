using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ShopUIQuickFixEditor
{
    [MenuItem("Tools/Infected Area/Fix Current Shop UI")]
    public static void FixCurrentShopUI()
    {
        GameObject shopSystem = GameObject.Find("ShopSystem");
        if (shopSystem == null)
        {
            Debug.LogError("ShopSystem을 찾지 못했습니다.");
            return;
        }

        Transform generated = shopSystem.transform.Find("GeneratedShopUI");
        if (generated == null)
        {
            Debug.LogError("GeneratedShopUI를 찾지 못했습니다.");
            return;
        }

        Transform oldWindow = shopSystem.transform.Find("ShopWindow");
        if (oldWindow != null)
        {
            oldWindow.gameObject.SetActive(false);
        }

        Transform weaponPanel = generated.Find("WeaponPanel");
        if (weaponPanel != null)
        {
            weaponPanel.gameObject.SetActive(false);
        }

        TMP_FontAsset koreanFont = FindKoreanFont();
        if (koreanFont == null)
        {
            Debug.LogWarning("한글 TMP 폰트를 찾지 못했습니다. Galmuri11 SDF 또는 Korean_TMP를 확인하세요.");
        }

        TMP_Text[] texts = generated.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (koreanFont != null)
                text.font = koreanFont;

            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = false;
            text.color = Color.white;
        }

        ApplyTextStyle(generated, "TitleText", 42, FontStyles.Bold, TextAlignmentOptions.Center);
        ApplyTextStyle(generated, "SubtitleText", 18, FontStyles.Normal, TextAlignmentOptions.Center);
        ApplyTextStyle(generated, "CurrencyText", 24, FontStyles.Bold, TextAlignmentOptions.Left);
        ApplyTextStyle(generated, "NameText", 24, FontStyles.Bold, TextAlignmentOptions.Center, true);
        ApplyTextStyle(generated, "DescText", 14, FontStyles.Normal, TextAlignmentOptions.Center, true);
        ApplyTextStyle(generated, "PriceText", 18, FontStyles.Bold, TextAlignmentOptions.Center, true);

        ResizeIfExists(generated, "Card_Left/ItemIcon", new Vector2(84, 84));
        ResizeIfExists(generated, "Card_Center/ItemIcon", new Vector2(84, 84));
        ResizeIfExists(generated, "Card_Right/ItemIcon", new Vector2(84, 84));

        HideButtonLabelIfExists(generated, "CloseButton");
        HideButtonLabelIfExists(generated, "RerollButton");
        HideButtonLabelIfExists(generated, "Card_Left/BuyButton");
        HideButtonLabelIfExists(generated, "Card_Center/BuyButton");
        HideButtonLabelIfExists(generated, "Card_Right/BuyButton");

        EditorUtility.SetDirty(shopSystem);
        Debug.Log("Shop UI 자동 보정 완료");
    }

    private static TMP_FontAsset FindKoreanFont()
    {
        string[] candidates =
        {
            "Galmuri11 SDF",
            "Korean_TMP",
            "Korean_Dynamic_TMP",
            "MALGUN_KOREAN SDF",
            "MALGUN_TMP"
        };

        foreach (string name in candidates)
        {
            string[] guids = AssetDatabase.FindAssets(name + " t:TMP_FontAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null)
                    return font;
            }
        }

        return null;
    }

    private static void ApplyTextStyle(
        Transform root,
        string objectName,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        bool multiple = false)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if ((!multiple && text.name == objectName) ||
                (multiple && text.name.Contains(objectName)))
            {
                text.fontSize = fontSize;
                text.fontStyle = style;
                text.alignment = alignment;
            }
        }
    }

    private static void ResizeIfExists(Transform root, string path, Vector2 size)
    {
        Transform target = root.Find(path);
        if (target == null) return;

        RectTransform rt = target as RectTransform;
        if (rt != null)
            rt.sizeDelta = size;
    }

    private static void HideButtonLabelIfExists(Transform root, string buttonPath)
    {
        Transform button = root.Find(buttonPath);
        if (button == null) return;

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            text.gameObject.SetActive(false);
        }
    }
}