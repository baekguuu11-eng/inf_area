using System.Collections;
using UnityEngine;

/// <summary>
/// Delayed green death burst used only by Chernobyl's summoned guards.
/// It never damages other enemies or the boss, preventing chain-reaction cheese.
/// </summary>
public sealed class ChernobylGuardDeathBurst : MonoBehaviour
{
    private float radius;
    private int damage;
    private float delay;
    private Color color;
    private GameObject telegraph;
    private ChernobylBossController ownerBoss;

    public static ChernobylGuardDeathBurst Create(Vector3 position, float radius, int damage, float delay, Color color, Transform parent = null)
    {
        GameObject root = new GameObject("CHN_GuardDeathBurst");
        if (parent != null) root.transform.SetParent(parent, true);
        root.transform.position = position;
        ChernobylGuardDeathBurst burst = root.AddComponent<ChernobylGuardDeathBurst>();
        burst.radius = Mathf.Max(0.25f, radius);
        burst.damage = Mathf.Max(0, damage);
        burst.delay = Mathf.Max(0.02f, delay);
        burst.color = color;
        burst.ownerBoss = parent != null ? parent.GetComponentInChildren<ChernobylBossController>(true) : null;
        return burst;
    }

    private IEnumerator Start()
    {
        telegraph = ChernobylBossEffects.CreateCircleTelegraph(transform.position, radius, delay, color, 17);
        if (telegraph != null) telegraph.transform.SetParent(transform, true);
        ChernobylBossEffects.SpawnCoreSparks(transform.position, color, 5, 1.7f);

        float elapsed = 0f;
        while (elapsed < delay)
        {
            if (ownerBoss == null || ownerBoss.IsDead) { Destroy(gameObject); yield break; }
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (telegraph != null) Destroy(telegraph);
        ChernobylBossEffects.SpawnCircleBlast(transform.position, radius, color);
        ChernobylBossEffects.SpawnCoreSparks(transform.position, color, 10, 2.8f);

        if (damage > 0 && ownerBoss != null && !ownerBoss.IsDead)
            PlayerDamageQuery.TryDamageCircle(transform.position, radius, damage);

        Destroy(gameObject);
    }
}
