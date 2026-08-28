using UnityEngine;
public class EnemyDamage : MonoBehaviour
{
    [SerializeField] private int damage=1;
    [SerializeField] private bool enableContactDamage=false;
    [SerializeField] private float contactCooldown=.9f;
    private float next;
    private void OnCollisionStay2D(Collision2D collision)
    {
        if(!enableContactDamage||Time.time<next)return;
        PlayerHealth h=collision.collider.GetComponentInParent<PlayerHealth>();if(h==null||!PlayerDamageQuery.IsBodyHit(h,collision.collider))return;
        next=Time.time+Mathf.Max(.1f,contactCooldown);h.TakeDamage(Mathf.Max(1,damage));
    }
}
