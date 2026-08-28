using UnityEngine;

// 특정 스테이지에서 각 적 타입이 뽑힐 상대적 확률(가중치).
// 값들의 합이 100일 필요는 없음 - 서로간의 "비율"로만 계산됨.
[System.Serializable]
public class StageSpawnRates
{
    [Tooltip("이 확률표가 적용되는 스테이지 번호")]
    public int stageNumber = 1;

    [Header("적 타입별 등장 확률 (상대값)")]
    public float meleeChance = 60f;   // 근거리
    public float rangedChance = 25f;  // 원거리
    public float tankChance = 10f;    // 탱
    public float bomberChance = 5f;   // 자폭

    public float GetChance(EnemyType type)
    {
        switch (type)
        {
            case EnemyType.Melee: return meleeChance;
            case EnemyType.Ranged: return rangedChance;
            case EnemyType.Tank: return tankChance;
            case EnemyType.Bomber: return bomberChance;
            default: return 0f;
        }
    }
}
