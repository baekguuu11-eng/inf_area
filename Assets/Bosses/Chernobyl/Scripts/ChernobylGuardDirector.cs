using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Page-scaled reinforcement system for Chernobyl. It intentionally does not use
/// RoomEnemySpawner/EnemySpawnEntrance, so core guards have a unique visual language.
/// </summary>
[DisallowMultipleComponent]
public sealed class ChernobylGuardDirector : MonoBehaviour
{
    private readonly List<ChernobylGuardUnit> active = new List<ChernobylGuardUnit>();
    private ChernobylBossController boss;
    private RoomController room;
    private float nextWaveTime;
    private bool waveRunning;
    private bool shuttingDown;

    public int ActiveGuardCount
    {
        get { CleanupList(); return active.Count; }
    }

    public void Initialize(ChernobylBossController ownerBoss, RoomController ownerRoom)
    {
        boss = ownerBoss;
        room = ownerRoom;
        nextWaveTime = Time.time + 4.8f;
    }

    public bool TryScheduleWave(int page)
    {
        if (shuttingDown || waveRunning || boss == null || boss.IsDead || Time.time < nextWaveTime) return false;
        CleanupList();
        // V15: no stacking waves. Director is called only during a dedicated guard section.
        if (active.Count > 0) return false;
        int desired = page <= 1 ? 1 : page == 2 ? 2 : 3;
        StartCoroutine(SpawnWave(page, desired));
        return true;
    }

    public void RecallForRecoveryV15()
    {
        CleanupList();
        foreach (ChernobylGuardUnit guard in active.ToArray())
            if (guard != null) guard.ShutdownWithoutDamage();
        active.Clear();
    }

    private IEnumerator SpawnWave(int page, int count)
    {
        waveRunning = true;
        List<EnemyType> plan = BuildPlan(page, count);
        List<Vector2> reserved = new List<Vector2>();
        for (int i = 0; i < plan.Count; i++)
        {
            Vector2 position;
            if (!TryFindSpawnPosition(page, reserved, out position)) continue;
            reserved.Add(position);

            GameObject prefab = ResolvePrefab(plan[i]);
            if (prefab == null) continue;
            GameObject instance = Instantiate(prefab, position, Quaternion.identity, room != null ? room.transform : null);
            if (instance == null) continue;

            EnemySpawnEntrance normalEntrance = instance.GetComponent<EnemySpawnEntrance>();
            if (normalEntrance != null) normalEntrance.enabled = false;

            EnemySizeController size = instance.GetComponent<EnemySizeController>();
            if (size != null) size.ApplySizeNow();

            ChernobylGuardUnit guard = instance.GetComponent<ChernobylGuardUnit>();
            if (guard == null) guard = instance.AddComponent<ChernobylGuardUnit>();
            guard.Configure(this, boss, plan[i], page);

            // Guard identity must not change the base enemy dimensions. Re-apply the same
            // EnemySizeController used by normal room spawns after all guard/evolution setup.
            if (size != null) size.ApplySizeNow();
            active.Add(guard);

            ChernobylGuardSpawnFX entrance = instance.GetComponent<ChernobylGuardSpawnFX>();
            if (entrance == null) entrance = instance.AddComponent<ChernobylGuardSpawnFX>();
            entrance.Configure(boss, guard, page, i * 0.11f);
        }

        float cooldown = page <= 1 ? Random.Range(15f, 18f) :
            page == 2 ? Random.Range(14f, 17f) : Random.Range(13f, 16f);
        nextWaveTime = Time.time + cooldown;
        yield return null;
        waveRunning = false;
    }

    private List<EnemyType> BuildPlan(int page, int count)
    {
        List<EnemyType> result = new List<EnemyType>(count);
        int tank = CountType(EnemyType.Tank);
        int bomber = CountType(EnemyType.Bomber);
        int ranged = CountType(EnemyType.Ranged);

        if (count > 0) result.Add(EnemyType.Melee);
        if (count > 1) result.Add(Random.value < 0.58f ? EnemyType.Ranged : EnemyType.Melee);
        while (result.Count < count)
        {
            float roll = Random.value;
            EnemyType choice;
            if (page <= 1)
                choice = roll < 0.68f ? EnemyType.Melee : EnemyType.Ranged;
            else if (page == 2)
            {
                if (roll < 0.40f) choice = EnemyType.Melee;
                else if (roll < 0.70f) choice = EnemyType.Ranged;
                else if (roll < 0.85f) choice = EnemyType.Tank;
                else choice = EnemyType.Bomber;
            }
            else
            {
                if (roll < 0.30f) choice = EnemyType.Melee;
                else if (roll < 0.60f) choice = EnemyType.Ranged;
                else if (roll < 0.76f) choice = EnemyType.Tank;
                else choice = EnemyType.Bomber;
            }

            int plannedTank = CountInPlan(result, EnemyType.Tank);
            int plannedBomber = CountInPlan(result, EnemyType.Bomber);
            int plannedRanged = CountInPlan(result, EnemyType.Ranged);
            if (choice == EnemyType.Tank && tank + plannedTank >= 1) choice = EnemyType.Melee;
            if (choice == EnemyType.Bomber && (page <= 1 || bomber + plannedBomber >= (page >= 3 ? 2 : 1))) choice = EnemyType.Melee;
            if (choice == EnemyType.Ranged && ranged + plannedRanged >= Mathf.CeilToInt(count * 0.5f)) choice = EnemyType.Melee;
            result.Add(choice);
        }
        return result;
    }

    private bool TryFindSpawnPosition(int page, List<Vector2> reserved, out Vector2 result)
    {
        Bounds bounds = GetArenaBounds();
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        int blockedMask = LayerMask.GetMask("Wall", "Obstacle", "RoomBoundary", "Enemy");
        Vector2 core = boss != null ? (Vector2)boss.transform.position : (Vector2)bounds.center;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(2.15f, page >= 3 ? 4.20f : 3.75f);
            Vector2 candidate = core + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            candidate.x = Mathf.Clamp(candidate.x, bounds.min.x + 0.65f, bounds.max.x - 0.65f);
            candidate.y = Mathf.Clamp(candidate.y, bounds.min.y + 0.60f, bounds.max.y - 0.60f);
            if (player != null && Vector2.Distance(candidate, player.position) < 3.0f) continue;
            if (Vector2.Distance(candidate, core) < 1.9f) continue;
            bool tooClose = false;
            for (int i = 0; i < reserved.Count; i++) if (Vector2.Distance(candidate, reserved[i]) < 1.25f) { tooClose = true; break; }
            if (tooClose) continue;
            for (int i = 0; i < active.Count; i++) if (active[i] != null && Vector2.Distance(candidate, active[i].transform.position) < 1.15f) { tooClose = true; break; }
            if (tooClose) continue;
            if (blockedMask != 0 && Physics2D.OverlapCircle(candidate, 0.42f, blockedMask) != null) continue;
            result = candidate;
            return true;
        }
        // Never bypass the collision/distance checks after retry exhaustion.
        result = Vector2.zero;
        return false;
    }

    public void ShutdownAllGuards()
    {
        if (shuttingDown) return;
        shuttingDown = true;
        CleanupList();
        ChernobylGuardUnit[] copy = active.ToArray();
        for (int i = 0; i < copy.Length; i++) if (copy[i] != null) copy[i].ShutdownWithoutDamage();
        active.Clear();
    }

    public void NotifyGuardDestroyed(ChernobylGuardUnit guard)
    {
        if (guard != null) active.Remove(guard);
    }

    private int CountType(EnemyType type)
    {
        CleanupList();
        int count = 0;
        for (int i = 0; i < active.Count; i++) if (active[i] != null && active[i].GuardType == type) count++;
        return count;
    }

    private static int CountInPlan(List<EnemyType> plan, EnemyType type)
    {
        int count = 0;
        for (int i = 0; i < plan.Count; i++) if (plan[i] == type) count++;
        return count;
    }

    private void CleanupList()
    {
        for (int i = active.Count - 1; i >= 0; i--) if (active[i] == null) active.RemoveAt(i);
    }

    private Bounds GetArenaBounds()
    {
        if (room != null)
        {
            Transform left = room.GetSpawnPoint(GateDirection.Left);
            Transform right = room.GetSpawnPoint(GateDirection.Right);
            Transform top = room.GetSpawnPoint(GateDirection.Top);
            Transform bottom = room.GetSpawnPoint(GateDirection.Bottom);
            if (left != null && right != null && top != null && bottom != null)
            {
                float minX = Mathf.Min(left.position.x, right.position.x) - 0.34f;
                float maxX = Mathf.Max(left.position.x, right.position.x) + 0.34f;
                float minY = Mathf.Min(bottom.position.y, top.position.y) - 0.34f;
                float maxY = Mathf.Max(bottom.position.y, top.position.y) + 0.34f;
                return new Bounds(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0f),
                    new Vector3(maxX - minX, maxY - minY, 1f));
            }
            if (room.EnemySpawnArea != null) return room.EnemySpawnArea.bounds;
        }
        return new Bounds(transform.position, new Vector3(12f, 7f, 1f));
    }

    private static GameObject ResolvePrefab(EnemyType type)
    {
        string name = type == EnemyType.Ranged ? "EnemyV611_Ranged" :
            type == EnemyType.Tank ? "EnemyV611_Tank" :
            type == EnemyType.Bomber ? "EnemyV611_Bomber" : "EnemyV611_Melee";
        return Resources.Load<GameObject>("Enemies/" + name);
    }
}
