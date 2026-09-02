using UnityEngine;

[DisallowMultipleComponent]
public sealed class AmmoDropper : MonoBehaviour
{
    private static int emptyAmmoKillCounter;
    private EnemyHealth health;
    private EnemyRole role;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        role = GetComponent<EnemyRole>();
    }

    private void OnEnable()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        if (health != null) health.Died += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= HandleDeath;
    }

    private void HandleDeath(EnemyHealth dead)
    {
        PlayerAmmoController ammo = Object.FindAnyObjectByType<PlayerAmmoController>();
        if (ammo == null) return;

        float baseChance = 0.15f;
        int minAmount = 4;
        int maxAmount = 4;
        EnemyRole.Role currentRole = role != null ? role.CurrentRole : EnemyRole.Role.Melee;
        switch (currentRole)
        {
            case EnemyRole.Role.Ranged: baseChance = 0.40f; maxAmount = 7; break;
            case EnemyRole.Role.Tank: baseChance = 0.65f; minAmount = 7; maxAmount = 11; break;
            case EnemyRole.Role.Bomber: baseChance = 0.12f; break;
        }

        float ratio = ammo.TotalAmmoRatio;
        float multiplier = ratio >= 0.70f ? 0.75f : ratio <= 0.25f ? 1.60f : 1f;
        bool guaranteed = false;
        if (ammo.CurrentTotalAmmoEnergy <= 0)
        {
            emptyAmmoKillCounter++;
            guaranteed = emptyAmmoKillCounter >= 2;
        }
        else emptyAmmoKillCounter = 0;

        if (!guaranteed && Random.value > baseChance * multiplier) return;
        emptyAmmoKillCounter = 0;
        SpawnPickup(guaranteed ? 4 : Random.Range(minAmount, maxAmount + 1));
    }

    private void SpawnPickup(int amount)
    {
        SpawnPickupAt(transform.position, amount, GetComponentInParent<RoomController>());
    }

    public static AmmoPickup SpawnPickupAt(Vector3 worldPosition, int amount, RoomController ownerRoom)
    {
        GameObject obj = new GameObject("AmmoPickup");
        if (ownerRoom != null)
            obj.transform.SetParent(ownerRoom.transform, true);
        obj.transform.position = worldPosition;

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 14;

        CircleCollider2D trigger = obj.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.20f;

        AmmoPickup pickup = obj.AddComponent<AmmoPickup>();
        pickup.Initialize(Mathf.Max(1, amount), ownerRoom);
        return pickup;
    }
}
