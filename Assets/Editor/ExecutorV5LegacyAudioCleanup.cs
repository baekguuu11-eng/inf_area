#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
public static class ExecutorV5LegacyAudioCleanup
{
    private static readonly string[] LegacyPaths =
    {
        "Assets/Resources/Bosses/Executor/Audio/Executor_TypeHitHeavy.wav",
        "Assets/Resources/Bosses/Executor/Audio/Executor_ArtilleryFall.wav",
        "Assets/Resources/Bosses/Executor/Audio/Executor_MachineActivate.wav",
        "Assets/Resources/Bosses/Executor/Audio/Executor_ArtilleryLaunch.wav",
        "Assets/Resources/Bosses/Executor/Audio/Executor_PhaseOverload.wav",
        "Assets/Resources/Bosses/Executor/Audio/Executor_RumbleLoop.wav",
        "Assets/Resources/Bosses/Executor/Audio/Executor_TypeClick.wav",
        "Assets/Resources/Bosses/Executor/Audio/Executor_Burrow.wav",
        "Assets/Resources/Bosses/Executor/Audio/Executor_Explosion.wav",
        "Assets/Resources/Bosses/Executor/Audio/Executor_RiseImpact.wav",
        "Assets/Resources/Bosses/Executor/Audio/Executor_GroundCreak.wav"
    };

    static ExecutorV5LegacyAudioCleanup()
    {
        EditorApplication.delayCall += RemoveLegacyAudio;
    }

    private static void RemoveLegacyAudio()
    {
        bool changed = false;
        for (int i = 0; i < LegacyPaths.Length; i++)
        {
            if (AssetDatabase.LoadMainAssetAtPath(LegacyPaths[i]) != null)
                changed |= AssetDatabase.DeleteAsset(LegacyPaths[i]);
        }
        if (changed) AssetDatabase.Refresh();
    }
}
#endif
