using UnityEngine;

public enum ExecutorRewardType
{
    Byte,
    Ammo
}

[DisallowMultipleComponent]
public sealed class ExecutorRewardOrb : MonoBehaviour
{
    private ExecutorRewardType rewardType;
    private int value;
    private Transform player;
    private Vector2 velocity;
    private float age;
    private bool collected;
    private SpriteRenderer renderer;

    public static ExecutorRewardOrb Create(Vector3 position, ExecutorRewardType type, int value, Transform player)
    {
        GameObject root = new GameObject(type == ExecutorRewardType.Byte ? "EXE_Reward_Byte" : "EXE_Reward_Ammo");
        root.transform.position = position;
        SpriteRenderer sprite = root.AddComponent<SpriteRenderer>();
        sprite.sprite = type == ExecutorRewardType.Byte
            ? ExecutorRuntimeSprites.GetByteReward()
            : ExecutorRuntimeSprites.GetAmmoReward();
        sprite.color = Color.white;
        sprite.sortingOrder = 30;
        root.transform.localScale = Vector3.one * 0.85f;

        ExecutorRewardOrb orb = root.AddComponent<ExecutorRewardOrb>();
        orb.rewardType = type;
        orb.value = Mathf.Max(1, value);
        orb.player = player;
        orb.renderer = sprite;
        Vector2 direction = Random.insideUnitCircle.normalized;
        if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;
        orb.velocity = direction * Random.Range(2.2f, 4f);
        return orb;
    }

    private void Update()
    {
        if (collected) return;
        age += Time.deltaTime;

        if (player == null)
        {
            PlayerHealth health = FindAnyObjectByType<PlayerHealth>();
            if (health != null) player = health.transform;
        }

        if (age < 0.48f)
        {
            velocity = Vector2.Lerp(velocity, Vector2.zero, Time.deltaTime * 4.5f);
            transform.position += (Vector3)(velocity * Time.deltaTime);
        }
        else if (player != null)
        {
            Vector2 toPlayer = player.position - transform.position;
            float distance = toPlayer.magnitude;
            Vector2 desired = distance > 0.001f ? toPlayer / distance * Mathf.Lerp(4f, 12f, Mathf.Clamp01((age - 0.48f) * 1.8f)) : Vector2.zero;
            velocity = Vector2.MoveTowards(velocity, desired, 22f * Time.deltaTime);
            transform.position += (Vector3)(velocity * Time.deltaTime);
            if (distance <= 0.32f)
                Collect();
        }

        transform.Rotate(0f, 0f, 220f * Time.deltaTime);
        float pulse = 1f + Mathf.Sin(age * 10f) * 0.08f;
        transform.localScale = Vector3.one * 0.85f * pulse;

        if (age > 5f)
            Collect();
    }

    private void Collect()
    {
        if (collected) return;
        collected = true;
        if (rewardType == ExecutorRewardType.Byte)
        {
            if (ByteCurrencyManager.Instance != null)
                ByteCurrencyManager.Instance.AddBytes(value);
        }
        else
        {
            PlayerAmmoController ammo = FindAnyObjectByType<PlayerAmmoController>();
            if (ammo != null)
                ammo.AddReserveAmmo(value);
        }
        Destroy(gameObject);
    }
}
