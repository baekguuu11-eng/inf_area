using UnityEngine;

public static class PlayerDamageQuery
{
    public static Collider2D ResolveBodyCollider(PlayerHealth health)
    {
        if (health == null) return null;
        Collider2D root = health.GetComponent<Collider2D>();
        if (IsValid(root)) return root;
        Collider2D[] all = health.GetComponentsInChildren<Collider2D>(true);
        Collider2D best = null; float bestArea = float.MaxValue;
        for (int i=0;i<all.Length;i++)
        {
            Collider2D c=all[i]; if(!IsValid(c))continue;
            string n=c.gameObject.name.ToLowerInvariant(); if(n.Contains("attack")||n.Contains("trigger")||n.Contains("weapon")||n.Contains("pickup"))continue;
            float area=c.bounds.size.x*c.bounds.size.y; if(area<bestArea){bestArea=area;best=c;}
        }
        return best;
    }
    public static bool IsBodyHit(PlayerHealth health, Collider2D collision)
    {
        if (health == null || collision == null) return false;
        Collider2D body = ResolveBodyCollider(health);
        return body == collision || (body != null && collision.transform.IsChildOf(body.transform));
    }
    public static bool TryDamageCircle(Vector2 center,float radius,int damage)
    {
        PlayerHealth[] players=Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for(int i=0;i<players.Length;i++)
        {
            PlayerHealth h=players[i]; if(h==null||h.IsDead)continue; Collider2D body=ResolveBodyCollider(h); if(body==null)continue;
            Vector2 p=body.ClosestPoint(center); if(Vector2.Distance(center,p)<=Mathf.Max(.01f,radius)){h.TakeDamage(Mathf.Max(1,damage));return true;}
        }
        return false;
    }
    public static bool TryDamageSector(Vector2 center,Vector2 direction,float radius,float angle,int damage)
    {
        Vector2 d=direction.sqrMagnitude>.001f?direction.normalized:Vector2.right;
        PlayerHealth[] players=Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for(int i=0;i<players.Length;i++)
        {
            PlayerHealth h=players[i]; if(h==null||h.IsDead)continue; Collider2D body=ResolveBodyCollider(h); if(body==null)continue;
            Vector2 p=body.ClosestPoint(center); Vector2 rel=p-center;
            if(rel.magnitude<=radius && (rel.sqrMagnitude<.001f||Vector2.Angle(d,rel.normalized)<=angle*.5f)){h.TakeDamage(Mathf.Max(1,damage));return true;}
        }
        return false;
    }
    private static bool IsValid(Collider2D c)=>c!=null&&c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy;
}
