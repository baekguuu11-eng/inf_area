using System;
using System.Collections;
using UnityEngine;

public static class ExecutorArtilleryShell
{
    public static IEnumerator LaunchUp(ExecutorBossController owner, Vector3 muzzlePosition, bool phaseTwo, ExecutorBossAudio audio)
    {
        GameObject shell = CreateShell(owner, "EXE_ArtilleryShell_Ascending", muzzlePosition, phaseTwo, 50);
        if (audio != null) audio.PlayArtilleryLaunch();
        Vector3 start = muzzlePosition;
        Vector3 end = start + Vector3.up * 4.2f;
        float elapsed = 0f;
        const float duration = 0.30f;
        while (elapsed < duration && shell != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;
            shell.transform.position = Vector3.Lerp(start, end, eased);
            shell.transform.localScale = Vector3.one * Mathf.Lerp(0.42f, 0.18f, t);
            if (Mathf.Repeat(elapsed, 0.045f) < Time.deltaTime)
                SpawnAfterimage(owner, shell.transform.position, phaseTwo, 49, Mathf.Lerp(0.30f, 0.10f, t));
            yield return null;
        }
        if (shell != null) UnityEngine.Object.Destroy(shell);
    }

    public static IEnumerator Fall(ExecutorBossController owner, Vector3 target, bool phaseTwo, ExecutorBossAudio audio, Action onImpact)
    {
        Vector3 start = target + Vector3.up * 4.0f;
        GameObject shell = CreateShell(owner, "EXE_ArtilleryShell_Falling", start, phaseTwo, 52);
        GameObject shadow = new GameObject("EXE_ArtilleryShadow");
        shadow.transform.position = target + new Vector3(0f, 0f, 0.03f);
        SpriteRenderer shadowRenderer = shadow.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = ExecutorRuntimeSprites.GetGlow();
        shadowRenderer.color = new Color(0.08f, 0.01f, 0.01f, 0.34f);
        shadowRenderer.sortingOrder = 3;
        shadow.transform.localScale = Vector3.one * 0.12f;
        if (owner != null) owner.RegisterSpawnedObject(shadow);
        if (audio != null) audio.PlayArtilleryFall();

        float elapsed = 0f;
        const float duration = 0.27f;
        while (elapsed < duration && shell != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * t;
            shell.transform.position = Vector3.Lerp(start, target, eased);
            shell.transform.localScale = Vector3.one * Mathf.Lerp(0.24f, 0.62f, eased);
            if (shadow != null)
            {
                shadow.transform.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.86f, eased);
                Color c = shadowRenderer.color;
                c.a = Mathf.Lerp(0.22f, 0.58f, eased);
                shadowRenderer.color = c;
            }
            if (Mathf.Repeat(elapsed, 0.035f) < Time.deltaTime)
                SpawnAfterimage(owner, shell.transform.position, phaseTwo, 51, Mathf.Lerp(0.14f, 0.38f, eased));
            yield return null;
        }

        if (shell != null) UnityEngine.Object.Destroy(shell);
        if (shadow != null) UnityEngine.Object.Destroy(shadow);
        onImpact?.Invoke();
    }

    private static GameObject CreateShell(ExecutorBossController owner, string name, Vector3 position, bool phaseTwo, int sortingOrder)
    {
        GameObject shell = new GameObject(name);
        shell.transform.position = position;
        SpriteRenderer renderer = shell.AddComponent<SpriteRenderer>();
        renderer.sprite = ExecutorRuntimeSprites.GetProjectile();
        renderer.sortingOrder = sortingOrder;
        renderer.color = phaseTwo
            ? new Color(1f, 0.30f, 0.08f, 1f)
            : new Color(1f, 0.66f, 0.12f, 1f);
        shell.transform.localScale = Vector3.one * 0.42f;
        if (owner != null) owner.RegisterSpawnedObject(shell);
        return shell;
    }

    private static void SpawnAfterimage(ExecutorBossController owner, Vector3 position, bool phaseTwo, int order, float alpha)
    {
        GameObject ghost = new GameObject("EXE_ArtilleryShell_Trail");
        ghost.transform.position = position;
        SpriteRenderer renderer = ghost.AddComponent<SpriteRenderer>();
        renderer.sprite = ExecutorRuntimeSprites.GetGlow();
        renderer.sortingOrder = order;
        renderer.color = phaseTwo
            ? new Color(1f, 0.18f, 0.04f, alpha)
            : new Color(1f, 0.55f, 0.08f, alpha);
        ghost.transform.localScale = Vector3.one * 0.16f;
        ExecutorArtilleryAfterimage transient = ghost.AddComponent<ExecutorArtilleryAfterimage>();
        transient.Initialize(0.16f);
        if (owner != null) owner.RegisterSpawnedObject(ghost);
    }
}

[DisallowMultipleComponent]
public sealed class ExecutorArtilleryAfterimage : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color startColor;
    private Vector3 startScale;
    private float life;
    private float elapsed;

    public void Initialize(float duration)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        startScale = transform.localScale;
        life = Mathf.Max(0.04f, duration);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / life);
        transform.localScale = Vector3.Lerp(startScale, startScale * 0.35f, t);
        if (spriteRenderer != null)
        {
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            spriteRenderer.color = color;
        }
        if (t >= 1f) Destroy(gameObject);
    }
}
