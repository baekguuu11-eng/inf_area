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
        ExecutorBossController executor = GetComponent<ExecutorBossController>();

        for (int i = 0; i < ticks; i++)
        {
            yield return new WaitForSeconds(Mathf.Max(0.08f, interval));
            if (health == null || health.IsDead)
                break;

            // 방화벽검 도트가 잠복 중인 집행자를 다시 지상 위치로 보이게 하던 버그 방지.
            // 남은 틱은 버리지 않고, 보스가 다시 타격 가능한 상태가 될 때까지 일시 정지한다.
            while (executor != null && !executor.CanReceivePersistentDamage)
            {
                if (health == null || health.IsDead)
                    break;
                yield return null;
            }

            if (health == null || health.IsDead)
                break;

            health.TakeDamage(new DamageContext(damage, direction, transform.position,
                EnemyHitKind.Environment, 0f));
        }
        routine = null;
    }
}
