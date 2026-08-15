#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuButtonRepairTool
{
    [MenuItem("Tools/INFECTED AREA/Repair Main Menu Buttons")]
    public static void Repair()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "Play 모드를 끈 뒤 다시 실행하세요.",
                "확인"
            );
            return;
        }

        Canvas canvas = FindMainMenuCanvas();

        if (canvas == null)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "Main Menu Canvas를 찾지 못했습니다.",
                "확인"
            );
            return;
        }

        Transform menuGroup = FindRecursive(canvas.transform, "MenuGroup");

        if (menuGroup == null)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "MenuGroup을 찾지 못했습니다.",
                "확인"
            );
            return;
        }

        Button start = FindButton(menuGroup, "StartButton");
        Button setting = FindButton(menuGroup, "SettingButton");
        Button makers = FindButton(menuGroup, "MakersButton");
        Button quit = FindButton(menuGroup, "QuitButton");

        // 혹시 예전 이름이 남아 있는 경우도 처리
        if (makers == null)
            makers = FindButton(menuGroup, "InfoButton");

        if (quit == null)
            quit = FindButton(menuGroup, "CreditButton");

        if (start == null || setting == null || makers == null || quit == null)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "START / SETTING / MAKERS / QUIT 버튼을 모두 찾지 못했습니다.",
                "확인"
            );
            return;
        }

        Undo.SetCurrentGroupName("Repair Main Menu Buttons");
        int undoGroup = Undo.GetCurrentGroup();

        makers.gameObject.name = "MakersButton";
        quit.gameObject.name = "QuitButton";

        TextMeshProUGUI startText = start.GetComponentInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI settingText = setting.GetComponentInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI makersText = makers.GetComponentInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI quitText = quit.GetComponentInChildren<TextMeshProUGUI>(true);

        if (makersText != null)
        {
            Undo.RecordObject(makersText, "Fix Makers Text");
            makersText.text = "MAKERS";
            makersText.gameObject.name = "Text (TMP)_Makers";
        }

        if (quitText != null)
        {
            Undo.RecordObject(quitText, "Fix Quit Text");
            quitText.text = "QUIT";
            quitText.gameObject.name = "Text (TMP)_Quit";
        }

        // 1) 버튼 위치와 Hierarchy 순서를 START -> SETTING -> MAKERS -> QUIT으로 고정
        RectTransform startRt = start.GetComponent<RectTransform>();
        RectTransform settingRt = setting.GetComponent<RectTransform>();
        RectTransform makersRt = makers.GetComponent<RectTransform>();
        RectTransform quitRt = quit.GetComponent<RectTransform>();

        float spacing = 70f;

        if (startRt != null && settingRt != null)
        {
            float measured = startRt.anchoredPosition.y - settingRt.anchoredPosition.y;

            if (Mathf.Abs(measured) > 5f)
                spacing = Mathf.Abs(measured);
        }

        if (settingRt != null)
        {
            CopyButtonRectLayout(settingRt, makersRt);
            CopyButtonRectLayout(settingRt, quitRt);

            Vector2 settingPos = settingRt.anchoredPosition;

            if (makersRt != null)
                makersRt.anchoredPosition = new Vector2(settingPos.x, settingPos.y - spacing);

            if (quitRt != null)
                quitRt.anchoredPosition = new Vector2(settingPos.x, settingPos.y - spacing * 2f);
        }

        start.transform.SetSiblingIndex(0);
        setting.transform.SetSiblingIndex(1);
        makers.transform.SetSiblingIndex(2);
        quit.transform.SetSiblingIndex(3);

        // 2) MAKERS 두 줄 방지. SETTING의 텍스트 레이아웃/폰트 크기를 기준으로 통일
        if (settingText != null)
        {
            MatchTextLayout(settingText, makersText, "MAKERS");
            MatchTextLayout(settingText, quitText, "QUIT");
        }

        // 3) 삭제된 예전 Hover 스크립트 때문에 생긴 Missing Script 제거 후 새 Hover 컴포넌트 재부착
        AudioClip hoverClip = FindAudioClip("Hover");
        AudioClip clickClip = FindAudioClip("Button_Click");

        RepairHover(start.gameObject, startText, hoverClip, clickClip);
        RepairHover(setting.gameObject, settingText, hoverClip, clickClip);
        RepairHover(makers.gameObject, makersText, hoverClip, clickClip);
        RepairHover(quit.gameObject, quitText, hoverClip, clickClip);

        // 인트로 등장 순서도 START -> SETTING -> MAKERS -> QUIT으로 고정
        RepairIntroOrder(
            menuGroup,
            startRt,
            settingRt,
            makersRt,
            quitRt
        );

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Selection.activeGameObject = menuGroup.gameObject;

        string soundInfo =
            (hoverClip != null ? "Hover 사운드 연결됨" : "Hover 사운드 자동 검색 실패") +
            "\n" +
            (clickClip != null ? "Click 사운드 연결됨" : "Click 사운드 자동 검색 실패");

        EditorUtility.DisplayDialog(
            "INFECTED AREA",
            "메인메뉴 버튼 복구 완료!\n\n" +
            "순서: START → SETTING → MAKERS → QUIT\n" +
            "MAKERS 줄바꿈 수정\n" +
            "Hover 컴포넌트 복구\n\n" +
            soundInfo,
            "확인"
        );
    }

    private static void RepairHover(
        GameObject buttonObject,
        TextMeshProUGUI label,
        AudioClip hoverClip,
        AudioClip clickClip)
    {
        if (buttonObject == null)
            return;

        // 삭제된 원본 스크립트의 Missing MonoBehaviour 제거
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(buttonObject);

        MenuTextButtonHover hover = buttonObject.GetComponent<MenuTextButtonHover>();

        if (hover == null)
            hover = Undo.AddComponent<MenuTextButtonHover>(buttonObject);

        Image underline = FindUnderlineImage(buttonObject.transform);

        if (underline == null)
            underline = CreateUnderline(buttonObject.transform);

        // Button이 클릭/호버 Raycast를 받을 Graphic 보장
        Image rootImage = buttonObject.GetComponent<Image>();

        if (rootImage == null)
        {
            rootImage = Undo.AddComponent<Image>(buttonObject);
            rootImage.color = new Color(1f, 1f, 1f, 0f);
        }

        rootImage.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();

        if (button != null)
        {
            button.interactable = true;

            if (button.targetGraphic == null)
                button.targetGraphic = rootImage;
        }

        if (label != null)
            label.raycastTarget = false;

        SerializedObject so = new SerializedObject(hover);
        SetObject(so, "labelText", label);
        SetObject(so, "underlineImage", underline);
        SetObject(so, "hoverSound", hoverClip);
        SetObject(so, "clickSound", clickClip);

        SerializedProperty useAutoWidth = so.FindProperty("useAutoUnderlineWidth");
        if (useAutoWidth != null)
            useAutoWidth.boolValue = true;

        SerializedProperty playHover = so.FindProperty("playHoverSound");
        if (playHover != null)
            playHover.boolValue = true;

        SerializedProperty playClick = so.FindProperty("playClickSound");
        if (playClick != null)
            playClick.boolValue = true;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hover);
    }

    private static void RepairIntroOrder(
        Transform menuGroup,
        RectTransform start,
        RectTransform setting,
        RectTransform makers,
        RectTransform quit)
    {
        MainMenuIntroAnimator intro = menuGroup.GetComponent<MainMenuIntroAnimator>();

        if (intro == null)
            intro = Object.FindFirstObjectByType<MainMenuIntroAnimator>();

        if (intro == null)
            return;

        SerializedObject so = new SerializedObject(intro);
        SerializedProperty items = so.FindProperty("menuItems");

        if (items != null)
        {
            items.arraySize = 4;
            items.GetArrayElementAtIndex(0).objectReferenceValue = start;
            items.GetArrayElementAtIndex(1).objectReferenceValue = setting;
            items.GetArrayElementAtIndex(2).objectReferenceValue = makers;
            items.GetArrayElementAtIndex(3).objectReferenceValue = quit;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(intro);
    }

    private static void MatchTextLayout(
        TextMeshProUGUI source,
        TextMeshProUGUI target,
        string textValue)
    {
        if (source == null || target == null)
            return;

        Undo.RecordObject(target, "Match Button Text Layout");

        target.text = textValue;
        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
        target.fontSize = source.fontSize;
        target.fontStyle = source.fontStyle;
        target.alignment = source.alignment;
        target.enableAutoSizing = false;
        target.textWrappingMode = TextWrappingModes.NoWrap;
        target.overflowMode = TextOverflowModes.Overflow;
        target.raycastTarget = false;

        RectTransform sourceRt = source.rectTransform;
        RectTransform targetRt = target.rectTransform;

        targetRt.anchorMin = sourceRt.anchorMin;
        targetRt.anchorMax = sourceRt.anchorMax;
        targetRt.pivot = sourceRt.pivot;
        targetRt.anchoredPosition = sourceRt.anchoredPosition;
        targetRt.sizeDelta = sourceRt.sizeDelta;
        targetRt.localScale = Vector3.one;
    }

    private static void CopyButtonRectLayout(
        RectTransform source,
        RectTransform target)
    {
        if (source == null || target == null)
            return;

        Undo.RecordObject(target, "Match Button Rect");

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta;
        target.localScale = Vector3.one;
    }

    private static Image FindUnderlineImage(Transform button)
    {
        for (int i = 0; i < button.childCount; i++)
        {
            Transform child = button.GetChild(i);
            string normalized = child.name.Replace("_", "").ToLower();

            if (normalized.Contains("underline"))
                return child.GetComponent<Image>();
        }

        return null;
    }

    private static Image CreateUnderline(Transform button)
    {
        GameObject go = new GameObject(
            "UnderLine",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        Undo.RegisterCreatedObjectUndo(go, "Create UnderLine");
        go.transform.SetParent(button, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 4f);
        rt.sizeDelta = new Vector2(0f, 3f);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.45f, 0.95f, 1f, 0f);
        image.raycastTarget = false;

        return image;
    }

    private static AudioClip FindAudioClip(string exactName)
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string filename = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(
                filename,
                exactName,
                System.StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }

        return null;
    }

    private static Canvas FindMainMenuCanvas()
    {
        GameObject exact = GameObject.Find("Main Menu Canvas");

        if (exact != null)
        {
            Canvas canvas = exact.GetComponent<Canvas>();

            if (canvas != null)
                return canvas;
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < canvases.Length; i++)
        {
            string lower = canvases[i].name.ToLower();

            if (lower.Contains("main") && lower.Contains("menu"))
                return canvases[i];
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }

    private static Button FindButton(Transform parent, string name)
    {
        Transform found = FindRecursive(parent, name);

        return found != null
            ? found.GetComponent<Button>()
            : null;
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindRecursive(parent.GetChild(i), name);

            if (found != null)
                return found;
        }

        return null;
    }

    private static void SetObject(
        SerializedObject so,
        string propertyName,
        Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }
}
#endif
