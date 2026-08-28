using System.Collections.Generic;
using UnityEngine;

public static class EnemyShardPoolV61
{
    private const int MaximumPoolSize = 180;
    private static readonly Stack<EnemyDeathShard> pool = new Stack<EnemyDeathShard>();
    private static Transform root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPool()
    {
        pool.Clear();
        root = null;
    }

    public static EnemyDeathShard Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        EnsureRoot();
        EnemyDeathShard shard = null;
        while (pool.Count > 0 && shard == null)
            shard = pool.Pop();

        if (shard == null)
        {
            GameObject instance;
            if (prefab != null)
                instance = Object.Instantiate(prefab, position, rotation);
            else
            {
                instance = new GameObject("RuntimeEnemyDeathShard_V61");
                instance.AddComponent<SpriteRenderer>();
                Rigidbody2D body = instance.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.linearDamping = 2.5f;
                body.angularDamping = 1.4f;
            }

            shard = instance.GetComponent<EnemyDeathShard>();
            if (shard == null)
                shard = instance.AddComponent<EnemyDeathShard>();
        }

        shard.transform.SetParent(root, false);
        shard.transform.position = position;
        shard.transform.rotation = rotation;
        shard.gameObject.SetActive(true);
        shard.PrepareForReuse();
        return shard;
    }

    public static void Release(EnemyDeathShard shard)
    {
        if (shard == null)
            return;

        EnsureRoot();
        shard.gameObject.SetActive(false);
        shard.transform.SetParent(root, false);
        if (pool.Count < MaximumPoolSize)
            pool.Push(shard);
        else
            Object.Destroy(shard.gameObject);
    }

    private static void EnsureRoot()
    {
        if (root != null)
            return;
        GameObject gameObject = new GameObject("V61_EnemyShardPool");
        gameObject.hideFlags = HideFlags.DontSave;
        root = gameObject.transform;
    }
}
