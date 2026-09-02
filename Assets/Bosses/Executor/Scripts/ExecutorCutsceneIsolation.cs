using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 컷신 동안 기존 HUD/상점 Canvas와 일반 음악만 런타임에서 숨긴다.
/// 보스 전용 SFX는 BGM과 별도 경로로 유지한다.
/// </summary>
public sealed class ExecutorCutsceneIsolation
{
    private struct CanvasState
    {
        public Canvas Canvas;
        public bool Enabled;
    }

    private struct AudioState
    {
        public AudioSource Source;
        public bool Muted;
    }

    private readonly List<CanvasState> canvases = new List<CanvasState>();
    private readonly List<AudioState> audioSources = new List<AudioState>();
    private bool active;

    public void Begin(Canvas keepCanvas, Transform keepAudioRoot)
    {
        if (active) return;
        active = true;
        canvases.Clear();
        audioSources.Clear();

        Canvas[] foundCanvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        for (int i = 0; i < foundCanvases.Length; i++)
        {
            Canvas canvas = foundCanvases[i];
            if (canvas == null || canvas == keepCanvas) continue;
            canvases.Add(new CanvasState { Canvas = canvas, Enabled = canvas.enabled });
            canvas.enabled = false;
        }

        AudioSource[] foundSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        for (int i = 0; i < foundSources.Length; i++)
        {
            AudioSource source = foundSources[i];
            if (source == null || IsProtectedBossSource(source, keepAudioRoot))
                continue;
            if (!IsMusicOrAmbient(source))
                continue;

            audioSources.Add(new AudioState { Source = source, Muted = source.mute });
            source.mute = true;
        }
    }

    private static bool IsProtectedBossSource(AudioSource source, Transform keepAudioRoot)
    {
        if (source == null)
            return false;

        if (source.GetComponentInParent<ExecutorBossAudio>() != null)
            return true;

        if (keepAudioRoot != null &&
            (source.transform == keepAudioRoot || source.transform.IsChildOf(keepAudioRoot)))
            return true;

        return false;
    }

    private static bool IsMusicOrAmbient(AudioSource source)
    {
        string objectName = source.gameObject.name.ToLowerInvariant();
        string clipName = source.clip != null ? source.clip.name.ToLowerInvariant() : string.Empty;

        if (objectName.Contains("bgm") || objectName.Contains("music") || objectName.Contains("ambient") ||
            clipName.Contains("bgm") || clipName.Contains("music") || clipName.Contains("ambient"))
            return true;

        // 일반적으로 길게 반복되는 장면 오디오는 환경음/음악이다.
        // 보스 럼블은 IsProtectedBossSource에서 먼저 제외된다.
        return source.loop && source.clip != null && source.clip.length >= 4f;
    }

    public void Restore()
    {
        if (!active) return;
        active = false;
        for (int i = 0; i < canvases.Count; i++)
        {
            CanvasState state = canvases[i];
            if (state.Canvas != null) state.Canvas.enabled = state.Enabled;
        }
        for (int i = 0; i < audioSources.Count; i++)
        {
            AudioState state = audioSources[i];
            if (state.Source != null) state.Source.mute = state.Muted;
        }
        canvases.Clear();
        audioSources.Clear();
    }
}
