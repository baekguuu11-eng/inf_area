using System.Collections;
using UnityEngine;

public class CameraRecoil : MonoBehaviour
{
    [Header("Recoil Settings")]
    [SerializeField] private float recoilStrength = 0.25f; // 카메라 밀리는 거리
    [SerializeField] private float returnSpeed = 14f;       // 복귀 속도

    private Vector3 originalPosition;
    private bool isRecoiling = false;

    public void TriggerRecoil(Vector2 shootDirection)
    {
        if (!isRecoiling)
        {
            originalPosition = transform.position;
            isRecoiling = true;
        }

        StopAllCoroutines();
        StartCoroutine(RecoilRoutine(shootDirection));
    }

    private IEnumerator RecoilRoutine(Vector2 shootDirection)
    {
        Vector3 recoilVector = -new Vector3(shootDirection.x, shootDirection.y, 0f).normalized;
        transform.position += recoilVector * recoilStrength;

        while (Vector3.Distance(transform.position, originalPosition) > 0.001f)
        {
            transform.position = Vector3.Lerp(transform.position, originalPosition, Time.deltaTime * returnSpeed);
            yield return null;
        }

        transform.position = originalPosition;
        isRecoiling = false;
    }
}