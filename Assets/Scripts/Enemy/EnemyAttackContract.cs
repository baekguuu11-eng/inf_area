using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAttackContract : MonoBehaviour
{
    public enum AttackPhase { Idle, Telegraph, Active, Recovery }

    public AttackPhase Phase { get; private set; } = AttackPhase.Idle;
    public int AttackInstanceId { get; private set; }

    private readonly HashSet<PlayerHealth> hitPlayers = new HashSet<PlayerHealth>();

    public void BeginTelegraph(EnemyTelegraph telegraph)
    {
        AttackInstanceId++;
        Phase = AttackPhase.Telegraph;
        hitPlayers.Clear();
    }

    public void BeginActive(EnemyTelegraph telegraph)
    {
        telegraph?.Hide();
        Phase = AttackPhase.Active;
        hitPlayers.Clear();
    }

    public void BeginRecovery(EnemyTelegraph telegraph)
    {
        telegraph?.Hide();
        Phase = AttackPhase.Recovery;
    }

    public void CancelAttack(EnemyTelegraph telegraph)
    {
        telegraph?.Hide();
        Phase = AttackPhase.Idle;
        hitPlayers.Clear();
    }

    public void FinishAttack(EnemyTelegraph telegraph)
    {
        CancelAttack(telegraph);
    }

    public bool TryDamageCircle(Vector2 center, float radius, int damage)
    {
        if (Phase != AttackPhase.Active)
            return false;

        float safeRadius = Mathf.Max(0.01f, radius);
        bool any = false;
        PlayerHealth[] players = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i];
            Collider2D body = PlayerDamageQuery.ResolveBodyCollider(health);
            if (!CanDamage(health, body))
                continue;

            Vector2 nearest = body.ClosestPoint(center);
            if (Vector2.Distance(center, nearest) <= safeRadius)
                any |= Damage(health, damage);
        }
        return any;
    }

    public bool TryDamageCapsule(Vector2 from, Vector2 to, float radius, int damage)
    {
        if (Phase != AttackPhase.Active)
            return false;

        float safeRadius = Mathf.Max(0.01f, radius);
        bool any = false;
        PlayerHealth[] players = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i];
            Collider2D body = PlayerDamageQuery.ResolveBodyCollider(health);
            if (!CanDamage(health, body))
                continue;

            Vector2 segmentPoint = ClosestPointOnSegment(body.bounds.center, from, to);
            Vector2 bodyPoint = body.ClosestPoint(segmentPoint);
            if (Vector2.Distance(segmentPoint, bodyPoint) <= safeRadius)
                any |= Damage(health, damage);
        }
        return any;
    }

    public bool TryDamageSector(Vector2 center, Vector2 direction, float radius, float angle, int damage)
    {
        if (Phase != AttackPhase.Active)
            return false;

        float safeRadius = Mathf.Max(0.01f, radius);
        float halfAngle = Mathf.Clamp(angle * 0.5f, 0f, 180f);
        Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        bool any = false;

        PlayerHealth[] players = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth health = players[i];
            Collider2D body = PlayerDamageQuery.ResolveBodyCollider(health);
            if (!CanDamage(health, body))
                continue;

            // 실제 플레이어 몸체의 가장 가까운 지점을 사용한다.
            // 무기/대시/트리거 콜라이더가 먼저 검색되어 공격이 빗나가는 문제를 방지한다.
            Vector2 nearest = body.ClosestPoint(center);
            Vector2 relative = nearest - center;
            float distance = relative.magnitude;
            if (distance > safeRadius)
                continue;

            bool insideAngle = relative.sqrMagnitude < 0.0001f ||
                Vector2.Angle(forward, relative.normalized) <= halfAngle;
            if (insideAngle)
                any |= Damage(health, damage);
        }

        return any;
    }

    private bool CanDamage(PlayerHealth health, Collider2D body)
    {
        return health != null && !health.IsDead && body != null &&
            body.enabled && body.gameObject.activeInHierarchy && !hitPlayers.Contains(health);
    }

    private bool Damage(PlayerHealth health, int damage)
    {
        if (health == null || hitPlayers.Contains(health))
            return false;

        hitPlayers.Add(health);
        health.TakeDamage(Mathf.Max(1, damage));
        return true;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float lengthSquared = delta.sqrMagnitude;
        if (lengthSquared <= 0.000001f)
            return from;

        float t = Mathf.Clamp01(Vector2.Dot(point - from, delta) / lengthSquared);
        return from + delta * t;
    }

    private void OnDisable()
    {
        Phase = AttackPhase.Idle;
        hitPlayers.Clear();
    }
}
