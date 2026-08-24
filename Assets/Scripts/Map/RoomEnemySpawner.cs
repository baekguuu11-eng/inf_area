using System.Collections.Generic;
using UnityEngine;

public class RoomEnemySpawner : MonoBehaviour
{
    [Header("Weight Config")]
    [Tooltip("적 타입별 가중치 비용/프리팹이 정의된 설정 (별도 스크립트: EnemyWeightConfig)")]
    [SerializeField] private EnemyWeightConfig weightConfig;

    [Header("Room Weight Range (방의 가중치 예산 범위)")]
    [SerializeField] private float baseMinWeight = 3f;
    [SerializeField] private float baseMaxWeight = 6f;

    [Header("난이도 증가치 (조정 가능)")]
    [SerializeField] private float proximityIncreasePerRoom = 1f;
    [SerializeField] private float stageIncreasePerStage = 2f;

    [Header("스테이지별 등장 확률")]
    [SerializeField]
    private List<StageSpawnRates> stageSpawnRates = new List<StageSpawnRates>
    {
        new StageSpawnRates
        {
            stageNumber = 1,
            meleeChance = 60f,
            rangedChance = 25f,
            tankChance = 10f,
            bomberChance = 5f,
        }
    };

    [Header("시작 방 예외")]
    [Tooltip("체크하면 각 스테이지의 1번 방(플레이어가 그 스테이지에 처음 들어서는 방)에는 적을 스폰하지 않음")]
    [SerializeField] private bool skipEnemySpawnInStartRoom = true;

    [Header("반복 등장 억제")]
    [SerializeField, Range(0.05f, 1f)] private float repeatPenaltyMultiplier = 0.5f;

    [Header("스폰 위치 (범위 안에서 랜덤)")]
    [Tooltip("스폰 지점이 벽 등 장애물과 겹치지 않는 자리를 찾기 위한 재시도 횟수")]
    [SerializeField] private int placementRetryCount = 10;
    [Tooltip("스폰 지점 주변에 이 반경 안에 장애물이 있으면 그 자리를 피함")]
    [SerializeField] private float placementCheckRadius = 0.2f;

    [Header("Safety")]
    [SerializeField] private int maxSpawnAttempts = 100;

    private EnemyType? lastSpawnedType = null;
    private int consecutiveStreak = 0;

    private void Awake()
    {
        if (weightConfig == null)
            Debug.LogWarning($"[{name}] Enemy Weight Config가 연결되어 있지 않습니다. 적이 스폰되지 않습니다.");

        if (baseMinWeight > baseMaxWeight)
            Debug.LogWarning($"[{name}] Base Min Weight({baseMinWeight})가 Base Max Weight({baseMaxWeight})보다 큽니다.");
    }

    public void SpawnEnemiesForRoom(RoomController room, int stageNumber)
    {
        if (room == null || weightConfig == null)
            return;

        if (room.IsPortalRoom)
            return;

        if (skipEnemySpawnInStartRoom && room.RoomNumber == 1)
            return;

        BoxCollider2D spawnArea = room.EnemySpawnArea;
        if (spawnArea == null)
        {
            Debug.LogWarning($"[{room.name}] Enemy Spawn Area가 설정되어 있지 않아 적을 스폰할 수 없습니다.");
            return;
        }

        float weightBudget = CalculateRoomWeightBudget(stageNumber, room.RoomNumber);
        StageSpawnRates rates = GetSpawnRatesForStage(stageNumber);

        lastSpawnedType = null;
        consecutiveStreak = 0;

        List<EnemyType> spawnPlan = BuildSpawnPlan(weightBudget, rates);

        int obstacleMask = 1 << LayerMask.NameToLayer("Obstacle");

        for (int i = 0; i < spawnPlan.Count; i++)
        {
            GameObject prefab = weightConfig.GetPrefab(spawnPlan[i]);
            if (prefab == null)
                continue;

            Vector2 spawnPos = GetRandomPointInArea(spawnArea, obstacleMask);
            Instantiate(prefab, spawnPos, Quaternion.identity, room.transform);
        }
    }

    // 스폰 범위 안에서 랜덤 좌표를 뽑되, 벽(Obstacle) 위에 겹치면 다른 자리를 재시도함.
    // 계속 실패하면 그냥 범위 중심에 스폰함 (완전히 실패하지는 않게 하는 안전장치).
    private Vector2 GetRandomPointInArea(BoxCollider2D area, int obstacleMask)
    {
        Bounds bounds = area.bounds;

        for (int attempt = 0; attempt < placementRetryCount; attempt++)
        {
            float x = Random.Range(bounds.min.x, bounds.max.x);
            float y = Random.Range(bounds.min.y, bounds.max.y);
            Vector2 point = new Vector2(x, y);

            Collider2D hit = Physics2D.OverlapCircle(point, placementCheckRadius, obstacleMask);
            if (hit == null)
                return point;
        }

        return bounds.center;
    }

    private float CalculateRoomWeightBudget(int stageNumber, int roomNumber)
    {
        float stageBonus = Mathf.Max(0, stageNumber - 1) * stageIncreasePerStage;
        float proximityBonus = Mathf.Max(0, roomNumber - 1) * proximityIncreasePerRoom;

        float min = baseMinWeight + stageBonus + proximityBonus;
        float max = baseMaxWeight;

        if (min > max)
            max = min;

        return Random.Range(min, max);
    }

    private StageSpawnRates GetSpawnRatesForStage(int stageNumber)
    {
        StageSpawnRates best = null;

        for (int i = 0; i < stageSpawnRates.Count; i++)
        {
            StageSpawnRates entry = stageSpawnRates[i];

            if (entry.stageNumber == stageNumber)
                return entry;

            if (entry.stageNumber < stageNumber && (best == null || entry.stageNumber > best.stageNumber))
                best = entry;
        }

        if (best != null)
            return best;

        return stageSpawnRates.Count > 0 ? stageSpawnRates[0] : new StageSpawnRates();
    }

    private List<EnemyType> BuildSpawnPlan(float weightBudget, StageSpawnRates rates)
    {
        List<EnemyType> plan = new List<EnemyType>();
        float remainingBudget = weightBudget;
        int attempts = 0;

        while (remainingBudget > 0f && attempts < maxSpawnAttempts)
        {
            attempts++;

            EnemyType? picked = PickWeightedType(rates, remainingBudget);
            if (picked == null)
                break;

            plan.Add(picked.Value);
            remainingBudget -= weightConfig.GetWeightCost(picked.Value);

            if (lastSpawnedType.HasValue && picked.Value == lastSpawnedType.Value)
                consecutiveStreak++;
            else
                consecutiveStreak = 1;

            lastSpawnedType = picked.Value;
        }

        return plan;
    }

    private EnemyType? PickWeightedType(StageSpawnRates rates, float remainingBudget)
    {
        List<EnemyType> candidateTypes = new List<EnemyType>();
        List<float> candidateWeights = new List<float>();

        System.Array enemyTypes = System.Enum.GetValues(typeof(EnemyType));
        foreach (EnemyType type in enemyTypes)
        {
            float baseChance = rates.GetChance(type);
            if (baseChance <= 0f)
                continue;

            float cost = weightConfig.GetWeightCost(type);
            if (cost > remainingBudget)
                continue;

            float finalChance = baseChance;

            if (lastSpawnedType.HasValue && lastSpawnedType.Value == type)
                finalChance *= Mathf.Pow(repeatPenaltyMultiplier, consecutiveStreak);

            candidateTypes.Add(type);
            candidateWeights.Add(finalChance);
        }

        if (candidateTypes.Count == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < candidateWeights.Count; i++)
            totalWeight += candidateWeights[i];

        if (totalWeight <= 0f)
            return null;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < candidateTypes.Count; i++)
        {
            cumulative += candidateWeights[i];
            if (roll <= cumulative)
                return candidateTypes[i];
        }

        return candidateTypes[candidateTypes.Count - 1];
    }
}
