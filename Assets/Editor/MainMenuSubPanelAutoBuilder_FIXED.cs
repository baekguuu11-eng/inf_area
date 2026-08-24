#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuSubPanelAutoBuilder
{
    private static readonly Color PanelColor = new Color(0.015f, 0.035f, 0.085f, 0.97f);
    private static readonly Color Cyan = new Color(0.55f, 0.95f, 1f, 1f);
    private static readonly Color SoftCyan = new Color(0.70f, 0.95f, 1f, 1f);
    private static readonly Color DimBlack = new Color(0f, 0f, 0f, 1f);
    private static readonly Color TrackColor = new Color(0.05f, 0.12f, 0.18f, 1f);
    private static readonly Color FillColor = new Color(0.30f, 0.90f, 1f, 1f);

    [MenuItem("Tools/INFECTED AREA/Rebuild Main Menu Subpanels")]
    public static void Rebuild()
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

        GameObject canvasObject = FindMainMenuCanvas();

        if (canvasObject == null)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "'Main Menu Canvas'를 찾지 못했습니다.",
                "확인"
            );
            return;
        }

        Transform canvas = canvasObject.transform;
        Transform menuGroup = FindRecursive(canvas, "MenuGroup");

        if (menuGroup == null)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "MenuGroup을 찾지 못했습니다.",
                "확인"
            );
            return;
        }

        Button startButton = FindButton(menuGroup, "StartButton");
        Button settingButton = FindButton(menuGroup, "SettingButton");

        Button makersButton = FindButton(menuGroup, "MakersButton");
        if (makersButton == null)
            makersButton = FindButton(menuGroup, "InfoButton");

        Button quitButton = FindButton(menuGroup, "QuitButton");
        if (quitButton == null)
            quitButton = FindButton(menuGroup, "CreditButton");

        if (settingButton == null || makersButton == null || quitButton == null)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "SettingButton / Info(Makers)Button / Credit(Quit)Button 중 일부를 찾지 못했습니다.",
                "확인"
            );
            return;
        }

        Undo.SetCurrentGroupName("Rebuild INFECTED AREA Main Menu Panels");
        int undoGroup = Undo.GetCurrentGroup();

        RenameMenuButton(makersButton.gameObject, "MakersButton", "MAKERS", "Text (TMP)_Makers");
        RenameMenuButton(quitButton.gameObject, "QuitButton", "QUIT", "Text (TMP)_Quit");

        CanvasGroup menuCanvasGroup = EnsureComponent<CanvasGroup>(menuGroup.gameObject);

        TextMeshProUGUI menuLabel = settingButton.GetComponentInChildren<TextMeshProUGUI>(true);
        TMP_FontAsset pixelFont = menuLabel != null ? menuLabel.font : null;
        TMP_FontAsset koreanFont = FindKoreanFont() ?? pixelFont;
        MenuTextButtonHover sourceHover = settingButton.GetComponent<MenuTextButtonHover>();

        DeleteGeneratedObject(canvas, "SubPanelRoot");
        DeleteGeneratedObject(canvas, "MenuSubPanelManager");

        GameObject root = CreateUIObject("SubPanelRoot", canvas);
        StretchFullScreen(root.GetComponent<RectTransform>());
        root.transform.SetSiblingIndex(Mathf.Min(menuGroup.GetSiblingIndex() + 1, canvas.childCount - 1));

        CanvasGroup dim = CreateDim(root.transform);

        SettingsBuildResult settings = BuildSettingsPanel(
            root.transform,
            pixelFont,
            sourceHover
        );

        MakersBuildResult makers = BuildMakersPanel(
            root.transform,
            pixelFont,
            koreanFont,
            sourceHover
        );

        QuitBuildResult quit = BuildQuitPanel(
            root.transform,
            pixelFont,
            sourceHover
        );

        GameObject managerObject = new GameObject("MenuSubPanelManager", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(managerObject, "Create MenuSubPanelManager");
        managerObject.transform.SetParent(canvas, false);

        RectTransform managerRect = managerObject.GetComponent<RectTransform>();
        managerRect.anchorMin = new Vector2(0.5f, 0.5f);
        managerRect.anchorMax = new Vector2(0.5f, 0.5f);
        managerRect.sizeDelta = Vector2.zero;

        MainMenuSubPanelManager manager = Undo.AddComponent<MainMenuSubPanelManager>(managerObject);

        AudioSource menuBgm = FindMenuBgmAudioSource(canvas);

        SerializedObject so = new SerializedObject(manager);

        SetObject(so, "menuGroupCanvasGroup", menuCanvasGroup);
        SetObject(so, "settingButton", settingButton);
        SetObject(so, "makersButton", makersButton);
        SetObject(so, "quitButton", quitButton);

        SetObject(so, "backgroundDimCanvasGroup", dim);

        SetObject(so, "settingsPanelCanvasGroup", settings.canvasGroup);
        SetObject(so, "makersPanelCanvasGroup", makers.canvasGroup);
        SetObject(so, "quitPanelCanvasGroup", quit.canvasGroup);

        SetObject(so, "settingsBackButton", settings.backButton);
        SetObject(so, "makersBackButton", makers.backButton);
        SetObject(so, "quitConfirmButton", quit.confirmButton);
        SetObject(so, "quitCancelButton", quit.cancelButton);
        SetObject(so, "resetSettingsButton", settings.resetButton);

        SetObjectArray(so, "makerRows", makers.rows);

        SetObject(so, "masterVolumeSlider", settings.masterSlider);
        SetObject(so, "bgmVolumeSlider", settings.bgmSlider);
        SetObject(so, "sfxVolumeSlider", settings.sfxSlider);

        SetObject(so, "masterVolumeValueText", settings.masterValue);
        SetObject(so, "bgmVolumeValueText", settings.bgmValue);
        SetObject(so, "sfxVolumeValueText", settings.sfxValue);
        SetObject(so, "menuBgmAudioSource", menuBgm);

        SetObject(so, "fullscreenToggle", settings.fullscreenToggle);
        SetObject(so, "resolutionPreviousButton", settings.resolutionPrevious);
        SetObject(so, "resolutionNextButton", settings.resolutionNext);
        SetObject(so, "resolutionValueText", settings.resolutionValue);

        so.ApplyModifiedPropertiesWithoutUndo();

        ConfigureIntroAnimator(
            menuGroup,
            menuCanvasGroup,
            startButton,
            settingButton,
            makersButton,
            quitButton
        );

        settings.canvasGroup.gameObject.SetActive(false);
        makers.canvasGroup.gameObject.SetActive(false);
        quit.canvasGroup.gameObject.SetActive(false);
        dim.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(canvasObject.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Selection.activeGameObject = root;

        EditorUtility.DisplayDialog(
            "INFECTED AREA",
            "완료!\n\nSETTING / MAKERS / QUIT 패널과 내부 UI를 자동 생성했고 연결까지 끝냈습니다.\n\n이제 Play로 테스트하세요.",
            "확인"
        );
    }

    [MenuItem("Tools/INFECTED AREA/Remove Generated Main Menu Subpanels")]
    public static void RemoveGenerated()
    {
        if (Application.isPlaying)
            return;

        GameObject canvasObject = FindMainMenuCanvas();
        if (canvasObject == null)
            return;

        Transform canvas = canvasObject.transform;

        DeleteGeneratedObject(canvas, "SubPanelRoot");
        DeleteGeneratedObject(canvas, "MenuSubPanelManager");

        EditorSceneManager.MarkSceneDirty(canvasObject.scene);
    }

    private static SettingsBuildResult BuildSettingsPanel(
        Transform parent,
        TMP_FontAsset font,
        MenuTextButtonHover sourceHover)
    {
        CanvasGroup panel = CreatePanel(
            parent,
            "SettingsPanel",
            new Vector2(880f, 650f)
        );

        CreateText(
            panel.transform,
            "Title",
            "[ SETTINGS ]",
            font,
            34f,
            new Vector2(0f, 265f),
            new Vector2(760f, 55f),
            TextAlignmentOptions.Center,
            Cyan
        );

        CreateText(
            panel.transform,
            "Subtitle",
            "SYSTEM CONFIGURATION",
            font,
            14f,
            new Vector2(0f, 222f),
            new Vector2(700f, 35f),
            TextAlignmentOptions.Center,
            new Color(0.55f, 0.75f, 0.82f, 1f)
        );

        CreateSeparator(panel.transform, new Vector2(0f, 195f), 740f);

        SettingsBuildResult result = new SettingsBuildResult();
        result.canvasGroup = panel;

        result.masterSlider = CreateSettingsSlider(
            panel.transform,
            font,
            "MASTER VOLUME",
            125f,
            1f,
            out result.masterValue
        );

        result.bgmSlider = CreateSettingsSlider(
            panel.transform,
            font,
            "BGM VOLUME",
            55f,
            0.7f,
            out result.bgmValue
        );

        result.sfxSlider = CreateSettingsSlider(
            panel.transform,
            font,
            "SFX VOLUME",
            -15f,
            0.8f,
            out result.sfxValue
        );

        CreateText(
            panel.transform,
            "FullscreenLabel",
            "FULLSCREEN",
            font,
            18f,
            new Vector2(-270f, -92f),
            new Vector2(300f, 40f),
            TextAlignmentOptions.MidlineLeft,
            SoftCyan
        );

        result.fullscreenToggle = CreateCyberToggle(
            panel.transform,
            new Vector2(120f, -92f)
        );

        CreateText(
            panel.transform,
            "ResolutionLabel",
            "RESOLUTION",
            font,
            18f,
            new Vector2(-270f, -157f),
            new Vector2(300f, 40f),
            TextAlignmentOptions.MidlineLeft,
            SoftCyan
        );

        result.resolutionPrevious = CreateCyberButton(
            panel.transform,
            "ResolutionPreviousButton",
            "<",
            font,
            sourceHover,
            new Vector2(-10f, -157f),
            new Vector2(70f, 44f),
            18f
        );

        result.resolutionValue = CreateText(
            panel.transform,
            "ResolutionValue",
            "1920 x 1080",
            font,
            16f,
            new Vector2(120f, -157f),
            new Vector2(230f, 45f),
            TextAlignmentOptions.Center,
            Color.white
        );

        result.resolutionNext = CreateCyberButton(
            panel.transform,
            "ResolutionNextButton",
            ">",
            font,
            sourceHover,
            new Vector2(250f, -157f),
            new Vector2(70f, 44f),
            18f
        );

        CreateSeparator(panel.transform, new Vector2(0f, -212f), 740f);

        result.resetButton = CreateCyberButton(
            panel.transform,
            "ResetButton",
            "RESET",
            font,
            sourceHover,
            new Vector2(-125f, -270f),
            new Vector2(210f, 55f),
            18f
        );

        result.backButton = CreateCyberButton(
            panel.transform,
            "BackButton",
            "BACK",
            font,
            sourceHover,
            new Vector2(125f, -270f),
            new Vector2(210f, 55f),
            18f
        );

        return result;
    }

    private static MakersBuildResult BuildMakersPanel(
        Transform parent,
        TMP_FontAsset pixelFont,
        TMP_FontAsset nameFont,
        MenuTextButtonHover sourceHover)
    {
        CanvasGroup panel = CreatePanel(
            parent,
            "MakersPanel",
            new Vector2(960f, 690f)
        );

        CreateText(
            panel.transform,
            "Title",
            "[ INFECTED AREA ]",
            pixelFont,
            32f,
            new Vector2(0f, 285f),
            new Vector2(800f, 55f),
            TextAlignmentOptions.Center,
            Cyan
        );

        CreateText(
            panel.transform,
            "Subtitle",
            "DEVELOPMENT TEAM",
            pixelFont,
            15f,
            new Vector2(0f, 242f),
            new Vector2(760f, 35f),
            TextAlignmentOptions.Center,
            new Color(0.55f, 0.75f, 0.82f, 1f)
        );

        CreateSeparator(panel.transform, new Vector2(0f, 214f), 780f);

        string[] rowNames =
        {
            "TeamLeaderRow",
            "GameDesignRow",
            "ProgrammingRow",
            "VisualDesignRow",
            "PixelArtRow",
            "AnimationRow",
            "SoundDesignRow",
            "SpecialThanksRow"
        };

        string[] labels =
        {
            "TEAM LEADER",
            "GAME DESIGN",
            "PROGRAMMING",
            "VISUAL DESIGN",
            "PIXEL ART",
            "ANIMATION",
            "SOUND DESIGN",
            "SPECIAL THANKS"
        };

        CanvasGroup[] rows = new CanvasGroup[rowNames.Length];

        float startY = 155f;
        float step = 48f;

        for (int i = 0; i < rowNames.Length; i++)
        {
            GameObject row = CreateUIObject(rowNames[i], panel.transform);
            RectTransform rt = row.GetComponent<RectTransform>();
            SetCenteredRect(
                rt,
                new Vector2(0f, startY - i * step),
                new Vector2(800f, 42f)
            );

            CanvasGroup cg = Undo.AddComponent<CanvasGroup>(row);
            cg.alpha = 1f;
            rows[i] = cg;

            CreateText(
                row.transform,
                "Role",
                labels[i],
                pixelFont,
                16f,
                new Vector2(-245f, 0f),
                new Vector2(300f, 40f),
                TextAlignmentOptions.MidlineLeft,
                SoftCyan
            );

            CreateText(
                row.transform,
                "Dots",
                "................",
                pixelFont,
                14f,
                new Vector2(25f, 0f),
                new Vector2(260f, 40f),
                TextAlignmentOptions.Center,
                new Color(0.25f, 0.62f, 0.68f, 1f)
            );

            CreateText(
                row.transform,
                "Name",
                "NAME",
                nameFont,
                19f,
                new Vector2(275f, 0f),
                new Vector2(250f, 40f),
                TextAlignmentOptions.MidlineRight,
                Color.white
            );
        }

        Button back = CreateCyberButton(
            panel.transform,
            "BackButton",
            "BACK",
            pixelFont,
            sourceHover,
            new Vector2(0f, -286f),
            new Vector2(220f, 55f),
            18f
        );

        MakersBuildResult result = new MakersBuildResult();
        result.canvasGroup = panel;
        result.rows = rows;
        result.backButton = back;
        return result;
    }

    private static QuitBuildResult BuildQuitPanel(
        Transform parent,
        TMP_FontAsset font,
        MenuTextButtonHover sourceHover)
    {
        CanvasGroup panel = CreatePanel(
            parent,
            "QuitPanel",
            new Vector2(700f, 330f)
        );

        CreateText(
            panel.transform,
            "Title",
            "[ SYSTEM TERMINATION ]",
            font,
            26f,
            new Vector2(0f, 100f),
            new Vector2(620f, 50f),
            TextAlignmentOptions.Center,
            new Color(1f, 0.34f, 0.34f, 1f)
        );

        CreateText(
            panel.transform,
            "Message",
            "TERMINATE PROGRAM?",
            font,
            18f,
            new Vector2(0f, 25f),
            new Vector2(580f, 60f),
            TextAlignmentOptions.Center,
            Color.white
        );

        Button confirm = CreateCyberButton(
            panel.transform,
            "ConfirmButton",
            "CONFIRM",
            font,
            sourceHover,
            new Vector2(-130f, -90f),
            new Vector2(220f, 55f),
            17f
        );

        Button cancel = CreateCyberButton(
            panel.transform,
            "CancelButton",
            "CANCEL",
            font,
            sourceHover,
            new Vector2(130f, -90f),
            new Vector2(220f, 55f),
            17f
        );

        QuitBuildResult result = new QuitBuildResult();
        result.canvasGroup = panel;
        result.confirmButton = confirm;
        result.cancelButton = cancel;
        return result;
    }

    private static Slider CreateSettingsSlider(
        Transform parent,
        TMP_FontAsset font,
        string label,
        float y,
        float initialValue,
        out TextMeshProUGUI valueText)
    {
        CreateText(
            parent,
            label.Replace(" ", "") + "Label",
            label,
            font,
            18f,
            new Vector2(-270f, y),
            new Vector2(300f, 40f),
            TextAlignmentOptions.MidlineLeft,
            SoftCyan
        );

        Slider slider = CreateCyberSlider(
            parent,
            label.Replace(" ", "") + "Slider",
            new Vector2(105f, y),
            new Vector2(330f, 34f)
        );

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = initialValue;
        slider.wholeNumbers = false;

        valueText = CreateText(
            parent,
            label.Replace(" ", "") + "Value",
            Mathf.RoundToInt(initialValue * 100f) + "%",
            font,
            16f,
            new Vector2(315f, y),
            new Vector2(100f, 40f),
            TextAlignmentOptions.Center,
            Color.white
        );

        return slider;
    }

    private static CanvasGroup CreatePanel(
        Transform parent,
        string name,
        Vector2 size)
    {
        GameObject panelObject = CreateUIObject(name, parent);
        RectTransform rt = panelObject.GetComponent<RectTransform>();
        SetCenteredRect(rt, Vector2.zero, size);

        Image image = Undo.AddComponent<Image>(panelObject);
        image.color = PanelColor;
        image.raycastTarget = true;

        Outline outline = Undo.AddComponent<Outline>(panelObject);
        outline.effectColor = new Color(0.25f, 0.85f, 0.95f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);

        CanvasGroup cg = Undo.AddComponent<CanvasGroup>(panelObject);
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        CreateCornerText(panelObject.transform, "SYS://INFECTED_AREA", new Vector2(-330f, -280f));
        return cg;
    }

    private static CanvasGroup CreateDim(Transform parent)
    {
        GameObject dimObject = CreateUIObject("BackgroundDim", parent);
        StretchFullScreen(dimObject.GetComponent<RectTransform>());

        Image image = Undo.AddComponent<Image>(dimObject);
        image.color = DimBlack;
        image.raycastTarget = true;

        CanvasGroup cg = Undo.AddComponent<CanvasGroup>(dimObject);
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        return cg;
    }

    private static Slider CreateCyberSlider(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size)
    {
        GameObject root = CreateUIObject(name, parent);
        RectTransform rootRt = root.GetComponent<RectTransform>();
        SetCenteredRect(rootRt, position, size);

        Slider slider = Undo.AddComponent<Slider>(root);
        slider.direction = Slider.Direction.LeftToRight;

        GameObject background = CreateUIObject("Background", root.transform);
        RectTransform bgRt = background.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0.5f);
        bgRt.anchorMax = new Vector2(1f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.offsetMin = new Vector2(0f, -5f);
        bgRt.offsetMax = new Vector2(0f, 5f);

        Image bgImage = Undo.AddComponent<Image>(background);
        bgImage.color = TrackColor;

        GameObject fillArea = CreateUIObject("Fill Area", root.transform);
        RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0f);
        fillAreaRt.anchorMax = new Vector2(1f, 1f);
        fillAreaRt.offsetMin = new Vector2(4f, 9f);
        fillAreaRt.offsetMax = new Vector2(-4f, -9f);

        GameObject fill = CreateUIObject("Fill", fillArea.transform);
        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        Image fillImage = Undo.AddComponent<Image>(fill);
        fillImage.color = FillColor;

        GameObject handleArea = CreateUIObject("Handle Slide Area", root.transform);
        RectTransform handleAreaRt = handleArea.GetComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10f, 0f);
        handleAreaRt.offsetMax = new Vector2(-10f, 0f);

        GameObject handle = CreateUIObject("Handle", handleArea.transform);
        RectTransform handleRt = handle.GetComponent<RectTransform>();
        handleRt.anchorMin = new Vector2(0.5f, 0.5f);
        handleRt.anchorMax = new Vector2(0.5f, 0.5f);
        handleRt.sizeDelta = new Vector2(16f, 28f);

        Image handleImage = Undo.AddComponent<Image>(handle);
        handleImage.color = Color.white;

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImage;

        return slider;
    }

    private static Toggle CreateCyberToggle(
        Transform parent,
        Vector2 position)
    {
        GameObject root = CreateUIObject("FullscreenToggle", parent);
        RectTransform rt = root.GetComponent<RectTransform>();
        SetCenteredRect(rt, position, new Vector2(70f, 42f));

        Toggle toggle = Undo.AddComponent<Toggle>(root);

        GameObject bg = CreateUIObject("Background", root.transform);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        SetCenteredRect(bgRt, Vector2.zero, new Vector2(38f, 38f));

        Image bgImage = Undo.AddComponent<Image>(bg);
        bgImage.color = TrackColor;

        Outline outline = Undo.AddComponent<Outline>(bg);
        outline.effectColor = new Color(0.3f, 0.85f, 0.95f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject check = CreateUIObject("Checkmark", bg.transform);
        RectTransform checkRt = check.GetComponent<RectTransform>();
        SetCenteredRect(checkRt, Vector2.zero, new Vector2(24f, 24f));

        Image checkImage = Undo.AddComponent<Image>(check);
        checkImage.color = FillColor;

        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.isOn = true;

        return toggle;
    }

    private static Button CreateCyberButton(
        Transform parent,
        string name,
        string label,
        TMP_FontAsset font,
        MenuTextButtonHover sourceHover,
        Vector2 position,
        Vector2 size,
        float fontSize)
    {
        GameObject root = CreateUIObject(name, parent);
        RectTransform rt = root.GetComponent<RectTransform>();
        SetCenteredRect(rt, position, size);

        Image image = Undo.AddComponent<Image>(root);
        image.color = new Color(0.02f, 0.08f, 0.12f, 0.55f);

        Button button = Undo.AddComponent<Button>(root);
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.75f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.65f, 0.95f, 1f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        TextMeshProUGUI text = CreateText(
            root.transform,
            "Text (TMP)_" + name,
            label,
            font,
            fontSize,
            Vector2.zero,
            size,
            TextAlignmentOptions.Center,
            SoftCyan
        );

        GameObject underlineObject = CreateUIObject("Underline", root.transform);
        RectTransform underlineRt = underlineObject.GetComponent<RectTransform>();
        underlineRt.anchorMin = new Vector2(0.5f, 0f);
        underlineRt.anchorMax = new Vector2(0.5f, 0f);
        underlineRt.pivot = new Vector2(0.5f, 0.5f);
        underlineRt.anchoredPosition = new Vector2(0f, 5f);
        underlineRt.sizeDelta = new Vector2(0f, 2f);

        Image underline = Undo.AddComponent<Image>(underlineObject);
        underline.color = Cyan;
        underline.raycastTarget = false;

        MenuTextButtonHover hover = Undo.AddComponent<MenuTextButtonHover>(root);

        if (sourceHover != null)
            EditorUtility.CopySerialized(sourceHover, hover);

        SerializedObject hoverSo = new SerializedObject(hover);
        SetObject(hoverSo, "labelText", text);
        SetObject(hoverSo, "underlineImage", underline);
        hoverSo.ApplyModifiedPropertiesWithoutUndo();

        return button;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string textValue,
        TMP_FontAsset font,
        float fontSize,
        Vector2 position,
        Vector2 size,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        RectTransform rt = go.GetComponent<RectTransform>();
        SetCenteredRect(rt, position, size);

        TextMeshProUGUI text = Undo.AddComponent<TextMeshProUGUI>(go);
        text.text = textValue;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        return text;
    }

    private static void CreateSeparator(
        Transform parent,
        Vector2 position,
        float width)
    {
        GameObject line = CreateUIObject("Separator", parent);
        RectTransform rt = line.GetComponent<RectTransform>();
        SetCenteredRect(rt, position, new Vector2(width, 2f));

        Image image = Undo.AddComponent<Image>(line);
        image.color = new Color(0.25f, 0.8f, 0.9f, 0.40f);
        image.raycastTarget = false;
    }

    private static void CreateCornerText(
        Transform parent,
        string value,
        Vector2 position)
    {
        TMP_FontAsset font = null;

        TextMeshProUGUI existing = parent.GetComponentInChildren<TextMeshProUGUI>(true);
        if (existing != null)
            font = existing.font;

        CreateText(
            parent,
            "SystemTag",
            value,
            font,
            10f,
            position,
            new Vector2(260f, 25f),
            TextAlignmentOptions.MidlineLeft,
            new Color(0.35f, 0.65f, 0.70f, 0.7f)
        );
    }

    private static void RenameMenuButton(
        GameObject button,
        string newName,
        string newText,
        string newTextName)
    {
        Undo.RecordObject(button, "Rename menu button");
        button.name = newName;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
        {
            Undo.RecordObject(label, "Update menu label");
            label.text = newText;
            label.gameObject.name = newTextName;
        }
    }

    private static void ConfigureIntroAnimator(
        Transform menuGroup,
        CanvasGroup menuCanvasGroup,
        Button start,
        Button setting,
        Button makers,
        Button quit)
    {
        MainMenuIntroAnimator intro = menuGroup.GetComponent<MainMenuIntroAnimator>();

        if (intro == null)
            intro = Object.FindObjectOfType<MainMenuIntroAnimator>();

        if (intro == null)
            return;

        SerializedObject so = new SerializedObject(intro);

        SetObject(so, "menuGroupCanvasGroup", menuCanvasGroup);

        SerializedProperty menuItems = so.FindProperty("menuItems");

        if (menuItems != null)
        {
            menuItems.arraySize = 4;

            menuItems.GetArrayElementAtIndex(0).objectReferenceValue =
                start != null ? start.GetComponent<RectTransform>() : null;

            menuItems.GetArrayElementAtIndex(1).objectReferenceValue =
                setting != null ? setting.GetComponent<RectTransform>() : null;

            menuItems.GetArrayElementAtIndex(2).objectReferenceValue =
                makers != null ? makers.GetComponent<RectTransform>() : null;

            menuItems.GetArrayElementAtIndex(3).objectReferenceValue =
                quit != null ? quit.GetComponent<RectTransform>() : null;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AudioSource FindMenuBgmAudioSource(Transform canvas)
    {
        GameObject bgm = GameObject.Find("BGMManager");

        if (bgm != null)
        {
            AudioSource source = bgm.GetComponent<AudioSource>();
            if (source != null)
                return source;
        }

        AudioSource[] sources = Object.FindObjectsOfType<AudioSource>();

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i].gameObject.name.ToLower().Contains("bgm"))
                return sources[i];
        }

        return null;
    }

    private static TMP_FontAsset FindKoreanFont()
    {
        string[] guids = AssetDatabase.FindAssets("Korean t:TMP_FontAsset");

        if (guids.Length == 0)
            guids = AssetDatabase.FindAssets("Dynamic t:TMP_FontAsset");

        if (guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }

    private static GameObject FindMainMenuCanvas()
    {
        GameObject exact = GameObject.Find("Main Menu Canvas");
        if (exact != null && exact.GetComponent<Canvas>() != null)
            return exact;

        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].name.ToLower().Contains("main") &&
                canvases[i].name.ToLower().Contains("menu"))
            {
                return canvases[i].gameObject;
            }
        }

        return canvases.Length > 0 ? canvases[0].gameObject : null;
    }

    private static Button FindButton(Transform parent, string name)
    {
        Transform found = FindRecursive(parent, name);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindRecursive(parent.GetChild(i), name);
            if (result != null)
                return result;
        }

        return null;
    }

    private static void DeleteGeneratedObject(Transform parent, string name)
    {
        Transform target = FindDirectChild(parent, name);

        if (target != null)
            Undo.DestroyObjectImmediate(target.gameObject);
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == name)
                return child;
        }

        return null;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();

        if (component == null)
            component = Undo.AddComponent<T>(go);

        return component;
    }

    private static void SetCenteredRect(
        RectTransform rt,
        Vector2 position,
        Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    private static void StretchFullScreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
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

    private static void SetObjectArray(
        SerializedObject so,
        string propertyName,
        Object[] values)
    {
        SerializedProperty property = so.FindProperty(propertyName);

        if (property == null)
            return;

        property.arraySize = values != null ? values.Length : 0;

        if (values == null)
            return;

        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private sealed class SettingsBuildResult
    {
        public CanvasGroup canvasGroup;
        public Slider masterSlider;
        public Slider bgmSlider;
        public Slider sfxSlider;
        public TextMeshProUGUI masterValue;
        public TextMeshProUGUI bgmValue;
        public TextMeshProUGUI sfxValue;
        public Toggle fullscreenToggle;
        public Button resolutionPrevious;
        public Button resolutionNext;
        public TextMeshProUGUI resolutionValue;
        public Button resetButton;
        public Button backButton;
    }

    private sealed class MakersBuildResult
    {
        public CanvasGroup canvasGroup;
        public CanvasGroup[] rows;
        public Button backButton;
    }

    private sealed class QuitBuildResult
    {
        public CanvasGroup canvasGroup;
        public Button confirmButton;
        public Button cancelButton;
    }
}
#endif
