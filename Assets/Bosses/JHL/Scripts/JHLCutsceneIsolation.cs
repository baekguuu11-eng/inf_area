using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hides normal HUD canvases and mutes scene music/ambient during the JHL intro.
/// Restores every captured state when the cutscene ends or is skipped.
/// </summary>
public sealed class JHLCutsceneIsolation
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
            if (source == null || IsProtected(source, keepAudioRoot)) continue;
            if (!IsMusicOrAmbient(source)) continue;
            audioSources.Add(new AudioState { Source = source, Muted = source.mute });
            source.mute = true;
        }
    }

    private static bool IsProtected(AudioSource source, Transform keepAudioRoot)
    {
        if (keepAudioRoot == null || source == null) return false;
        return source.transform == keepAudioRoot || source.transform.IsChildOf(keepAudioRoot);
    }

    private static bool IsMusicOrAmbient(AudioSource source)
    {
        string objectName = source.gameObject.name.ToLowerInvariant();
        string clipName = source.clip != null ? source.clip.name.ToLowerInvariant() : string.Empty;
        if (objectName.Contains("bgm") || objectName.Contains("music") || objectName.Contains("ambient") ||
            clipName.Contains("bgm") || clipName.Contains("music") || clipName.Contains("ambient"))
            return true;
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
