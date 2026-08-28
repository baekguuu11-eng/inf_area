using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAttackCoordinator : MonoBehaviour
{
    public enum AttackChannel
    {
        Melee,
        Ranged,
        Heavy,
        Bomber
    }

    private sealed class Lease
    {
        public MonoBehaviour Owner;
        public float ExpireAt;
    }

    public static EnemyAttackCoordinator Instance { get; private set; }

    [Header("동시 공격 제한")]
    [SerializeField, Min(1)] private int maxMeleeAttackers = 2;
    [SerializeField, Min(1)] private int maxRangedAttackers = 2;
    [SerializeField, Min(1)] private int maxHeavyAttackers = 1;
    [SerializeField, Min(1)] private int maxBombersArming = 1;

    [Header("근거리 전투 슬롯")]
    [SerializeField, Range(4, 12)] private int meleeSlotCount = 8;
    [SerializeField] private float slotRotationSpeed = 5f;

    private readonly Dictionary<AttackChannel, List<Lease>> leases = new Dictionary<AttackChannel, List<Lease>>();
    private readonly Dictionary<MonoBehaviour, int> meleeSlots = new Dictionary<MonoBehaviour, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        foreach (AttackChannel channel in System.Enum.GetValues(typeof(AttackChannel)))
            leases[channel] = new List<Lease>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        Cleanup();
    }

    public static EnemyAttackCoordinator EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        EnemyAttackCoordinator existing = Object.FindAnyObjectByType<EnemyAttackCoordinator>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject("EnemyCombatCoordinator");
        return host.AddComponent<EnemyAttackCoordinator>();
    }

    public bool TryAcquire(MonoBehaviour owner, AttackChannel channel, float leaseSeconds = 4f)
    {
        if (owner == null || !owner.isActiveAndEnabled)
            return false;

        CleanupChannel(channel);
        List<Lease> list = leases[channel];
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Owner == owner)
            {
                list[i].ExpireAt = Time.unscaledTime + Mathf.Max(0.5f, leaseSeconds);
                return true;
            }
        }

        if (list.Count >= GetLimit(channel))
            return false;

        list.Add(new Lease
        {
            Owner = owner,
            ExpireAt = Time.unscaledTime + Mathf.Max(0.5f, leaseSeconds)
        });
        return true;
    }

    public void Release(MonoBehaviour owner, AttackChannel channel)
    {
        if (owner == null || !leases.TryGetValue(channel, out List<Lease> list))
            return;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].Owner == owner)
                list.RemoveAt(i);
        }
    }

    public void ReleaseAll(MonoBehaviour owner)
    {
        if (owner == null)
            return;

        foreach (KeyValuePair<AttackChannel, List<Lease>> pair in leases)
        {
            List<Lease> list = pair.Value;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Owner == owner)
                    list.RemoveAt(i);
            }
        }

        meleeSlots.Remove(owner);
    }

    public Vector2 GetMeleeSlotDirection(MonoBehaviour owner)
    {
        CleanupSlots();
        if (owner == null)
            return Vector2.down;

        if (!meleeSlots.TryGetValue(owner, out int slot))
        {
            bool[] occupied = new bool[Mathf.Max(4, meleeSlotCount)];
            foreach (int used in meleeSlots.Values)
            {
                if (used >= 0 && used < occupied.Length)
                    occupied[used] = true;
            }

            slot = 0;
            for (int i = 0; i < occupied.Length; i++)
            {
                if (!occupied[i])
                {
                    slot = i;
                    break;
                }
            }

            if (occupied[slot])
                slot = Mathf.Abs(owner.GetHashCode()) % occupied.Length;

            meleeSlots[owner] = slot;
        }

        float baseAngle = 360f * slot / Mathf.Max(4, meleeSlotCount);
        float subtleRotation = Mathf.Sin(Time.unscaledTime * slotRotationSpeed * 0.1f + slot) * 4f;
        float radians = (baseAngle + subtleRotation) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private int GetLimit(AttackChannel channel)
    {
        switch (channel)
        {
            case AttackChannel.Ranged: return Mathf.Max(1, maxRangedAttackers);
            case AttackChannel.Heavy: return Mathf.Max(1, maxHeavyAttackers);
            case AttackChannel.Bomber: return Mathf.Max(1, maxBombersArming);
            default: return Mathf.Max(1, maxMeleeAttackers);
        }
    }

    private void Cleanup()
    {
        foreach (AttackChannel channel in System.Enum.GetValues(typeof(AttackChannel)))
            CleanupChannel(channel);
        CleanupSlots();
    }

    private void CleanupChannel(AttackChannel channel)
    {
        if (!leases.TryGetValue(channel, out List<Lease> list))
        {
            list = new List<Lease>();
            leases[channel] = list;
        }

        float now = Time.unscaledTime;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Lease lease = list[i];
            if (lease.Owner == null || !lease.Owner.isActiveAndEnabled || lease.ExpireAt <= now)
                list.RemoveAt(i);
        }
    }

    private void CleanupSlots()
    {
        if (meleeSlots.Count == 0)
            return;

        List<MonoBehaviour> invalid = null;
        foreach (MonoBehaviour owner in meleeSlots.Keys)
        {
            if (owner != null && owner.isActiveAndEnabled)
                continue;

            if (invalid == null)
                invalid = new List<MonoBehaviour>();
            invalid.Add(owner);
        }

        if (invalid == null)
            return;
        for (int i = 0; i < invalid.Count; i++)
            meleeSlots.Remove(invalid[i]);
    }
}
