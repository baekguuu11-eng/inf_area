using System.Collections;
using UnityEngine;

public class CameraRecoil : MonoBehaviour
{
    [Header("반동 설정")]
    [SerializeField] private float recoilStrength = 0.3f; // 반동 세기 (화면이 밀려나는 거리)
    [SerializeField] private float returnSpeed = 12f;      // 원래 위치로 돌아오는 복귀 속도

    private Vector3 originalPosition;
    private bool isRecoiling = false;

    public void TriggerRecoil(Vector2 shootDirection)
    {
        // 연속으로 총을 쏠 때 원래 카메라 중심점(원래 위치)이 일그러지는 것을 방지합니다.
        // 첫 번째 반동이 시작될 때만 진짜 원래 위치를 저장합니다.
        if (!isRecoiling)
        {
            originalPosition = transform.position;
            isRecoiling = true;
        }

        // 기존에 돌고 있던 반동 코루틴이 있다면 정지하고 새로 시작 (연사 대응)
        StopAllCoroutines();
        StartCoroutine(RecoilRoutine(shootDirection));
    }

    private IEnumerator RecoilRoutine(Vector2 shootDirection)
    {
        // 쏜 방향의 '반대' 방향 계산 (카메라 Z축 고정을 위해 z는 0)
        Vector3 recoilDir = -new Vector3(shootDirection.x, shootDirection.y, 0f).normalized;

        // 순간적으로 쏜 방향 반대로 카메라 위치를 튕김
        transform.position += recoilDir * recoilStrength;

        // Lerp를 이용해 부드럽게 원래 고정되어 있던 방 중심 위치로 복귀
        while (Vector3.Distance(transform.position, originalPosition) > 0.001f)
        {
            transform.position = Vector3.Lerp(transform.position, originalPosition, Time.deltaTime * returnSpeed);
            yield return null;
        }

        // 복귀 완료 후 위치 정밀 고정 및 상태 리셋
        transform.position = originalPosition;
        isRecoiling = false;
    }
}