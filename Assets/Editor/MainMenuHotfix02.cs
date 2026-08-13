#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MainMenuHotfix02
{
    [MenuItem("Tools/INFECTED AREA/Apply Hotfix 02 - Layout & Missing Scripts")]
    public static void Apply()
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

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "현재 열린 Scene을 확인할 수 없습니다.",
                "확인"
            );
            return;
        }

        Canvas mainMenuCanvas = FindMainMenuCanvas();

        if (mainMenuCanvas == null)
        {
            EditorUtility.DisplayDialog(
                "INFECTED AREA",
                "Main Menu Canvas를 찾지 못했습니다.\nMainMenu 씬을 연 뒤 다시 실행하세요.",
                "확인"
            );
            return;
        }

        Undo.SetCurrentGroupName("INFECTED AREA MainMenu Hotfix 02");
        int undoGroup = Undo.GetCurrentGroup();

        int removedMissing = RemoveAllMissingScripts(scene);

        Transform subPanelRoot = FindRecursive(mainMenuCanvas.transform, "SubPanelRoot");

        if (subPanelRoot != null)
        {
            RemoveSystemTags(subPanelRoot);

            Transform makersPanel = FindRecursive(subPanelRoot, "MakersPanel");
            if (makersPanel != null)
                RepairMakersLayout(makersPanel);

            RepairAllButtons(subPanelRoot);
        }

        Transform menuGroup = FindRecursive(mainMenuCanvas.transform, "MenuGroup");
        if (menuGroup != null)
            RepairAllButtons(menuGroup);

        RepairStatusOverlay(mainMenuCanvas.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog(
            "INFECTED AREA",
            "Hotfix 02 적용 완료!\n\n" +
            "• Missing Script " + removedMissing + "개 제거\n" +
            "• 패널 좌하단 SYS://INFECTED_AREA 텍스트 제거\n" +
            "• MAKERS 이름 영역/간격 정리\n" +
            "• 쉼표 뒤 공백 자동 정리\n" +
            "• 버튼 Hover 컴포넌트/Underline/사운드 연결 점검\n" +
            "• 시스템 상태 표시 위치를 화면 우하단 안쪽으로 정리\n\n" +
            "Ctrl + S로 저장한 뒤 Play로 확인하세요.",
            "확인"
        );
    }

    private static int RemoveAllMissingScripts(Scene scene)
    {
        int total = 0;
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
            total += RemoveMissingRecursive(roots[i].transform);

        return total;
    }

    private static int RemoveMissingRecursive(Transform root)
    {
        int removed = 0;

        int countBefore = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root.gameObject);

        if (countBefore > 0)
        {
            Undo.RegisterCompleteObjectUndo(root.gameObject, "Remove Missing Scripts");
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root.gameObject);
        }

        for (int i = 0; i < root.childCount; i++)
            removed += RemoveMissingRecursive(root.GetChild(i));

        return removed;
    }

    private static void RemoveSystemTags(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);

            if (string.Equals(child.name, "SystemTag", StringComparison.OrdinalIgnoreCase))
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                continue;
            }

            RemoveSystemTags(child);
        }
    }

    private static void RepairMakersLayout(Transform makersPanel)
    {
        TMP_FontAsset galmuri11 = FindFont("Galmuri11");

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

        for (int i = 0; i < rowNames.Length; i++)
        {
            Transform row = FindRecursive(makersPanel, rowNames[i]);
            if (row == null)
                continue;

            TextMeshProUGUI role = FindTMPDirect(row, "Role");
            TextMeshProUGUI dots = FindTMPDirect(row, "Dots");
            TextMeshProUGUI name = FindTMPDirect(row, "Name");

            if (role != null)
            {
                RectTransform rt = role.rectTransform;
                Undo.RecordObject(rt, "Repair makers role layout");
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-255f, 0f);
                rt.sizeDelta = new Vector2(320f, 40f);
                role.textWrappingMode = TextWrappingModes.NoWrap;
                role.overflowMode = TextOverflowModes.Overflow;
                role.raycastTarget = false;
            }

            if (dots != null)
            {
                RectTransform rt = dots.rectTransform;
                Undo.RecordObject(rt, "Repair makers dots layout");
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(5f, 0f);
                rt.sizeDelta = new Vector2(170f, 40f);
                dots.text = "..............";
                dots.textWrappingMode = TextWrappingModes.NoWrap;
                dots.overflowMode = TextOverflowModes.Overflow;
                dots.raycastTarget = false;
            }

            if (name != null)
            {
                Undo.RecordObject(name, "Repair makers name style");

                if (galmuri11 != null)
                    name.font = galmuri11;

                name.fontSize = 19f;
                name.enableAutoSizing = true;
                name.fontSizeMin = 14f;
                name.fontSizeMax = 19f;
                name.color = Color.yellow;
                name.alignment = TextAlignmentOptions.MidlineRight;
                name.textWrappingMode = TextWrappingModes.NoWrap;
                name.overflowMode = TextOverflowModes.Overflow;
                name.raycastTarget = false;

                // "이름,이름,이름" -> "이름, 이름, 이름"
                name.text = Regex.Replace(name.text, @",\s*", ", ");

                RectTransform rt = name.rectTransform;
                Undo.RecordObject(rt, "Repair makers name layout");
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(270f, 0f);
                rt.sizeDelta = new Vector2(330f, 42f);

                EditorUtility.SetDirty(name);
            }
        }
    }

    private static void RepairAllButtons(Transform root)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);

        AudioClip hoverClip = FindAudioClip("Hover");
        AudioClip clickClip = FindAudioClip("Button_Click");

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            GameObject go = button.gameObject;

            // Missing Script가 버튼 오브젝트에 남아 있으면 제거
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0)
            {
                Undo.RegisterCompleteObjectUndo(go, "Remove button missing script");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            }

            TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
                continue;

            Image rootGraphic = go.GetComponent<Image>();
            if (rootGraphic == null)
            {
                rootGraphic = Undo.AddComponent<Image>(go);
                rootGraphic.color = new Color(1f, 1f, 1f, 0f);
            }

            rootGraphic.raycastTarget = true;
            button.interactable = true;

            if (button.targetGraphic == null)
                button.targetGraphic = rootGraphic;

            label.raycastTarget = false;

            Image underline = FindUnderline(go.transform);
            if (underline == null)
                underline = CreateUnderline(go.transform);

            MenuTextButtonHover hover = go.GetComponent<MenuTextButtonHover>();
            if (hover == null)
                hover = Undo.AddComponent<MenuTextButtonHover>(go);

            SerializedObject so = new SerializedObject(hover);
            SetObject(so, "labelText", label);
            SetObject(so, "underlineImage", underline);

            if (hoverClip != null)
                SetObject(so, "hoverSound", hoverClip);

            if (clickClip != null)
                SetObject(so, "clickSound", clickClip);

            SerializedProperty playHover = so.FindProperty("playHoverSound");
            if (playHover != null)
                playHover.boolValue = true;

            SerializedProperty playClick = so.FindProperty("playClickSound");
            if (playClick != null)
                playClick.boolValue = true;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hover);
        }
    }

    private static void RepairStatusOverlay(Transform canvas)
    {
        Transform overlay = FindRecursive(canvas, "SystemStatusOverlay");
        if (overlay == null)
            return;

        TextMeshProUGUI network = FindTMPRecursive(overlay, "NetworkStatus");
        TextMeshProUGUI rafael = FindTMPRecursive(overlay, "RafaelStatus");
        TextMeshProUGUI build = FindTMPRecursive(overlay, "BuildStatus");

        RepairStatusText(network, new Vector2(-28f, 66f), new Vector2(580f, 22f), 11f);
        RepairStatusText(rafael, new Vector2(-28f, 42f), new Vector2(580f, 22f), 11f);
        RepairStatusText(build, new Vector2(-28f, 18f), new Vector2(580f, 20f), 9f);
    }

    private static void RepairStatusText(
        TextMeshProUGUI text,
        Vector2 position,
        Vector2 size,
        float fontSize)
    {
        if (text == null)
            return;

        RectTransform rt = text.rectTransform;
        Undo.RecordObject(rt, "Repair system status layout");
        Undo.RecordObject(text, "Repair system status text");

        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.BottomRight;
        text.raycastTarget = false;
    }

    private static TMP_FontAsset FindFont(string name)
    {
        string[] guids = AssetDatabase.FindAssets(name + " t:TMP_FontAsset");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (font != null && font.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                return font;
        }

        return null;
    }

    private static AudioClip FindAudioClip(string exactName)
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string filename = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(filename, exactName, StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        return null;
    }

    private static Image FindUnderline(Transform button)
    {
        for (int i = 0; i < button.childCount; i++)
        {
            Transform child = button.GetChild(i);
            string normalized = child.name.Replace("_", "").ToLowerInvariant();

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

    private static Canvas FindMainMenuCanvas()
    {
        GameObject exact = GameObject.Find("Main Menu Canvas");

        if (exact != null)
        {
            Canvas c = exact.GetComponent<Canvas>();
            if (c != null)
                return c;
        }

        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        for (int i = 0; i < canvases.Length; i++)
        {
            string lower = canvases[i].name.ToLowerInvariant();

            if (lower.Contains("main") && lower.Contains("menu"))
                return canvases[i];
        }

        return canvases.Length > 0 ? canvases[0] : null;
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

    private static TextMeshProUGUI FindTMPDirect(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private static TextMeshProUGUI FindTMPRecursive(Transform parent, string objectName)
    {
        Transform found = FindRecursive(parent, objectName);
        return found != null ? found.GetComponent<TextMeshProUGUI>() : null;
    }

    private static void SetObject(SerializedObject so, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = so.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }
}
#endif
