using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ShopUIVisibilityFixEditor
{
    [MenuItem("Tools/Infected Area/Fix Shop Visibility")]
    public static void FixShopVisibility()
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

        // ShopSystem 자체는 항상 켜져 있어야 함
        shopSystem.SetActive(true);
        generated.gameObject.SetActive(true);

        // 예전 수동 UI는 끄기
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

        // ShopSystem CanvasGroup은 항상 보이게 고정
        CanvasGroup shopSystemGroup = shopSystem.GetComponent<CanvasGroup>();
        if (shopSystemGroup == null)
        {
            shopSystemGroup = shopSystem.AddComponent<CanvasGroup>();
        }

        shopSystemGroup.alpha = 1f;
        shopSystemGroup.interactable = true;
        shopSystemGroup.blocksRaycasts = true;

        // 실제 상점 UI는 GeneratedShopUI CanvasGroup으로 제어
        CanvasGroup generatedGroup = generated.GetComponent<CanvasGroup>();
        if (generatedGroup == null)
        {
            generatedGroup = generated.gameObject.AddComponent<CanvasGroup>();
        }

        // 에디터에서는 일단 보이게 해둠
        generatedGroup.alpha = 1f;
        generatedGroup.interactable = true;
        generatedGroup.blocksRaycasts = true;

        // 자식 CanvasGroup들이 투명으로 잠겨 있으면 전부 풀기
        CanvasGroup[] childGroups = generated.GetComponentsInChildren<CanvasGroup>(true);
        foreach (CanvasGroup group in childGroups)
        {
            if (group == generatedGroup)
            {
                continue;
            }

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        ShopManager manager = shopSystem.GetComponent<ShopManager>();
        if (manager == null)
        {
            Debug.LogError("ShopSystem에 ShopManager가 없습니다.");
            return;
        }

        Image dimOverlay = FindChild<Image>(generated, "DimOverlay");
        ShopCardUI leftCard = FindChild<ShopCardUI>(generated, "Card_Left");
        ShopCardUI centerCard = FindChild<ShopCardUI>(generated, "Card_Center");
        ShopCardUI rightCard = FindChild<ShopCardUI>(generated, "Card_Right");
        TMP_Text currencyText = FindChild<TMP_Text>(generated, "CurrencyText");
        Button closeButton = FindChild<Button>(generated, "CloseButton");
        Button rerollButton = FindChild<Button>(generated, "RerollButton");

        TMP_Text rerollText = null;
        if (rerollButton != null)
        {
            rerollText = rerollButton.GetComponentInChildren<TMP_Text>(true);
        }

        SerializedObject so = new SerializedObject(manager);

        SetObject(so, "shopPanel", generated.gameObject);
        SetObject(so, "shopCanvasGroup", generatedGroup);
        SetObject(so, "dimOverlay", dimOverlay);

        SetObject(so, "leftCard", leftCard);
        SetObject(so, "centerCard", centerCard);
        SetObject(so, "rightCard", rightCard);

        SetObject(so, "currencyText", currencyText);
        SetObject(so, "exitButton", closeButton);

        SetObject(so, "rerollButton", rerollButton);
        SetObject(so, "rerollButtonText", rerollText);

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(shopSystem);
        EditorUtility.SetDirty(generated.gameObject);
        EditorUtility.SetDirty(manager);

        Selection.activeGameObject = generated.gameObject;

        Debug.Log("상점 표시/연결 자동 수정 완료. 이제 Play 후 B키로 확인하세요.");
    }

    private static T FindChild<T>(Transform root, string childName) where T : Component
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child.name == childName)
            {
                T component = child.GetComponent<T>();

                if (component != null)
                {
                    return component;
                }
            }
        }

        Debug.LogWarning(childName + " 을 찾지 못했습니다.");
        return null;
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null)
        {
            property.objectReferenceValue = value;
        }
        else
        {
            Debug.LogWarning("프로퍼티를 찾지 못함: " + propertyName);
        }
    }
}