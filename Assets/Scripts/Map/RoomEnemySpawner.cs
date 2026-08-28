// TARGET: Assets/Scripts/Map/RoomEnemySpawner.cs
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomEnemySpawner : MonoBehaviour
{
    [Header("Enemy Config")]
    [SerializeField] private EnemyWeightConfig weightConfig;

    [Header("Legacy Weight Fields - compatibility")]
    [SerializeField] private float baseMinWeight = 3f;
    [SerializeField] private float baseMaxWeight = 6f;
    [SerializeField] private float proximityIncreasePerRoom = 1f;
    [SerializeField] private float stageIncreasePerStage = 2f;
    [SerializeField] private List<StageSpawnRates> stageSpawnRates = new List<StageSpawnRates>();

    [Header("Room Rules")]
    [SerializeField] private bool skipEnemySpawnInStartRoom = true;
    [SerializeField] private int earlyMinEnemies = 3;
    [SerializeField] private int earlyMaxEnemies = 4;
    [SerializeField] private int midMinEnemies = 4;
    [SerializeField] private int midMaxEnemies = 6;
    [SerializeField] private int lateMinEnemies = 5;
    [SerializeField] private int lateMaxEnemies = 7;

    [Header("Mandatory Melee")]
    [SerializeField] private int meleeMinimumSmallRoom = 1;
    [SerializeField] private int meleeMinimumMediumRoom = 2;
    [SerializeField] private int meleeMinimumLargeRoom = 3;

    [Header("Special Enemy Limits")]
    [SerializeField] private int maxTanksPerWave = 1;
    [SerializeField] private int maxBombersPerWave = 2;
    [SerializeField, Range(0.1f, 0.8f)] private float maxRangedRatio = 0.4f;
    [SerializeField] private bool excludeTankFromFirstCombat = true;
    [SerializeField] private bool excludeBomberFromFirstCombat = true;

    [Header("Natural Spawn Entrance")]
    [SerializeField] private bool useSpawnEntrance = true;
    [SerializeField, Min(0.05f)] private float spawnEntranceDuration = 0.48f;
    [SerializeField, Min(0f)] private float spawnEntranceStagger = 0.075f;

    [Header("Placement")]
    [SerializeField] private int placementRetryCount = 24;
    [SerializeField] private float placementCheckRadius = 0.42f;
    [SerializeField] private float minimumPlayerDistance = 3.25f;
    [SerializeField] private float minimumGateDistance = 1.5f;
    [SerializeField] private float minimumEnemySpacing = 0.95f;

    private readonly List<Vector2> plannedPositions = new List<Vector2>();
    private EnemyType? lastPickedType;

    public void SpawnEnemiesForRoom(RoomController room, int stageNumber)
    {
        if (room == null)
            return;
        if (room.IsPortalRoom)
            return;
        // Only the very first room (Stage 1, Room 1) is safe. Stage 2-1, 3-1, ... are combat rooms.
        if (skipEnemySpawnInStartRoom && stageNumber == 1 && room.RoomNumber == 1)
            return;
        EnemyType fallbackType;
        if (!TryGetAvailableFallback(out fallbackType))
        {
            Debug.LogError("[RoomEnemySpawner] Resources/Enemies의 v6.1.1 적 프리팹까지 찾지 못해 방 생성을 중단했습니다.", this);
            return;
        }
        if (ResolvePrefab(EnemyType.Melee) == null)
        {
            Debug.LogWarning("[RoomEnemySpawner] Melee 프리팹이 누락되어 " + fallbackType + " 프리팹으로 안전 대체합니다.", this);
        }

        BoxCollider2D spawnArea = room.EnemySpawnArea;
        if (spawnArea == null)
        {
            Debug.LogError("[RoomEnemySpawner] " + room.name + "에 EnemySpawnArea가 없어 적 생성을 중단했습니다.", room);
            return;
        }

        float progress = CalculateProgress(stageNumber, room.RoomNumber);
        int targetCount = DetermineTargetCount(progress);
        List<EnemyType> spawnPlan = BuildSpawnPlan(targetCount, progress, stageNumber, room.RoomNumber);
        if (spawnPlan.Count == 0)
            return;

        Transform player = FindPlayer();
        Transform[] gates = CollectGateTransforms(room);
        int blockedMask = LayerMask.GetMask("Wall", "Obstacle", "RoomBoundary", "Enemy");
        plannedPositions.Clear();

        for (int i = 0; i < spawnPlan.Count; i++)
        {
            EnemyType type = spawnPlan[i];
            GameObject prefab = ResolvePrefab(type);
            if (prefab == null)
            {
                EnemyType availableFallbackType;
                if (!TryGetAvailableFallback(out availableFallbackType))
                    continue;
                prefab = ResolvePrefab(availableFallbackType);
                Debug.LogWarning("[RoomEnemySpawner] " + type + " 프리팹이 누락되어 " + availableFallbackType + " 적으로 대체합니다.", this);
            }

            Vector2 position;
            if (!TryFindSafePosition(spawnArea, player, gates, blockedMask, out position))
            {
                Debug.LogWarning("[RoomEnemySpawner] 안전한 스폰 위치를 충분히 찾지 못해 가장 안전한 격자 위치를 사용합니다: " + room.name, room);
                position = FindBestGridPosition(spawnArea, player, gates, blockedMask);
            }

            plannedPositions.Add(position);
            GameObject instance = Instantiate(prefab, position, Quaternion.identity, room.transform);
            if (useSpawnEntrance && instance != null)
            {
                EnemySpawnEntrance entrance = instance.GetComponent<EnemySpawnEntrance>();
                if (entrance == null)
                    entrance = instance.AddComponent<EnemySpawnEntrance>();
                entrance.Configure(i * spawnEntranceStagger, spawnEntranceDuration, room);
            }
        }
    }

    private List<EnemyType> BuildSpawnPlan(int targetCount, float progress, int stageNumber, int roomNumber)
    {
        List<EnemyType> plan = new List<EnemyType>(targetCount);
        int mandatoryMelee = targetCount <= 3
            ? meleeMinimumSmallRoom
            : targetCount <= 6 ? meleeMinimumMediumRoom : meleeMinimumLargeRoom;
        mandatoryMelee = Mathf.Clamp(mandatoryMelee, 1, targetCount);

        EnemyType mandatoryType = ResolvePrefab(EnemyType.Melee) != null
            ? EnemyType.Melee
            : ResolveFallbackType();
        for (int i = 0; i < mandatoryMelee; i++)
            plan.Add(mandatoryType);

        int tankCount = 0;
        int bomberCount = 0;
        int rangedCount = 0;
        lastPickedType = EnemyType.Melee;
        bool firstCombat = stageNumber == 1 && roomNumber == 2;

        while (plan.Count < targetCount)
        {
            EnemyType picked = PickType(progress, firstCombat, plan.Count, targetCount, tankCount, bomberCount, rangedCount);
            GameObject prefab = ResolvePrefab(picked);
            if (prefab == null)
            {
                picked = ResolveFallbackType();
                prefab = ResolvePrefab(picked);
                if (prefab == null)
                    break;
            }

            plan.Add(picked);
            if (picked == EnemyType.Tank) tankCount++;
            if (picked == EnemyType.Bomber) bomberCount++;
            if (picked == EnemyType.Ranged) rangedCount++;
            lastPickedType = picked;
        }

        Shuffle(plan);
        return plan;
    }

    private EnemyType PickType(float progress, bool firstCombat, int currentCount, int targetCount, int tankCount, int bomberCount, int rangedCount)
    {
        float meleeWeight;
        float rangedWeight;
        float tankWeight;
        float bomberWeight;

        if (progress < 0.25f)
        {
            meleeWeight = 65f;
            rangedWeight = 25f;
            tankWeight = 0f;
            bomberWeight = 10f;
        }
        else if (progress < 0.65f)
        {
            meleeWeight = 45f;
            rangedWeight = 25f;
            tankWeight = 15f;
            bomberWeight = 15f;
        }
        else
        {
            meleeWeight = 35f;
            rangedWeight = 25f;
            tankWeight = 20f;
            bomberWeight = 20f;
        }

        if (firstCombat && excludeTankFromFirstCombat) tankWeight = 0f;
        if (firstCombat && excludeBomberFromFirstCombat) bomberWeight = 0f;
        if (tankCount >= maxTanksPerWave) tankWeight = 0f;
        if (bomberCount >= maxBombersPerWave) bomberWeight = 0f;
        if (rangedCount >= Mathf.FloorToInt(targetCount * maxRangedRatio)) rangedWeight = 0f;

        if (lastPickedType.HasValue)
        {
            if (lastPickedType.Value == EnemyType.Ranged) rangedWeight *= 0.45f;
            if (lastPickedType.Value == EnemyType.Tank) tankWeight *= 0.25f;
            if (lastPickedType.Value == EnemyType.Bomber) bomberWeight *= 0.35f;
        }

        if (ResolvePrefab(EnemyType.Ranged) == null) rangedWeight = 0f;
        if (ResolvePrefab(EnemyType.Tank) == null) tankWeight = 0f;
        if (ResolvePrefab(EnemyType.Bomber) == null) bomberWeight = 0f;

        float total = meleeWeight + rangedWeight + tankWeight + bomberWeight;
        if (total <= 0f)
            return EnemyType.Melee;

        float roll = Random.Range(0f, total);
        if ((roll -= meleeWeight) <= 0f) return EnemyType.Melee;
        if ((roll -= rangedWeight) <= 0f) return EnemyType.Ranged;
        if ((roll -= tankWeight) <= 0f) return EnemyType.Tank;
        return EnemyType.Bomber;
    }

    private float CalculateProgress(int stageNumber, int roomNumber)
    {
        float stageProgress = Mathf.Max(0, stageNumber - 1) * 3f;
        float roomProgress = Mathf.Max(0, roomNumber - 2);
        return Mathf.Clamp01((stageProgress + roomProgress) / 9f);
    }

    private int DetermineTargetCount(float progress)
    {
        if (progress < 0.25f)
            return Random.Range(Mathf.Max(1, earlyMinEnemies), Mathf.Max(earlyMinEnemies, earlyMaxEnemies) + 1);
        if (progress < 0.65f)
            return Random.Range(Mathf.Max(1, midMinEnemies), Mathf.Max(midMinEnemies, midMaxEnemies) + 1);
        return Random.Range(Mathf.Max(1, lateMinEnemies), Mathf.Max(lateMinEnemies, lateMaxEnemies) + 1);
    }

    private bool TryFindSafePosition(BoxCollider2D area, Transform player, Transform[] gates, int blockedMask, out Vector2 position)
    {
        Bounds bounds = area.bounds;
        for (int attempt = 0; attempt < Mathf.Max(1, placementRetryCount); attempt++)
        {
            Vector2 candidate = new Vector2(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y));
            if (IsSafe(candidate, player, gates, blockedMask))
            {
                position = candidate;
                return true;
            }
        }
        position = bounds.center;
        return false;
    }

    private Vector2 FindBestGridPosition(BoxCollider2D area, Transform player, Transform[] gates, int blockedMask)
    {
        Bounds bounds = area.bounds;
        Vector2 best = bounds.center;
        float bestScore = float.MinValue;
        const int grid = 7;
        for (int x = 0; x < grid; x++)
        {
            for (int y = 0; y < grid; y++)
            {
                Vector2 candidate = new Vector2(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, (x + 0.5f) / grid),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, (y + 0.5f) / grid));
                if (Physics2D.OverlapCircle(candidate, placementCheckRadius, blockedMask) != null)
                    continue;

                float score = player != null ? Vector2.Distance(candidate, player.position) : 0f;
                for (int i = 0; i < plannedPositions.Count; i++)
                    score += Mathf.Min(2f, Vector2.Distance(candidate, plannedPositions[i]));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
        }
        return best;
    }

    private bool IsSafe(Vector2 candidate, Transform player, Transform[] gates, int blockedMask)
    {
        if (player != null && Vector2.Distance(candidate, player.position) < minimumPlayerDistance)
            return false;
        for (int i = 0; i < gates.Length; i++)
            if (gates[i] != null && Vector2.Distance(candidate, gates[i].position) < minimumGateDistance)
                return false;
        for (int i = 0; i < plannedPositions.Count; i++)
            if (Vector2.Distance(candidate, plannedPositions[i]) < minimumEnemySpacing)
                return false;
        return Physics2D.OverlapCircle(candidate, placementCheckRadius, blockedMask) == null;
    }

    private Transform FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }

    private Transform[] CollectGateTransforms(RoomController room)
    {
        GateTrigger[] triggers = room.GetComponentsInChildren<GateTrigger>(true);
        Transform[] result = new Transform[triggers.Length];
        for (int i = 0; i < triggers.Length; i++)
            result[i] = triggers[i] != null ? triggers[i].transform : null;
        return result;
    }


    private GameObject ResolvePrefab(EnemyType type)
    {
        if (weightConfig != null)
        {
            GameObject configured = weightConfig.GetPrefab(type);
            if (configured != null)
                return configured;
        }

        string resourceName;
        switch (type)
        {
            case EnemyType.Ranged:
                resourceName = "EnemyV611_Ranged";
                break;
            case EnemyType.Tank:
                resourceName = "EnemyV611_Tank";
                break;
            case EnemyType.Bomber:
                resourceName = "EnemyV611_Bomber";
                break;
            default:
                resourceName = "EnemyV611_Melee";
                break;
        }

        return Resources.Load<GameObject>("Enemies/" + resourceName);
    }

    private bool TryGetAvailableFallback(out EnemyType type)
    {
        EnemyType[] priority =
        {
            EnemyType.Melee,
            EnemyType.Ranged,
            EnemyType.Bomber,
            EnemyType.Tank
        };

        for (int i = 0; i < priority.Length; i++)
        {
            if (ResolvePrefab(priority[i]) != null)
            {
                type = priority[i];
                return true;
            }
        }

        type = EnemyType.Melee;
        return false;
    }

    private EnemyType ResolveFallbackType()
    {
        EnemyType type;
        return TryGetAvailableFallback(out type) ? type : EnemyType.Melee;
    }

    private static void Shuffle(List<EnemyType> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            EnemyType temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
