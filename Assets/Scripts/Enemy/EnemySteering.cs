// TARGET: Assets/Scripts/Enemy/EnemySteering.cs
using UnityEngine;

public static class EnemySteering
{
    private static readonly Collider2D[] NearbyBuffer = new Collider2D[24];

    public static Vector2 BuildDirection(
        Rigidbody2D body,
        Vector2 desiredDirection,
        float separationRadius,
        float separationStrength,
        LayerMask wallMask)
    {
        if (body == null || desiredDirection.sqrMagnitude < 0.0001f)
            return Vector2.zero;

        Vector2 desired = desiredDirection.normalized;
        Vector2 separation = Vector2.zero;
        int count = Physics2D.OverlapCircleNonAlloc(body.position, Mathf.Max(0.1f, separationRadius), NearbyBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = NearbyBuffer[i];
            NearbyBuffer[i] = null;
            if (hit == null || hit.attachedRigidbody == body)
                continue;

            EnemyHealth otherHealth = hit.GetComponentInParent<EnemyHealth>();
            if (otherHealth == null)
                continue;

            Vector2 away = body.position - (Vector2)hit.bounds.center;
            float distance = Mathf.Max(0.05f, away.magnitude);
            separation += away.normalized * (1f - Mathf.Clamp01(distance / Mathf.Max(0.1f, separationRadius)));
        }

        Vector2 result = desired + separation * Mathf.Max(0f, separationStrength);
        if (result.sqrMagnitude < 0.0001f)
            result = desired;
        result.Normalize();

        float radius = 0.18f;
        Collider2D ownCollider = body.GetComponent<Collider2D>();
        if (ownCollider != null)
            radius = Mathf.Clamp(Mathf.Min(ownCollider.bounds.extents.x, ownCollider.bounds.extents.y) * 0.72f, 0.12f, 0.55f);

        if (wallMask.value != 0 && Physics2D.CircleCast(body.position, radius, result, 0.5f, wallMask).collider != null)
        {
            Vector2 left = new Vector2(-result.y, result.x);
            Vector2 right = -left;
            bool leftBlocked = Physics2D.CircleCast(body.position, radius, left, 0.42f, wallMask).collider != null;
            bool rightBlocked = Physics2D.CircleCast(body.position, radius, right, 0.42f, wallMask).collider != null;

            if (!leftBlocked && !rightBlocked)
            {
                float leftAlignment = Vector2.Dot(left, desired);
                float rightAlignment = Vector2.Dot(right, desired);
                result = leftAlignment >= rightAlignment ? left : right;
            }
            else if (!leftBlocked)
            {
                result = left;
            }
            else if (!rightBlocked)
            {
                result = right;
            }
            else
            {
                result = Vector2.zero;
            }
        }

        return result;
    }
}
