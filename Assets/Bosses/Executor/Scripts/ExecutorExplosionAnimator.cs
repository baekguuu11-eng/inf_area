using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExecutorExplosionAnimator : MonoBehaviour
{
    private static Sprite[] cachedFrames;
    private SpriteRenderer rendererComponent;

    public static ExecutorExplosionAnimator Create(ExecutorBossController owner, Vector3 position, float radius, bool stronger)
    {
        GameObject root = new GameObject(stronger ? "EXE_Explosion_Phase2" : "EXE_Explosion");
        root.transform.position = position + new Vector3(0f, 0f, -0.04f);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 46;
        renderer.color = stronger ? new Color(1f, 0.78f, 0.62f, 1f) : Color.white;
        ExecutorExplosionAnimator animator = root.AddComponent<ExecutorExplosionAnimator>();
        animator.rendererComponent = renderer;
        if (owner != null) owner.RegisterSpawnedObject(root);
        animator.StartCoroutine(animator.Play(radius, stronger));
        return animator;
    }

    private static Sprite[] Frames
    {
        get
        {
            if (cachedFrames == null || cachedFrames.Length == 0)
            {
                cachedFrames = Resources.LoadAll<Sprite>("Bosses/Executor/Explosion");
                Array.Sort(cachedFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            return cachedFrames;
        }
    }

    private IEnumerator Play(float radius, bool stronger)
    {
        Sprite[] frames = Frames;
        if (frames == null || frames.Length == 0)
        {
            Destroy(gameObject);
            yield break;
        }

        // 경고 원/피해 판정/폭발 GIF의 실제 지름을 동일하게 맞춘다.
        float targetDiameter = Mathf.Max(0.2f, radius * 2f);
        float width = Mathf.Max(0.01f, frames[0].bounds.size.x);
        transform.localScale = Vector3.one * (targetDiameter / width);

        const float frameDuration = 0.075f;
        for (int i = 0; i < frames.Length; i++)
        {
            if (rendererComponent == null) yield break;
            rendererComponent.sprite = frames[i];
            float elapsed = 0f;
            while (elapsed < frameDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        Destroy(gameObject);
    }
}
