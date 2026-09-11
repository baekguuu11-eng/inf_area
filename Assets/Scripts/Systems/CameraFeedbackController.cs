using System.Collections;
using UnityEngine;

public enum CameraImpactLevelV11
{
    Small,
    Medium,
    Heavy,
    Boss
}

[DisallowMultipleComponent]
public class CameraFeedbackController : MonoBehaviour
{
    public static CameraFeedbackController Instance { get; private set; }

    [Header("Recoil")]
    [SerializeField] private float defaultRecoilStrength = 0.10f;
    [SerializeField] private float recoilReturnSpeed = 15f;
    [SerializeField] private float maxAccumulatedRecoil = 0.22f;

    [Header("Shake")]
    [SerializeField] private float maxShakeMagnitude = 0.24f;
    [SerializeField] private float shakeFrequency = 42f;

    [Header("Safe Hit Stop")]
    [SerializeField, Range(0.02f, 0.3f)] private float hitStopTimeScale = 0.08f;

    private Vector3 baseLocalPosition;
    private Vector3 lastAppliedOffset;
    private Vector2 recoilOffset;
    private float shakeRemaining;
    private float shakeMagnitude;
    private Vector2 directionalBias;

    private Coroutine hitStopRoutine;
    private bool ownsHitStop;
    private float hitStopRestoreScale = 1f;
    private float hitStopEndRealtime;

    private void Awake()
    {
        if (Instance == null || Instance == this || CompareTag("MainCamera"))
            Instance = this;

        baseLocalPosition = transform.localPosition;

        // 이전 버전의 중첩 히트스톱 버그가 남긴 값만 안전하게 복구합니다.
        if (Time.timeScale > 0f && Time.timeScale <= 0.0315f)
            Time.timeScale = 1f;
    }

    private void OnDisable()
    {
        RestoreOwnedHitStop();
    }

    private void OnDestroy()
    {
        RestoreOwnedHitStop();
        if (Instance == this)
            Instance = null;
    }

    public void SetBaseWorldPosition(Vector3 worldPosition, bool snap)
    {
        baseLocalPosition = transform.parent != null
            ? transform.parent.InverseTransformPoint(worldPosition)
            : worldPosition;

        if (snap)
        {
            recoilOffset = Vector2.zero;
            shakeRemaining = 0f;
            shakeMagnitude = 0f;
            directionalBias = Vector2.zero;
            lastAppliedOffset = Vector3.zero;
            transform.localPosition = baseLocalPosition;
        }
        else
        {
            transform.localPosition = baseLocalPosition + lastAppliedOffset;
        }
    }

    public void AddRecoil(Vector2 shootDirection)
    {
        AddRecoil(shootDirection, defaultRecoilStrength);
    }

    public void AddRecoil(Vector2 shootDirection, float strength)
    {
        if (shootDirection.sqrMagnitude < 0.0001f)
            return;

        recoilOffset += -shootDirection.normalized * Mathf.Max(0f, strength);
        if (recoilOffset.magnitude > maxAccumulatedRecoil)
            recoilOffset = recoilOffset.normalized * maxAccumulatedRecoil;
    }

    public void Shake(float duration, float magnitude)
    {
        Shake(duration, magnitude, Vector2.zero);
    }

    public void Shake(float duration, float magnitude, Vector2 direction)
    {
        shakeRemaining = Mathf.Max(shakeRemaining, Mathf.Max(0f, duration));
        shakeMagnitude = Mathf.Clamp(Mathf.Max(shakeMagnitude, magnitude), 0f, maxShakeMagnitude);
        if (direction.sqrMagnitude > 0.0001f)
            directionalBias = direction.normalized;
    }

    /// <summary>
    /// V11 공용 카메라 충격 등급. 무기/적/보스가 제각각 숫자를 쓰지 않고 네 단계 안에서 통일한다.
    /// </summary>
    public void Impact(CameraImpactLevelV11 level, Vector2 direction, bool withHitStop = false)
    {
        float duration;
        float magnitude;
        float stop;
        switch (level)
        {
            case CameraImpactLevelV11.Boss:
                duration = 0.22f; magnitude = 0.205f; stop = 0.040f; break;
            case CameraImpactLevelV11.Heavy:
                duration = 0.155f; magnitude = 0.135f; stop = 0.028f; break;
            case CameraImpactLevelV11.Medium:
                duration = 0.095f; magnitude = 0.075f; stop = 0.016f; break;
            default:
                duration = 0.050f; magnitude = 0.032f; stop = 0f; break;
        }

        Shake(duration, magnitude, direction);
        if (withHitStop && stop > 0f)
            HitStop(stop);
    }

    public void HitStop(float duration)
    {
        if (duration <= 0f || Time.timeScale <= 0f)
            return;

        float now = Time.realtimeSinceStartup;
        hitStopEndRealtime = Mathf.Max(hitStopEndRealtime, now + Mathf.Clamp(duration, 0.004f, 0.08f));

        if (!ownsHitStop)
        {
            ownsHitStop = true;
            hitStopRestoreScale = Mathf.Max(0.1f, Time.timeScale);
            Time.timeScale = Mathf.Min(Time.timeScale, hitStopTimeScale);
            hitStopRoutine = StartCoroutine(HitStopRoutine());
        }
        else if (Time.timeScale > hitStopTimeScale)
        {
            Time.timeScale = hitStopTimeScale;
        }
    }

    private IEnumerator HitStopRoutine()
    {
        while (ownsHitStop && Time.realtimeSinceStartup < hitStopEndRealtime)
            yield return null;

        RestoreOwnedHitStop();
    }

    private void RestoreOwnedHitStop()
    {
        if (!ownsHitStop)
            return;

        ownsHitStop = false;
        if (hitStopRoutine != null)
        {
            StopCoroutine(hitStopRoutine);
            hitStopRoutine = null;
        }

        if (Time.timeScale > 0f && Time.timeScale <= hitStopTimeScale + 0.005f)
            Time.timeScale = Mathf.Max(0.1f, hitStopRestoreScale);

        hitStopEndRealtime = 0f;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);

        Vector3 externallyMovedBase = transform.localPosition - lastAppliedOffset;
        if ((externallyMovedBase - baseLocalPosition).sqrMagnitude > 0.0000001f)
            baseLocalPosition = externallyMovedBase;

        recoilOffset = Vector2.MoveTowards(recoilOffset, Vector2.zero, recoilReturnSpeed * dt * Mathf.Max(0.025f, maxAccumulatedRecoil));

        Vector2 shakeOffset = Vector2.zero;
        if (shakeRemaining > 0f && shakeMagnitude > 0f)
        {
            float totalBefore = shakeRemaining;
            shakeRemaining = Mathf.Max(0f, shakeRemaining - dt);
            float envelope = Mathf.Clamp01(totalBefore / Mathf.Max(0.04f, totalBefore + dt));
            float seed = Time.unscaledTime * shakeFrequency;
            Vector2 noise = new Vector2(
                Mathf.PerlinNoise(seed, 11.3f) * 2f - 1f,
                Mathf.PerlinNoise(7.1f, seed) * 2f - 1f);

            if (noise.sqrMagnitude > 0.0001f)
                noise.Normalize();

            shakeOffset = noise * shakeMagnitude * Mathf.Max(0.30f, envelope);
            if (directionalBias.sqrMagnitude > 0.0001f)
                shakeOffset += directionalBias * shakeMagnitude * 0.38f * envelope;

            shakeMagnitude = Mathf.MoveTowards(shakeMagnitude, 0f, dt * maxShakeMagnitude * 7f);
        }
        else
        {
            shakeRemaining = 0f;
            shakeMagnitude = 0f;
            directionalBias = Vector2.zero;
        }

        Vector3 newOffset = new Vector3(recoilOffset.x + shakeOffset.x, recoilOffset.y + shakeOffset.y, 0f);
        transform.localPosition = baseLocalPosition + newOffset;
        lastAppliedOffset = newOffset;
    }
}
