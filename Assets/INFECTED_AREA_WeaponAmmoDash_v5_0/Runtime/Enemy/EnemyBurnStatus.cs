using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyBurnStatus : MonoBehaviour
{
    private Coroutine routine;

    public void Apply(int damagePerTick, int ticks, float interval, Vector2 sourceDirection)
    {
        if (damagePerTick <= 0 || ticks <= 0)
            return;
        if (routine != null)
            StopCoroutine(routine);
        routine = StartCoroutine(BurnRoutine(damagePerTick, ticks, interval, sourceDirection));
    }

    private IEnumerator BurnRoutine(int damage, int ticks, float interval, Vector2 direction)
    {
        EnemyHealth health = GetComponent<EnemyHealth>();
        for (int i = 0; i < ticks; i++)
        {
            yield return new WaitForSeconds(Mathf.Max(0.08f, interval));
            if (health == null || health.IsDead)
                break;
            health.TakeDamage(new DamageContext(damage, direction, transform.position, EnemyHitKind.Environment, 0f));
        }
        routine = null;
    }
}
