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
        AttackInstanceId++; Phase = AttackPhase.Telegraph; hitPlayers.Clear();
    }
    public void BeginActive(EnemyTelegraph telegraph)
    {
        telegraph?.Hide(); Phase = AttackPhase.Active; hitPlayers.Clear();
    }
    public void BeginRecovery(EnemyTelegraph telegraph)
    {
        telegraph?.Hide(); Phase = AttackPhase.Recovery;
    }
    public void CancelAttack(EnemyTelegraph telegraph)
    {
        telegraph?.Hide(); Phase = AttackPhase.Idle; hitPlayers.Clear();
    }
    public void FinishAttack(EnemyTelegraph telegraph) => CancelAttack(telegraph);

    public bool TryDamageCircle(Vector2 center, float radius, int damage)
    {
        if (Phase != AttackPhase.Active) return false;
        return DamageHits(Physics2D.OverlapCircleAll(center, Mathf.Max(0.01f, radius)), damage, center);
    }
    public bool TryDamageCapsule(Vector2 from, Vector2 to, float radius, int damage)
    {
        if (Phase != AttackPhase.Active)
            return false;

        radius = Mathf.Max(0.01f, radius);
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.001f)
            return TryDamageCircle(to, radius, damage);

        Vector2 center = (from + to) * 0.5f;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Vector2 size = new Vector2(distance + radius * 2f, radius * 2f);
        Collider2D[] hits = Physics2D.OverlapCapsuleAll(
            center,
            size,
            CapsuleDirection2D.Horizontal,
            angle);

        return DamageHits(hits, damage, center);
    }
    public bool TryDamageSector(Vector2 center, Vector2 direction, float radius, float angle, int damage)
    {
        if (Phase != AttackPhase.Active) return false;
        Collider2D[] all = Physics2D.OverlapCircleAll(center, Mathf.Max(0.01f, radius));
        List<Collider2D> filtered = new List<Collider2D>();
        for (int i=0;i<all.Length;i++)
        {
            if (all[i]==null) continue; Vector2 p=all[i].ClosestPoint(center); Vector2 rel=p-center;
            if (rel.sqrMagnitude<0.0001f || Vector2.Angle(direction,rel.normalized)<=angle*0.5f) filtered.Add(all[i]);
        }
        return DamageHits(filtered.ToArray(), damage, center);
    }
    private bool DamageHits(Collider2D[] hits, int damage, Vector2 center)
    {
        bool any=false;
        for (int i=0;i<hits.Length;i++)
        {
            Collider2D hit=hits[i]; if(hit==null)continue; PlayerHealth health=hit.GetComponentInParent<PlayerHealth>();
            if(health==null||hitPlayers.Contains(health)||!PlayerDamageQuery.IsBodyHit(health,hit))continue;
            hitPlayers.Add(health); health.TakeDamage(Mathf.Max(1,damage)); any=true;
        }
        return any;
    }
    private void OnDisable() { Phase=AttackPhase.Idle; hitPlayers.Clear(); }
}
