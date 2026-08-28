using UnityEngine;
[RequireComponent(typeof(Rigidbody2D))][RequireComponent(typeof(BoxCollider2D))][DisallowMultipleComponent]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] private float lifetime=4f; private Rigidbody2D body; private int damage=1; private bool consumed;
    private void Awake(){body=GetComponent<Rigidbody2D>();body.gravityScale=0;body.freezeRotation=true;body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;body.interpolation=RigidbodyInterpolation2D.Interpolate;Collider2D c=GetComponent<Collider2D>();c.isTrigger=true;RuntimeProjectileVisual.Ensure(gameObject,false);RuntimeProjectileVisual.ConfigureCollider(c,false);}
    private void Start(){Destroy(gameObject,Mathf.Max(.1f,lifetime));}
    public void Setup(Vector2 direction,float speed,int dmg){damage=Mathf.Max(1,dmg);Vector2 d=direction.sqrMagnitude>.001f?direction.normalized:Vector2.down;body.linearVelocity=d*Mathf.Max(.1f,speed);transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);}
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(consumed||collision==null||collision.GetComponentInParent<EnemyHealth>()!=null)return;
        PlayerHealth health=collision.GetComponentInParent<PlayerHealth>();
        if(health!=null){if(!PlayerDamageQuery.IsBodyHit(health,collision))return;consumed=true;health.TakeDamage(damage);Destroy(gameObject);return;}
        if(collision.isTrigger)return; if(IsWall(collision.gameObject)){consumed=true;Destroy(gameObject);}
    }
    private static bool IsWall(GameObject t){if(t==null)return false;int w=LayerMask.NameToLayer("Wall"),o=LayerMask.NameToLayer("Obstacle"),b=LayerMask.NameToLayer("RoomBoundary");if(t.layer==w||t.layer==o||t.layer==b)return true;string n=t.name.ToLowerInvariant();return n.Contains("wall")||n.Contains("boundary");}
}
