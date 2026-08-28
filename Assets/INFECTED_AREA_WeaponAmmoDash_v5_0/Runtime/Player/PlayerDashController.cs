using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public sealed class PlayerDashController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode dashKey = KeyCode.E;

    [Header("Dash")]
    [SerializeField, Min(0.1f)] private float dashDistance = 0.90f;
    [SerializeField, Min(0.03f)] private float dashDuration = 0.11f;
    [SerializeField, Min(0.05f)] private float dashCooldown = 0.85f;
    [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.06f;
    [SerializeField, Min(0.005f)] private float wallSkin = 0.035f;

    [Header("Afterimage")]
    [SerializeField, Range(1, 8)] private int afterimageCount = 3;
    [SerializeField, Min(0.01f)] private float afterimageLifetime = 0.16f;
    [SerializeField] private Color afterimageColor = new Color(0.2f, 0.95f, 1f, 0.55f);

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[12];
    private Rigidbody2D body;
    private PlayerMovement movement;
    private PlayerCombat combat;
    private SpriteRenderer bodyRenderer;
    private Coroutine dashRoutine;
    private float nextDashTime;
    private float invulnerableUntil;
    private Vector2 lastDashDirection = Vector2.down;

    public bool IsDashing => dashRoutine != null;
    public bool IsInvulnerable => Time.unscaledTime < invulnerableUntil;
    public float CooldownRemaining => Mathf.Max(0f, nextDashTime - Time.unscaledTime);

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        bodyRenderer = FindBodyRenderer();
    }

    private void Update()
    {
        if (movement != null && movement.MoveInput.sqrMagnitude > 0.0001f)
            lastDashDirection = movement.MoveInput.normalized;

        if (GameInputState.IsLocked || IsDashing || Time.unscaledTime < nextDashTime)
            return;

        if (Input.GetKeyDown(dashKey))
            TryDash();
    }

    public bool TryDash()
    {
        if (GameInputState.IsLocked || IsDashing || Time.unscaledTime < nextDashTime)
            return false;

        Vector2 direction = movement != null && movement.MoveInput.sqrMagnitude > 0.0001f
            ? movement.MoveInput.normalized
            : lastDashDirection;
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.down;

        if (combat == null)
            combat = GetComponent<PlayerCombat>();
        if (combat != null)
            combat.SendMessage("CancelCurrentActionForDash", SendMessageOptions.DontRequireReceiver);

        dashRoutine = StartCoroutine(DashRoutine(direction.normalized));
        return true;
    }

    private IEnumerator DashRoutine(Vector2 direction)
    {
        nextDashTime = Time.unscaledTime + dashCooldown;
        invulnerableUntil = Time.unscaledTime + Mathf.Min(invulnerabilityDuration, dashDuration);
        body.linearVelocity = Vector2.zero;

        float safeDistance = CalculateSafeDistance(direction, dashDistance);
        Vector2 start = body.position;
        Vector2 target = start + direction * safeDistance;
        float elapsed = 0f;
        int spawned = 0;

        while (elapsed < dashDuration)
        {
            elapsed += Time.fixedUnscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, dashDuration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            body.MovePosition(Vector2.Lerp(start, target, eased));

            int expected = Mathf.Min(afterimageCount, Mathf.FloorToInt(t * (afterimageCount + 1)));
            while (spawned < expected)
            {
                SpawnAfterimage();
                spawned++;
            }
            yield return new WaitForFixedUpdate();
        }

        body.position = target;
        body.linearVelocity = Vector2.zero;
        dashRoutine = null;
    }

    private float CalculateSafeDistance(Vector2 direction, float requestedDistance)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));

        int count = body.Cast(direction, filter, castHits, requestedDistance);
        float nearest = requestedDistance;
        for (int i = 0; i < count; i++)
        {
            Collider2D hitCollider = castHits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                continue;
            if (hitCollider.GetComponentInParent<EnemyHealth>() != null)
                continue;
            nearest = Mathf.Min(nearest, Mathf.Max(0f, castHits[i].distance - wallSkin));
        }
        return nearest;
    }

    private void SpawnAfterimage()
    {
        if (bodyRenderer == null || bodyRenderer.sprite == null)
            bodyRenderer = FindBodyRenderer();
        if (bodyRenderer == null || bodyRenderer.sprite == null)
            return;

        GameObject ghost = new GameObject("DashAfterimage");
        ghost.transform.position = bodyRenderer.transform.position;
        ghost.transform.rotation = bodyRenderer.transform.rotation;
        ghost.transform.localScale = bodyRenderer.transform.lossyScale;

        SpriteRenderer renderer = ghost.AddComponent<SpriteRenderer>();
        renderer.sprite = bodyRenderer.sprite;
        renderer.flipX = bodyRenderer.flipX;
        renderer.flipY = bodyRenderer.flipY;
        renderer.sortingLayerID = bodyRenderer.sortingLayerID;
        renderer.sortingOrder = bodyRenderer.sortingOrder - 1;
        renderer.color = afterimageColor;

        DashAfterimage fade = ghost.AddComponent<DashAfterimage>();
        fade.Configure(afterimageLifetime, afterimageColor);
    }

    private SpriteRenderer FindBodyRenderer()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null)
                continue;
            string lower = renderer.gameObject.name.ToLowerInvariant();
            if (!lower.Contains("weapon") && !lower.Contains("trail") && !lower.Contains("shadow"))
                return renderer;
        }
        return null;
    }
}

public sealed class DashAfterimage : MonoBehaviour
{
    private float duration;
    private float elapsed;
    private Color startColor;
    private SpriteRenderer rendererComponent;

    public void Configure(float lifeTime, Color color)
    {
        duration = Mathf.Max(0.03f, lifeTime);
        startColor = color;
        rendererComponent = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
        if (rendererComponent != null)
        {
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            rendererComponent.color = color;
        }
        transform.localScale *= Mathf.Pow(0.90f, Time.unscaledDeltaTime * 10f);
        if (elapsed >= duration)
            Destroy(gameObject);
    }
}
