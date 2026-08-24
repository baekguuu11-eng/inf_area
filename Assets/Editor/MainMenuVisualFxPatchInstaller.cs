#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuVisualFxPatchInstaller
{
    private static readonly Color CyanLine =
        new Color(0.35f, 0.95f, 1f, 1f);

    private static readonly Color RedLine =
        new Color(1f, 0.18f, 0.18f, 1f);

    private static readonly Color StatusCyan =
        new Color(0.56f, 0.88f, 0.92f, 0.72f);

    private static readonly Color StatusWarning =
        new Color(1f, 0.78f, 0.18f, 0.82f);

    [MenuItem("Tools/INFECTED AREA/Apply Visual FX Patch")]
    public static void ApplyPatch()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "Play 모드를 끈 뒤 실행하세요.",
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

        Transform subPanelRoot =
            FindRecursive(
                canvas.transform,
                "SubPanelRoot"
            );

        Transform settingsPanel =
            FindRecursive(
                canvas.transform,
                "SettingsPanel"
            );

        Transform makersPanel =
            FindRecursive(
                canvas.transform,
                "MakersPanel"
            );

        Transform quitPanel =
            FindRecursive(
                canvas.transform,
                "QuitPanel"
            );

        if (subPanelRoot == null ||
            settingsPanel == null ||
            makersPanel == null ||
            quitPanel == null)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "SubPanelRoot / SettingsPanel / MakersPanel / QuitPanel 중 일부가 없습니다.\n먼저 기존 Subpanel 자동 생성을 끝낸 뒤 다시 실행하세요.",
                "확인"
            );
            return;
        }

        Undo.SetCurrentGroupName(
            "Apply Main Menu Visual FX Patch"
        );

        int undoGroup =
            Undo.GetCurrentGroup();

        MainMenuPanelConstructAnimator settingsAnimator =
            SetupPanel(
                settingsPanel,
                false
            );

        MainMenuPanelConstructAnimator makersAnimator =
            SetupPanel(
                makersPanel,
                false
            );

        MainMenuPanelConstructAnimator quitAnimator =
            SetupPanel(
                quitPanel,
                true
            );

        ApplyMakersNameStyle(makersPanel);

        SetupSystemStatus(canvas.transform);
        SetupMicroGlitch(canvas.transform);
        ReduceOldAtmosphereGlitch();

        ReconnectManagerDisplayFields(
            canvas.transform
        );

        EditorSceneManager.MarkSceneDirty(
            canvas.gameObject.scene
        );

        Undo.CollapseUndoOperations(
            undoGroup
        );

        EditorUtility.DisplayDialog(
            "INFECTED AREA",
            "Visual FX 패치 적용 완료!\n\n" +
            "• SETTING / MAKERS / QUIT 패널 생성 애니메이션\n" +
            "• MAKERS 이름: Galmuri11 SDF / 19 / 노란색\n" +
            "• ARK / RAFAEL 시스템 상태 표시\n" +
            "• 매우 약한 간헐적 화면 글리치\n\n" +
            "Ctrl + S로 씬을 저장한 뒤 Play로 확인하세요.",
            "확인"
        );
    }

    private static MainMenuPanelConstructAnimator SetupPanel(
        Transform panel,
        bool dangerStyle)
    {
        GameObject panelObject =
            panel.gameObject;

        Image background =
            panelObject.GetComponent<Image>();

        if (background == null)
            background =
                Undo.AddComponent<Image>(
                    panelObject
                );

        Transform oldLine =
            panel.Find("ConstructFX_Line");

        Image line;

        if (oldLine == null)
        {
            GameObject lineObject =
                new GameObject(
                    "ConstructFX_Line",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );

            Undo.RegisterCreatedObjectUndo(
                lineObject,
                "Create panel construct line"
            );

            lineObject.transform.SetParent(
                panel,
                false
            );

            line =
                lineObject.GetComponent<Image>();

            line.raycastTarget = false;
        }
        else
        {
            line =
                oldLine.GetComponent<Image>();

            if (line == null)
                line =
                    Undo.AddComponent<Image>(
                        oldLine.gameObject
                    );
        }

        line.color = dangerStyle
            ? RedLine
            : CyanLine;

        List<CanvasGroup> groups =
            new List<CanvasGroup>();

        for (int i = 0;
             i < panel.childCount;
             i++)
        {
            Transform child =
                panel.GetChild(i);

            if (child == line.transform)
                continue;

            CanvasGroup group =
                child.GetComponent<CanvasGroup>();

            if (group == null)
            {
                group =
                    Undo.AddComponent<CanvasGroup>(
                        child.gameObject
                    );
            }

            group.alpha = 1f;
            groups.Add(group);
        }

        MainMenuPanelConstructAnimator animator =
            panelObject.GetComponent
            <MainMenuPanelConstructAnimator>();

        if (animator == null)
        {
            animator =
                Undo.AddComponent
                <MainMenuPanelConstructAnimator>(
                    panelObject
                );
        }

        animator.Configure(
            background,
            line,
            groups.ToArray(),
            dangerStyle
        );

        EditorUtility.SetDirty(animator);

        return animator;
    }

    private static void ApplyMakersNameStyle(
        Transform makersPanel)
    {
        TMP_FontAsset galmuri =
            FindGalmuri11Font();

        TextMeshProUGUI[] texts =
            makersPanel.GetComponentsInChildren
            <TextMeshProUGUI>(true);

        for (int i = 0;
             i < texts.Length;
             i++)
        {
            TextMeshProUGUI text =
                texts[i];

            if (text == null)
                continue;

            if (!string.Equals(
                    text.gameObject.name,
                    "Name",
                    System.StringComparison
                        .OrdinalIgnoreCase))
            {
                continue;
            }

            Undo.RecordObject(
                text,
                "Style Makers Name"
            );

            if (galmuri != null)
                text.font = galmuri;

            text.fontSize = 19f;
            text.color = Color.yellow;
            text.enableWordWrapping = false;
            text.overflowMode =
                TextOverflowModes.Overflow;

            EditorUtility.SetDirty(text);
        }
    }

    private static void SetupSystemStatus(
        Transform canvas)
    {
        Transform existing =
            FindDirectChild(
                canvas,
                "SystemStatusOverlay"
            );

        if (existing != null)
            Undo.DestroyObjectImmediate(
                existing.gameObject
            );

        GameObject root =
            new GameObject(
                "SystemStatusOverlay",
                typeof(RectTransform)
            );

        Undo.RegisterCreatedObjectUndo(
            root,
            "Create system status overlay"
        );

        root.transform.SetParent(
            canvas,
            false
        );

        RectTransform rootRt =
            root.GetComponent<RectTransform>();

        StretchFullScreen(rootRt);

        TMP_FontAsset font =
            FindGalmuri11Font();

        TextMeshProUGUI network =
            CreateStatusText(
                root.transform,
                "NetworkStatus",
                "[ARK NETWORK]  CONNECTION : UNSTABLE",
                font,
                new Vector2(-42f, 60f),
                new Vector2(620f, 24f),
                12f,
                StatusWarning
            );

        TextMeshProUGUI rafael =
            CreateStatusText(
                root.transform,
                "RafaelStatus",
                "[RAFAEL NODE]  STATUS : STANDBY",
                font,
                new Vector2(-42f, 36f),
                new Vector2(620f, 24f),
                12f,
                StatusCyan
            );

        TextMeshProUGUI build =
            CreateStatusText(
                root.transform,
                "BuildStatus",
                "BUILD 0.1 // LOCAL NODE",
                font,
                new Vector2(-42f, 14f),
                new Vector2(620f, 20f),
                10f,
                new Color(
                    StatusCyan.r,
                    StatusCyan.g,
                    StatusCyan.b,
                    0.48f
                )
            );

        MainMenuSystemStatusDisplay display =
            Undo.AddComponent
            <MainMenuSystemStatusDisplay>(
                root
            );

        SerializedObject so =
            new SerializedObject(display);

        SetObject(
            so,
            "networkText",
            network
        );

        SetObject(
            so,
            "rafaelText",
            rafael
        );

        SetObject(
            so,
            "buildText",
            build
        );

        so.ApplyModifiedPropertiesWithoutUndo();

        root.transform.SetAsLastSibling();
    }

    private static void SetupMicroGlitch(
        Transform canvas)
    {
        Transform existing =
            FindDirectChild(
                canvas,
                "MicroGlitchOverlay"
            );

        if (existing != null)
            Undo.DestroyObjectImmediate(
                existing.gameObject
            );

        GameObject root =
            new GameObject(
                "MicroGlitchOverlay",
                typeof(RectTransform)
            );

        Undo.RegisterCreatedObjectUndo(
            root,
            "Create micro glitch overlay"
        );

        root.transform.SetParent(
            canvas,
            false
        );

        StretchFullScreen(
            root.GetComponent<RectTransform>()
        );

        Undo.AddComponent
            <MainMenuMicroGlitchEffect>(
                root
            );

        Transform menuGroup =
            FindRecursive(
                canvas,
                "MenuGroup"
            );

        if (menuGroup != null)
        {
            root.transform.SetSiblingIndex(
                Mathf.Max(
                    0,
                    menuGroup.GetSiblingIndex()
                )
            );
        }
    }

    private static void ReduceOldAtmosphereGlitch()
    {
        MainMenuAtmosphereEffect effect =
            Object.FindFirstObjectByType
            <MainMenuAtmosphereEffect>();

        if (effect == null)
            return;

        SerializedObject so =
            new SerializedObject(effect);

        SerializedProperty enableGlitch =
            so.FindProperty(
                "enableGlitch"
            );

        if (enableGlitch != null)
            enableGlitch.boolValue = false;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ReconnectManagerDisplayFields(
        Transform canvas)
    {
        MainMenuSubPanelManager manager =
            Object.FindFirstObjectByType
            <MainMenuSubPanelManager>();

        if (manager == null)
            return;

        SerializedObject so =
            new SerializedObject(manager);

        Button previous =
            FindComponent<Button>(
                canvas,
                "ResolutionPreviousButton"
            );

        Button next =
            FindComponent<Button>(
                canvas,
                "ResolutionNextButton"
            );

        TextMeshProUGUI value =
            FindComponent<TextMeshProUGUI>(
                canvas,
                "ResolutionValue"
            );

        SetObject(
            so,
            "resolutionPreviousButton",
            previous
        );

        SetObject(
            so,
            "resolutionNextButton",
            next
        );

        SetObject(
            so,
            "resolutionValueText",
            value
        );

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TextMeshProUGUI CreateStatusText(
        Transform parent,
        string name,
        string value,
        TMP_FontAsset font,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        Color color)
    {
        GameObject go =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );

        Undo.RegisterCreatedObjectUndo(
            go,
            "Create status text"
        );

        go.transform.SetParent(
            parent,
            false
        );

        RectTransform rt =
            go.GetComponent<RectTransform>();

        rt.anchorMin =
            new Vector2(1f, 0f);

        rt.anchorMax =
            new Vector2(1f, 0f);

        rt.pivot =
            new Vector2(1f, 0f);

        rt.anchoredPosition =
            anchoredPosition;

        rt.sizeDelta = size;

        TextMeshProUGUI text =
            go.GetComponent
            <TextMeshProUGUI>();

        text.text = value;

        if (font != null)
            text.font = font;

        text.fontSize = fontSize;
        text.color = color;

        text.alignment =
            TextAlignmentOptions.BottomRight;

        text.enableWordWrapping = false;
        text.overflowMode =
            TextOverflowModes.Overflow;

        text.raycastTarget = false;

        return text;
    }

    private static TMP_FontAsset FindGalmuri11Font()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "Galmuri11 t:TMP_FontAsset"
            );

        if (guids.Length == 0)
        {
            guids =
                AssetDatabase.FindAssets(
                    "MALGUN t:TMP_FontAsset"
                );
        }

        if (guids.Length == 0)
            return null;

        for (int i = 0;
             i < guids.Length;
             i++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[i]
                );

            TMP_FontAsset asset =
                AssetDatabase.LoadAssetAtPath
                <TMP_FontAsset>(path);

            if (asset != null &&
                asset.name.ToLower()
                    .Contains("galmuri11"))
            {
                return asset;
            }
        }

        string fallbackPath =
            AssetDatabase.GUIDToAssetPath(
                guids[0]
            );

        return AssetDatabase.LoadAssetAtPath
            <TMP_FontAsset>(fallbackPath);
    }

    private static Canvas FindMainMenuCanvas()
    {
        GameObject exact =
            GameObject.Find(
                "Main Menu Canvas"
            );

        if (exact != null)
        {
            Canvas exactCanvas =
                exact.GetComponent<Canvas>();

            if (exactCanvas != null)
                return exactCanvas;
        }

        Canvas[] canvases =
            Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int i = 0;
             i < canvases.Length;
             i++)
        {
            string lower =
                canvases[i].name.ToLower();

            if (lower.Contains("main") &&
                lower.Contains("menu"))
            {
                return canvases[i];
            }
        }

        return canvases.Length > 0
            ? canvases[0]
            : null;
    }

    private static Transform FindRecursive(
        Transform parent,
        string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0;
             i < parent.childCount;
             i++)
        {
            Transform found =
                FindRecursive(
                    parent.GetChild(i),
                    name
                );

            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindDirectChild(
        Transform parent,
        string name)
    {
        for (int i = 0;
             i < parent.childCount;
             i++)
        {
            Transform child =
                parent.GetChild(i);

            if (child.name == name)
                return child;
        }

        return null;
    }

    private static T FindComponent<T>(
        Transform parent,
        string objectName)
        where T : Component
    {
        Transform found =
            FindRecursive(
                parent,
                objectName
            );

        return found != null
            ? found.GetComponent<T>()
            : null;
    }

    private static void StretchFullScreen(
        RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot =
            new Vector2(0.5f, 0.5f);

        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private static void SetObject(
        SerializedObject so,
        string propertyName,
        Object value)
    {
        SerializedProperty property =
            so.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue =
                value;
    }
}
#endif
