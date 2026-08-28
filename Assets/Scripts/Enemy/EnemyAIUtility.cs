using UnityEngine;
public static class EnemyAIUtility
{
    public static bool DamagePlayerInCircle(Vector2 center,float radius,int damage)=>PlayerDamageQuery.TryDamageCircle(center,radius,damage);
    public static bool DamagePlayerInSector(Vector2 center,Vector2 direction,float radius,float angleDegrees,int damage)=>PlayerDamageQuery.TryDamageSector(center,direction,radius,angleDegrees,damage);
    public static Vector2 Perpendicular(Vector2 direction,int sign){Vector2 s=direction.sqrMagnitude>.001f?direction.normalized:Vector2.right;return new Vector2(-s.y,s.x)*(sign>=0?1f:-1f);}
}
