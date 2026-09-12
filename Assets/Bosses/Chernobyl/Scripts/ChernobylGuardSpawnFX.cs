using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chernobyl-only spawn entrance. Deliberately different from normal room EnemySpawnEntrance:
/// toxic-green marker, reactor-core energy link, scanline and digital materialization.
/// </summary>
[DisallowMultipleComponent]
public sealed class ChernobylGuardSpawnFX : MonoBehaviour
{
    private readonly List<MonoBehaviour> disabledBehaviours = new List<MonoBehaviour>();
    private readonly List<Collider2D> disabledColliders = new List<Collider2D>();
    private readonly List<SpriteRenderer> bodyRenderers = new List<SpriteRenderer>();
    private readonly List<Color> bodyColors = new List<Color>();
    private readonly List<bool> bodyEnabled = new List<bool>();

    private ChernobylBossController boss;
    private ChernobylGuardUnit guard;
    private float delay;
    private float duration;
    private int page;
    private GameObject cueRoot;
    private SpriteRenderer ring;
    private SpriteRenderer scan;
    private SpriteRenderer link;
    private readonly List<Transform> pixels = new List<Transform>();
    private bool prepared;
    private EnemyVisualMotion visualMotion;
    private EnemySizeController sizeController;
    private Vector3 originalRootScale = Vector3.one;

    public void Configure(ChernobylBossController ownerBoss, ChernobylGuardUnit guardUnit, int bossPage, float startDelay)
    {
        boss = ownerBoss;
        guard = guardUnit;
        page = Mathf.Clamp(bossPage, 1, 3);
        delay = Mathf.Max(0f, startDelay);
        duration = page == 1 ? 0.72f : page == 2 ? 0.62f : 0.54f;
        Prepare();
    }

    private void Prepare()
    {
        if (prepared) return;
        prepared = true;
        originalRootScale = transform.localScale;
        visualMotion = GetComponent<EnemyVisualMotion>();
        sizeController = GetComponent<EnemySizeController>();
        if (sizeController != null) sizeController.ApplySizeNow();
        if (visualMotion != null)
        {
            visualMotion.RefreshBasePose();
            visualMotion.SetSpawnProgress(0f);
        }
        CacheAndDisableGameplay();
        CacheBodyRenderers();
        CreateCue();
    }

    private IEnumerator Start()
    {
        if (!prepared) Prepare();
        float e = 0f;
        while (e < delay)
        {
            e += Time.deltaTime;
            AnimateCue(0f, true);
            yield return null;
        }

        e = 0f;
        while (e < duration)
        {
            e += Time.deltaTime;
            float t = Mathf.Clamp01(e / duration);
            float reveal = 1f - Mathf.Pow(1f - t, 3f);
            for (int i = 0; i < bodyRenderers.Count; i++)
            {
                SpriteRenderer r = bodyRenderers[i];
                if (r == null) continue;
                r.enabled = bodyEnabled[i];
                Color c = bodyColors[i];
                c.a *= reveal;
                r.color = c;
            }
            // Keep the exact same root/world scale as normal room enemies.
            // Only the normal EnemyVisualMotion spawn pose is animated; the Chernobyl identity
            // comes from the green marker/link/scanline, not from making guards larger or smaller.
            if (visualMotion != null) visualMotion.SetSpawnProgress(reveal);
            AnimateCue(t, false);
            yield return null;
        }

        transform.localScale = originalRootScale;
        if (sizeController != null) sizeController.ApplySizeNow();
        if (visualMotion != null) visualMotion.SetSpawnProgress(1f);
        RestoreGameplay();
        if (guard != null) guard.ApplyGuardPalette();
        if (cueRoot != null) Destroy(cueRoot);
        Destroy(this);
    }

    private void CacheAndDisableGameplay()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b = behaviours[i];
            if (b == null || b == this || b is ChernobylGuardUnit || b is EnemyHealth ||
                b is EnemyVisualMotion || b is EnemyCombatFeedback || b is EnemyTelegraph)
                continue;
            string n = b.GetType().Name;
            bool disable = n.EndsWith("AI") || n == "EnemyMotor" || n == "EnemyDamage" || n == "EnemyPerception" ||
                n == "EnemyAnimatedSpriteProxy" || n == "EnemyVisualIntegrityGuard";
            if (disable && b.enabled) { disabledBehaviours.Add(b); b.enabled = false; }
        }
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled)
            {
                disabledColliders.Add(colliders[i]);
                colliders[i].enabled = false;
            }
        }
    }

    private void CacheBodyRenderers()
    {
        SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer r = all[i];
            if (r == null || r.sprite == null) continue;
            string n = r.gameObject.name.ToLowerInvariant();
            if (n.Contains("shadow") || n.Contains("telegraph") || n.Contains("warning") || n.Contains("spawn")) continue;
            bodyRenderers.Add(r);
            bodyColors.Add(r.color);
            bodyEnabled.Add(r.enabled);
            Color c = r.color; c.a = 0f; r.color = c;
        }
    }

    private void CreateCue()
    {
        cueRoot = new GameObject("CHN_GuardSpawnCue");
        Transform parent = boss != null && boss.OwnerRoom != null ? boss.OwnerRoom.transform : null;
        if (parent != null) cueRoot.transform.SetParent(parent, true);
        cueRoot.transform.position = transform.position;

        Color cue = page == 1 ? new Color(0.30f, 1f, 0.24f, 0.78f) :
            page == 2 ? new Color(0.52f, 1f, 0.16f, 0.86f) : new Color(0.72f, 1f, 0.10f, 0.92f);

        GameObject ringObj = new GameObject("GuardMarker");
        ringObj.transform.SetParent(cueRoot.transform, false);
        ring = ringObj.AddComponent<SpriteRenderer>();
        ring.sprite = ChernobylRuntimeSprites.Ring;
        ring.color = cue;
        ring.sortingOrder = 18;
        ringObj.transform.localScale = Vector3.one * 0.88f;

        GameObject scanObj = new GameObject("GuardScanline");
        scanObj.transform.SetParent(cueRoot.transform, false);
        scan = scanObj.AddComponent<SpriteRenderer>();
        scan.sprite = ChernobylRuntimeSprites.WhitePixel;
        scan.color = new Color(cue.r, cue.g, cue.b, 0.72f);
        scan.sortingOrder = 20;
        scanObj.transform.localScale = new Vector3(0.82f, 0.035f, 1f);

        if (boss != null)
        {
            GameObject linkObj = new GameObject("CoreLink");
            linkObj.transform.SetParent(cueRoot.transform, false);
            link = linkObj.AddComponent<SpriteRenderer>();
            link.sprite = ChernobylRuntimeSprites.WhitePixel;
            link.color = new Color(cue.r, cue.g, cue.b, 0.34f);
            link.sortingOrder = 16;
            Vector2 localBoss = cueRoot.transform.InverseTransformPoint(boss.transform.position + Vector3.up * 0.08f);
            float len = localBoss.magnitude;
            linkObj.transform.localPosition = localBoss * 0.5f;
            linkObj.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(localBoss.y, localBoss.x) * Mathf.Rad2Deg);
            linkObj.transform.localScale = new Vector3(len, 0.022f, 1f);
        }

        for (int i = 0; i < 7; i++)
        {
            GameObject p = new GameObject("DataPixel_" + i);
            p.transform.SetParent(cueRoot.transform, false);
            SpriteRenderer pr = p.AddComponent<SpriteRenderer>();
            pr.sprite = ChernobylRuntimeSprites.WhitePixel;
            pr.color = new Color(cue.r, cue.g, cue.b, 0.62f);
            pr.sortingOrder = 19;
            p.transform.localScale = Vector3.one * (0.035f + (i % 3) * 0.012f);
            pixels.Add(p.transform);
        }
    }

    private void AnimateCue(float progress, bool waiting)
    {
        if (cueRoot == null) return;
        float time = Time.unscaledTime;
        if (ring != null)
        {
            float pulse = waiting ? 0.86f + Mathf.Sin(time * 8f) * 0.06f : Mathf.Lerp(0.88f, 1.18f, progress);
            ring.transform.localScale = Vector3.one * pulse;
            Color c = ring.color; c.a = waiting ? 0.66f : Mathf.Lerp(0.82f, 0.18f, progress); ring.color = c;
        }
        if (scan != null)
        {
            float y = Mathf.Lerp(-0.46f, 0.52f, waiting ? (Mathf.Sin(time * 7f) + 1f) * 0.5f : progress);
            scan.transform.localPosition = new Vector3(0f, y, 0f);
        }
        if (link != null)
        {
            Color c = link.color;
            c.a = waiting ? 0.22f + (Mathf.Sin(time * 10f) * 0.5f + 0.5f) * 0.12f : Mathf.Lerp(0.46f, 0f, progress);
            link.color = c;
        }
        for (int i = 0; i < pixels.Count; i++)
        {
            Transform p = pixels[i];
            if (p == null) continue;
            float phaseOffset = i * 0.37f;
            float cycle = Mathf.Repeat(time * (1.6f + page * 0.22f) + phaseOffset, 1f);
            float x = Mathf.Sin(i * 2.17f) * 0.38f;
            p.localPosition = new Vector3(x, Mathf.Lerp(-0.48f, 0.58f, cycle), 0f);
        }
    }

    private void RestoreGameplay()
    {
        for (int i = 0; i < disabledBehaviours.Count; i++) if (disabledBehaviours[i] != null) disabledBehaviours[i].enabled = true;
        for (int i = 0; i < disabledColliders.Count; i++) if (disabledColliders[i] != null) disabledColliders[i].enabled = true;
        for (int i = 0; i < bodyRenderers.Count; i++)
        {
            SpriteRenderer r = bodyRenderers[i];
            if (r == null) continue;
            r.enabled = bodyEnabled[i];
            r.color = bodyColors[i];
        }
    }
}
