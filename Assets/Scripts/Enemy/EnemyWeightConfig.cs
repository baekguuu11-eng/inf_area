using UnityEngine;

// [요청하신 "가중치 시스템 별도 스크립트"]
// 적 타입별 가중치 비용 + 프리팹을 한 곳에 등록해두는 설정 자산.
// Project 창에서 우클릭 -> Create -> InfArea -> Enemy Weight Config 로 에셋을 하나 만들어서
// 필요한 방(RoomEnemySpawner)마다 그 하나를 공용으로 연결해서 쓰면 됨.
[CreateAssetMenu(fileName = "EnemyWeightConfig", menuName = "InfArea/Enemy Weight Config")]
public class EnemyWeightConfig : ScriptableObject
{
    [System.Serializable]
    public class EnemyEntry
    {
        public EnemyType type;

        [Tooltip("이 적 타입의 프리팹")]
        public GameObject prefab;

        [Tooltip("이 적 한 마리를 방에 배치하는 데 드는 가중치 비용")]
        public float weightCost = 1f;
    }

    [Tooltip("적 타입별 프리팹/가중치 비용. 예: 근거리1, 원거리2, 탱3, 자폭2")]
    public EnemyEntry[] enemies = new EnemyEntry[]
    {
        new EnemyEntry { type = EnemyType.Melee,  weightCost = 1f },
        new EnemyEntry { type = EnemyType.Ranged, weightCost = 2f },
        new EnemyEntry { type = EnemyType.Tank,   weightCost = 3f },
        new EnemyEntry { type = EnemyType.Bomber, weightCost = 2f },
    };

    public EnemyEntry GetEntry(EnemyType type)
    {
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i].type == type)
                return enemies[i];
        }

        return null;
    }

    public float GetWeightCost(EnemyType type)
    {
        EnemyEntry entry = GetEntry(type);
        return entry != null ? entry.weightCost : 1f;
    }

    public GameObject GetPrefab(EnemyType type)
    {
        EnemyEntry entry = GetEntry(type);
        return entry != null ? entry.prefab : null;
    }
}
