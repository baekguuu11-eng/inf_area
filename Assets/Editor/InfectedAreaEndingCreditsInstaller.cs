using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

public static class InfectedAreaEndingCreditsInstaller
{
    [MenuItem("Tools/Infected Area/Install Final Ending Credits v8")]
    public static void Install()
    {
        CreateFolders();
        CreateOrUpdateV8Text();

        GameObject system = GameObject.Find("EndingCredits_AutoSystem");

        if (system == null)
        {
            system = new GameObject("EndingCredits_AutoSystem");
        }

        InfectedAreaEndingCreditsAuto ending = system.GetComponent<InfectedAreaEndingCreditsAuto>();

        if (ending == null)
        {
            ending = system.AddComponent<InfectedAreaEndingCreditsAuto>();
        }

        SerializedObject so = new SerializedObject(ending);

        SetBool(so, "enableTKey", true);
        SetBool(so, "showDebugButton", false);

        SetFloat(so, "totalDuration", 120f);
        SetFloat(so, "fadeInDuration", 2.4f);
        SetFloat(so, "fadeOutDuration", 1.2f);

        SetBool(so, "autoFitAllTextInDuration", true);
        SetFloat(so, "scrollSpeed", 180f);
        SetFloat(so, "textFitDuration", 44f);
        SetFloat(so, "startTextTopY", -260f);
        SetFloat(so, "textWidth", 1240f);

        SetFloat(so, "titleFontSize", 52f);
        SetFloat(so, "headingFontSize", 40f);
        SetFloat(so, "bodyFontSize", 30f);
        SetFloat(so, "smallFontSize", 24f);
        SetFloat(so, "lineSpacing", 32f);
        SetFloat(so, "paragraphSpacing", 82f);
        SetFloat(so, "sectionSpacing", 165f);

        SetFloat(so, "galleryStartTime", 40f);
        SetFloat(so, "slideDuration", 4f);
        SetFloat(so, "slideFadeDuration", 0.45f);
        SetFloat(so, "videoMaxDuration", 8f);

        SetBool(so, "enableDecorations", true);
        SetInt(so, "decorationCount", 3);
        SetFloat(so, "decorationAlpha", 0.06f);
        SetFloat(so, "decorationMoveAmount", 18f);
        SetFloat(so, "decorationSpeed", 0.30f);

        SetBool(so, "muteOtherAudioSources", true);
        SetFloat(so, "musicVolume", 0.85f);
        SetFloat(so, "musicStartDelay", 1f);
        SetFloat(so, "musicFadeOutDuration", 4f);
        SetObjectString(so, "finalThanksText", "감사합니다^^");
        SetFloat(so, "finalThanksFontSize", 54f);
        SetBool(so, "goToMainMenuAfterEnding", false);

        AudioClip music = FindEndingMusic();
        if (music != null)
        {
            SetObject(so, "endingMusic", music);
        }

        TMP_FontAsset font = FindKoreanFont();
        if (font != null)
        {
            SetObject(so, "koreanFont", font);
        }
        else
        {
            Debug.LogWarning("[EndingCredits] 한글 TMP 폰트를 자동으로 찾지 못했습니다. Galmuri11 TMP Font Asset을 Korean Font 칸에 직접 연결하세요.");
        }

        CopyLikelyVideosIntoStreamingAssets();

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(system);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.Refresh();

        Debug.Log("[EndingCredits] v8 설치 완료");
        Debug.Log("[EndingCredits] 동영상 권장 위치: Assets/StreamingAssets/EndingCredits/Videos/01_making.mp4");
        Debug.Log("[EndingCredits] 글 수정: Assets/Resources/EndingCredits/EndingCreditsText.txt");
        Debug.Log("[EndingCredits] 테스트: Play 후 T키");
    }

    private static void CreateFolders()
    {
        Directory.CreateDirectory("Assets/Scripts/Ending");
        Directory.CreateDirectory("Assets/Resources/EndingCredits");
        Directory.CreateDirectory("Assets/Resources/EndingCredits/Gallery");
        Directory.CreateDirectory("Assets/Resources/EndingCredits/Decorations");
        Directory.CreateDirectory("Assets/Resources/EndingCredits/Videos");
        Directory.CreateDirectory("Assets/StreamingAssets/EndingCredits/Videos");
    }

    private static void CreateOrUpdateV8Text()
    {
        string path = "Assets/Resources/EndingCredits/EndingCreditsText.txt";

        if (File.Exists(path))
        {
            string backup = "Assets/Resources/EndingCredits/EndingCreditsText_backup_before_v8.txt";
            if (!File.Exists(backup))
            {
                File.Copy(path, backup, false);
            }
        }

        string text =
@"# INFECTED AREA

## 엔딩 크레딧

---

# 당신은 백신이 아니었다.

감염구역은 당신을 치료제로 부르지 않았다.
그저 가장 오래 버틴 감염체에게
마지막 문을 열 권한을 주었을 뿐.

쓰러진 적들의 데이터는
모두 같은 문장을 남겼다.

“우리도 한때는 탈출하고 싶었다.”

칼끝에 남은 것은 승리가 아니라 기억이었다.
검은 연구소의 심장은 멈췄고,
격리문 너머로 처음 바람이 들어왔다.

---

# PROJECT TEAM

김두현
이주윤
김희주
박믿음

---

# CREDITS

- 기획  ____________________
- 프로그래밍  ____________________
- 아트 / UI  ____________________
- 사운드 / 음악  ____________________
- 테스트 플레이  ____________________
- 도움 준 사람들  ____________________

---

# MAKING PROCESS

40초 이후부터 제작 과정 사진과 영상이 이어집니다.";

        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static AudioClip FindEndingMusic()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string lower = path.ToLowerInvariant();

            if (lower.Contains("endingmusic") || lower.Contains("fragile") || lower.Contains("motif"))
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    return clip;
                }
            }
        }

        return null;
    }


    private static TMP_FontAsset FindKoreanFont()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string lower = path.ToLowerInvariant();

            if (lower.Contains("galmuri") || lower.Contains("korean") || lower.Contains("kr") || lower.Contains("noto"))
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null && !lower.Contains("liberation"))
                {
                    return font;
                }
            }
        }

        return null;
    }

    private static void CopyLikelyVideosIntoStreamingAssets()
    {
        string targetFolder = "Assets/StreamingAssets/EndingCredits/Videos";
        Directory.CreateDirectory(targetFolder);

        string[] existing = Directory.GetFiles(targetFolder, "*.mp4", SearchOption.TopDirectoryOnly);
        if (existing.Length > 0)
        {
            return;
        }

        string projectRoot = Directory.GetCurrentDirectory();
        string[] candidates = Directory.GetFiles(projectRoot, "*.mp4", SearchOption.AllDirectories);

        foreach (string source in candidates)
        {
            string normalized = source.Replace('\\', '/');
            if (normalized.Contains("/Library/") || normalized.Contains("/Temp/") || normalized.Contains("/Obj/"))
            {
                continue;
            }

            string fileName = Path.GetFileName(source);
            if (fileName.ToLowerInvariant().Contains("making") || fileName.StartsWith("01_"))
            {
                string dest = Path.Combine(targetFolder, fileName);
                if (!File.Exists(dest))
                {
                    File.Copy(source, dest, true);
                    Debug.Log("[EndingCredits] 동영상 자동 복사: " + normalized + " -> " + dest);
                }
                return;
            }
        }
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.intValue = value;
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.floatValue = value;
    }

    private static void SetObjectString(SerializedObject so, string name, string value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.stringValue = value;
    }

    private static void SetObject(SerializedObject so, string name, Object value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.objectReferenceValue = value;
    }
}
