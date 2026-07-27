using System.Collections;
using UnityEngine;

public class GameFeelManager : MonoBehaviour
{
    public static GameFeelManager Instance;

    private Vector3 originalLocalPosition;
    private Coroutine shakeCoroutine;

    // [버그 수정] 히트스탑이 겹쳐서 호출될 때 Time.timeScale이 0에 고정되던 문제 방지용
    private int hitStopDepth = 0;
    private float timeScaleBeforeHitStop = 1f;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        originalLocalPosition = transform.localPosition;
    }

    public void DoHitStop(float duration)
    {
        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        // 여러 히트스탑이 겹쳐도 "제일 처음" timeScale만 기억해서 나중에 그걸로 복구함.
        // (기존 코드는 매 호출마다 '현재' timeScale을 저장했는데, 겹쳐서 호출되면
        //  이미 0으로 바뀐 값을 저장해버려서 나중에 0으로 영구 고정되는 버그가 있었음)
        if (hitStopDepth == 0)
            timeScaleBeforeHitStop = Time.timeScale;

        hitStopDepth++;
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(duration);

        hitStopDepth = Mathf.Max(0, hitStopDepth - 1);

        // 겹쳐 있던 히트스탑이 전부 끝났을 때만 원래 속도로 복구
        if (hitStopDepth == 0)
            Time.timeScale = timeScaleBeforeHitStop;
    }

    public void Shake(float duration, float magnitude)
    {
        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);

        shakeCoroutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            Vector2 offset2D = Random.insideUnitCircle * magnitude;
            transform.localPosition = originalLocalPosition + new Vector3(offset2D.x, offset2D.y, 0f);

            yield return null;
        }

        transform.localPosition = originalLocalPosition;
        shakeCoroutine = null;
    }
}