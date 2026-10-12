using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

// Configures references only: never overwrites user-edited credit text or timing JSON.
public static class InfectedAreaEndingCreditsInstaller
{
    [MenuItem("Tools/Infected Area/Configure KOD Credits")]
    public static void Install()
    {
        InfectedAreaEndingCreditsAuto ending = Object.FindAnyObjectByType<InfectedAreaEndingCreditsAuto>();
        if (ending == null)
        {
            GameObject root = new GameObject("EndingCredits_AutoSystem");
            Undo.RegisterCreatedObjectUndo(root,"Create ending credits");
            ending = Undo.AddComponent<InfectedAreaEndingCreditsAuto>(root);
        }
        Undo.RecordObject(ending,"Configure ending credits");
        SerializedObject so = new SerializedObject(ending);
        so.FindProperty("koreanFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/Galmuri11 SDF.asset");
        so.FindProperty("endingMusic").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/UI/EndingMusic.mp3");
        so.FindProperty("creditsText").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/EndingCredits/EndingCreditsText.txt");
        so.FindProperty("settingsJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/EndingCredits/EndingCreditsSettings.json");
        so.FindProperty("gameLogo").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/EndingCredits/GameLogo.png");
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(ending.gameObject.scene);
        Selection.activeGameObject = ending.gameObject;
        Debug.Log("KOD credits configured. Save the scene. T: full / Shift+T: 46s preview / Escape: restore gameplay. Text and JSON are preserved.");
    }
}
