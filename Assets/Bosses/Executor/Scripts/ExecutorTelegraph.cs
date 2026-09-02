using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExecutorTelegraph : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float duration;
    private float elapsed;
    private Vector3 baseScale;
    private Color startColor;
    private Color endColor;

    public bool IsFinished => elapsed >= duration;
    public float Progress => duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;

    public static ExecutorTelegraph Create(ExecutorBossController owner, Vector3 position, float radius,
        float duration, Color startColor, Color endColor, string name = "EXE_Telegraph")
    {
        GameObject root = new GameObject(name);
        root.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = ExecutorRuntimeSprites.GetWarning();
        renderer.sortingOrder = 24;

        ExecutorTelegraph telegraph = root.AddComponent<ExecutorTelegraph>();
        telegraph.spriteRenderer = renderer;
        telegraph.duration = Mathf.Max(0.05f, duration);
        telegraph.startColor = startColor;
        telegraph.endColor = endColor;
        renderer.color = startColor;

        float spriteSize = Mathf.Max(0.001f, renderer.sprite.bounds.size.x);
        float scale = radius * 2f / spriteSize;
        telegraph.baseScale = Vector3.one * scale;
        root.transform.localScale = telegraph.baseScale;

        if (owner != null)
            owner.RegisterSpawnedObject(root);
        return telegraph;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Progress;
        float pulseFrequency = Mathf.Lerp(6f, 18f, t);
        float pulseAmount = Mathf.Lerp(0.025f, 0.09f, t);
        transform.localScale = baseScale * (1f + Mathf.Sin(elapsed * pulseFrequency) * pulseAmount);

        if (spriteRenderer != null)
        {
            Color color = Color.Lerp(startColor, endColor, t * t);
            if (t > 0.72f)
                color.a *= Mathf.PingPong(elapsed * 14f, 1f) > 0.32f ? 1f : 0.32f;
            spriteRenderer.color = color;
        }
    }
}
