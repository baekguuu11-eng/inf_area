using UnityEngine;

/// <summary>
/// Marker + lifecycle wrapper for JHL hand collision. The hand is not a hurtbox.
/// The collider is normally disabled and is enabled only while a pattern intentionally
/// uses the hands as moving physical walls (Compression / Prison / Moving Gate).
/// </summary>
[DisallowMultipleComponent]
public sealed class JHLHandBarrier : MonoBehaviour
{
    [SerializeField] private Collider2D barrierCollider;

    public Collider2D BarrierCollider => barrierCollider;
    public bool IsBarrierEnabled => barrierCollider != null && barrierCollider.enabled;

    public void Initialize(Collider2D collider)
    {
        barrierCollider = collider;
    }

    public void ResizeEllipse(Vector2 worldSize)
    {
        if (barrierCollider == null) barrierCollider = GetComponent<Collider2D>();
        PolygonCollider2D polygon = barrierCollider as PolygonCollider2D;
        if (polygon == null) return;

        const int pointCount = 18;
        Vector2[] points = new Vector2[pointCount];
        float radiusX = Mathf.Max(0.1f, worldSize.x * 0.455f);
        float radiusY = Mathf.Max(0.1f, worldSize.y * 0.455f);
        for (int i = 0; i < pointCount; i++)
        {
            float angle = Mathf.PI * 2f * i / pointCount;
            points[i] = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
        }
        polygon.points = points;
    }

    public void SetBarrierEnabled(bool enabledState)
    {
        if (barrierCollider == null) barrierCollider = GetComponent<Collider2D>();
        if (barrierCollider != null) barrierCollider.enabled = enabledState;
    }
}
