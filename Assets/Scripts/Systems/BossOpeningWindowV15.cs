using UnityEngine;

/// <summary>
/// Opt-in boss component: attacks fill a stagger reserve; only the boss director opens
/// a punish window at a safe boundary. Ordinary enemies never receive this component.
/// No health callbacks or recursive damage calls are used.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossOpeningWindowV15 : MonoBehaviour
{
    private EnemyHealth health;
    private Transform focus;
    private Vector2 markerSize;
    private SpriteRenderer[] marks;
    private float stagger;
    private float threshold;
    private float lastHitAt;
    private float closesAt;
    private float visualStrength;
    private bool open;
    private bool broken;

    public bool IsOpen => open && Time.time < closesAt;
    public bool StaggerReady => stagger >= threshold && threshold > 0f;
    public float StaggerRatio => threshold > 0f ? Mathf.Clamp01(stagger / threshold) : 0f;

    public static BossOpeningWindowV15 Attach(GameObject owner, Transform weakPoint, Vector2 size)
    {
        BossOpeningWindowV15 window = owner.GetComponent<BossOpeningWindowV15>();
        if (window == null) window = owner.AddComponent<BossOpeningWindowV15>();
        window.health = owner.GetComponent<EnemyHealth>();
        window.focus = weakPoint;
        window.markerSize = size;
        window.threshold = window.health != null ? window.health.MaxHealth * 0.065f : 180f;
        // V16: no floating bracket UI. Core feedback communicates the opening.
        return window;
    }

    public int ResolveDamage(int rawDamage, EnemyHitKind kind)
    {
        if (rawDamage <= 0) return rawDamage;
        if (!IsOpen)
        {
            stagger = Mathf.Min(threshold, stagger + rawDamage * (kind == EnemyHitKind.Melee ? 1.25f : 1f));
            lastHitAt = Time.time;
            return rawDamage;
        }
        // Reward positioning during recovery. Fire/environment damage remains unamplified.
        float multiplier = kind == EnemyHitKind.Melee ? (broken ? 1.50f : 1.35f) :
            kind == EnemyHitKind.Ranged ? (broken ? 1.35f : 1.20f) : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(rawDamage * multiplier));
    }

    public bool Open(float seconds, bool allowStagger = true)
    {
        broken = allowStagger && StaggerReady;
        if (broken) stagger = 0f;
        open = true;
        closesAt = Time.time + Mathf.Max(0.1f, seconds) + (broken ? 0.90f : 0f);
        return broken;
    }

    public void Close()
    {
        open = false;
        broken = false;
        if (marks != null) foreach (SpriteRenderer mark in marks) if (mark != null) mark.enabled = false;
    }

    public void ResetCombat()
    {
        Close(); stagger = 0f; lastHitAt = Time.time; visualStrength = 0f;
    }

    private void Update()
    {
        if (health == null || health.IsDead) { Close(); return; }
        if (open && Time.time >= closesAt) Close();
        if (!IsOpen && Time.time - lastHitAt > 4f)
            stagger = Mathf.Max(0f, stagger - threshold * 0.055f * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (marks == null || focus == null) return;
        if (health == null || health.IsDead) { ResetCombat(); return; }
        float target = IsOpen ? 1f : StaggerRatio >= 0.4f ? StaggerRatio * 0.36f : 0f;
        visualStrength = Mathf.MoveTowards(visualStrength, target, Time.deltaTime * 7f);
        Color color = IsOpen ? (broken ? new Color(1f, 0.80f, 0.25f) : new Color(0.38f, 1f, 0.90f))
            : new Color(0.55f, 0.65f, 0.70f);
        color.a = visualStrength;
        Vector2 half = markerSize * 0.5f + Vector2.one * 0.12f;
        for (int i = 0; i < 4; i++)
        {
            float sx = (i & 1) == 0 ? -1f : 1f;
            float sy = i < 2 ? -1f : 1f;
            Vector3 corner = focus.position + new Vector3(sx * half.x, sy * half.y, 0f);
            SpriteRenderer h = marks[i * 2], v = marks[i * 2 + 1];
            h.enabled = v.enabled = visualStrength > 0.01f;
            h.color = v.color = color;
            h.transform.position = corner + new Vector3(-sx * 0.12f, 0f, 0f);
            v.transform.position = corner + new Vector3(0f, -sy * 0.12f, 0f);
        }
    }

    private void BuildMarkers()
    {
        if (marks != null) return;
        marks = new SpriteRenderer[8];
        for (int i = 0; i < marks.Length; i++)
        {
            GameObject node = new GameObject("BossOpeningBracket_" + i);
            node.transform.SetParent(transform, false);
            marks[i] = node.AddComponent<SpriteRenderer>();
            marks[i].sprite = RuntimePixelSpriteFactory.GetWhitePixelSprite();
            marks[i].sortingOrder = 39;
            Vector2 native = marks[i].sprite.bounds.size;
            Vector2 size = (i & 1) == 0 ? new Vector2(0.24f, 0.045f) : new Vector2(0.045f, 0.24f);
            node.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
            marks[i].enabled = false;
        }
    }

    private void OnDisable() { Close(); }
}
