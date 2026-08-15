#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MainMenuSubPanelReferenceRepair
{
    [MenuItem("Tools/INFECTED AREA/Repair SubPanel Manager References")]
    public static void Repair()
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

        GameObject canvasObject = GameObject.Find("Main Menu Canvas");
        if (canvasObject == null)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "Main Menu Canvas를 찾지 못했습니다.",
                "확인"
            );
            return;
        }

        Transform canvas = canvasObject.transform;

        Transform menuGroup = FindRecursive(canvas, "MenuGroup");
        Transform subPanelRoot = FindRecursive(canvas, "SubPanelRoot");

        Transform settingsPanel = FindRecursive(canvas, "SettingsPanel");
        Transform makersPanel = FindRecursive(canvas, "MakersPanel");
        Transform quitPanel = FindRecursive(canvas, "QuitPanel");
        Transform backgroundDim = FindRecursive(canvas, "BackgroundDim");

        Button settingButton = FindButton(canvas, "SettingButton");
        Button makersButton = FindButton(canvas, "MakersButton");
        Button quitButton = FindButton(canvas, "QuitButton");

        if (menuGroup == null ||
            subPanelRoot == null ||
            settingsPanel == null ||
            makersPanel == null ||
            quitPanel == null ||
            settingButton == null ||
            makersButton == null ||
            quitButton == null)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "필수 오브젝트를 찾지 못했습니다.\n\n" +
                "필요: MenuGroup / SubPanelRoot / SettingsPanel / MakersPanel / QuitPanel / SettingButton / MakersButton / QuitButton",
                "확인"
            );
            return;
        }

        Undo.SetCurrentGroupName("Repair Main Menu SubPanel Manager");
        int undoGroup = Undo.GetCurrentGroup();

        CanvasGroup menuGroupCg = EnsureComponent<CanvasGroup>(menuGroup.gameObject);
        CanvasGroup settingsCg = EnsureComponent<CanvasGroup>(settingsPanel.gameObject);
        CanvasGroup makersCg = EnsureComponent<CanvasGroup>(makersPanel.gameObject);
        CanvasGroup quitCg = EnsureComponent<CanvasGroup>(quitPanel.gameObject);
        CanvasGroup dimCg = backgroundDim != null
            ? EnsureComponent<CanvasGroup>(backgroundDim.gameObject)
            : null;

        // Manager object 찾기. 없으면 생성.
        GameObject managerObject = GameObject.Find("MenuSubPanelManager");

        if (managerObject == null)
        {
            managerObject = new GameObject("MenuSubPanelManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Create MenuSubPanelManager");
            managerObject.transform.SetParent(canvas, false);
        }

        // 이전 스크립트 삭제/재생성 때문에 Missing Script가 남았다면 제거.
        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(managerObject) > 0)
        {
            Undo.RegisterCompleteObjectUndo(managerObject, "Remove missing manager script");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(managerObject);
        }

        MainMenuSubPanelManager manager =
            managerObject.GetComponent<MainMenuSubPanelManager>();

        if (manager == null)
            manager = Undo.AddComponent<MainMenuSubPanelManager>(managerObject);

        SerializedObject so = new SerializedObject(manager);

        SetObject(so, "menuGroupCanvasGroup", menuGroupCg);
        SetObject(so, "settingButton", settingButton);
        SetObject(so, "makersButton", makersButton);
        SetObject(so, "quitButton", quitButton);

        SetObject(so, "backgroundDimCanvasGroup", dimCg);

        SetObject(so, "settingsPanelCanvasGroup", settingsCg);
        SetObject(so, "makersPanelCanvasGroup", makersCg);
        SetObject(so, "quitPanelCanvasGroup", quitCg);

        SetObject(so, "settingsBackButton", FindButton(settingsPanel, "BackButton"));
        SetObject(so, "makersBackButton", FindButton(makersPanel, "BackButton"));
        SetObject(so, "quitConfirmButton", FindButton(quitPanel, "ConfirmButton"));
        SetObject(so, "quitCancelButton", FindButton(quitPanel, "CancelButton"));
        SetObject(so, "resetSettingsButton", FindButton(settingsPanel, "ResetButton"));

        SetObject(so, "masterVolumeSlider", FindComponent<Slider>(settingsPanel, "MASTERVOLUMESlider", "MasterSlider", "MasterVolumeSlider"));
        SetObject(so, "bgmVolumeSlider", FindComponent<Slider>(settingsPanel, "BGMVOLUMESlider", "BgmSlider", "BGMVolumeSlider"));
        SetObject(so, "sfxVolumeSlider", FindComponent<Slider>(settingsPanel, "SFXVOLUMESlider", "SfxSlider", "SFXVolumeSlider"));

        SetObject(so, "masterVolumeValueText", FindComponent<TextMeshProUGUI>(settingsPanel, "MASTERVOLUMEValue", "MasterValue", "MasterVolumeValue"));
        SetObject(so, "bgmVolumeValueText", FindComponent<TextMeshProUGUI>(settingsPanel, "BGMVOLUMEValue", "BgmValue", "BGMVolumeValue"));
        SetObject(so, "sfxVolumeValueText", FindComponent<TextMeshProUGUI>(settingsPanel, "SFXVOLUMEValue", "SfxValue", "SFXVolumeValue"));

        SetObject(so, "fullscreenToggle", FindComponent<Toggle>(settingsPanel, "FullscreenToggle"));

        SetObject(so, "resolutionPreviousButton",
            FindButton(settingsPanel, "ResolutionPreviousButton"));

        SetObject(so, "resolutionNextButton",
            FindButton(settingsPanel, "ResolutionNextButton"));

        SetObject(so, "resolutionValueText",
            FindComponent<TextMeshProUGUI>(settingsPanel, "ResolutionValue"));

        AudioSource bgmSource = null;
        GameObject bgmObject = GameObject.Find("BGMManager");
        if (bgmObject != null)
            bgmSource = bgmObject.GetComponent<AudioSource>();

        SetObject(so, "menuBgmAudioSource", bgmSource);

        // Makers rows
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

        SerializedProperty rows = so.FindProperty("makerRows");
        if (rows != null)
        {
            rows.arraySize = rowNames.Length;

            for (int i = 0; i < rowNames.Length; i++)
            {
                Transform row = FindRecursive(makersPanel, rowNames[i]);
                CanvasGroup cg = row != null
                    ? EnsureComponent<CanvasGroup>(row.gameObject)
                    : null;

                rows.GetArrayElementAtIndex(i).objectReferenceValue = cg;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);

        // 패널 Animator들이 사라지지 않았는지도 확인.
        EnsurePanelAnimator(settingsPanel);
        EnsurePanelAnimator(makersPanel);
        EnsurePanelAnimator(quitPanel);

        EditorSceneManager.MarkSceneDirty(canvasObject.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Selection.activeGameObject = managerObject;

        EditorUtility.DisplayDialog(
            "INFECTED AREA",
            "SubPanel Manager 복구 완료!\n\n" +
            "SETTING / MAKERS / QUIT 버튼과 패널 레퍼런스를 다시 연결했습니다.\n" +
            "Missing Manager Script가 있었다면 제거하고 새 Manager를 다시 부착했습니다.\n\n" +
            "Ctrl + S 저장 후 Play로 테스트하세요.",
            "확인"
        );
    }

    private static void EnsurePanelAnimator(Transform panel)
    {
        if (panel == null)
            return;

        MainMenuPanelConstructAnimator animator =
            panel.GetComponent<MainMenuPanelConstructAnimator>();

        // 이미 있으면 그대로 둠. 없으면 단순히 경고만 남김.
        if (animator == null)
        {
            Debug.LogWarning(
                "[INFECTED AREA] " + panel.name +
                "에 MainMenuPanelConstructAnimator가 없습니다. " +
                "Visual FX Patch를 다시 적용하면 생성 애니메이션도 복구됩니다."
            );
        }
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();

        if (component == null)
            component = Undo.AddComponent<T>(go);

        return component;
    }

    private static Button FindButton(Transform root, string name)
    {
        Transform found = FindRecursive(root, name);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private static T FindComponent<T>(Transform root, params string[] names)
        where T : Component
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindRecursive(root, names[i]);

            if (found != null)
            {
                T component = found.GetComponent<T>();

                if (component != null)
                    return component;
            }
        }

        return null;
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
