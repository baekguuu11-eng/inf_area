using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyVisualMotion : MonoBehaviour
{
    public enum ActionState
    {
        None,
        Windup,
        Attack,
        Recovery,
        Arming,
        Spawn
    }

    [Header("참조")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer groundShadow;
    [SerializeField] private EnemyRole role;

    [Header("픽셀 움직임")]
    [SerializeField, Min(1f)] private float pixelsPerUnit = 64f;
    [SerializeField] private bool snapOffsetsToPixel = true;

    [Header("이동 생동감")]
    [SerializeField] private float bobPixels = 1.6f;
    [SerializeField] private float bobFrequency = 7.5f;
    [SerializeField] private float idleFloatPixels = 0.45f;
    [SerializeField] private float maxTiltDegrees = 4.5f;
    [SerializeField] private float moveSquash = 0.03f;
    [SerializeField] private float locomotionSmoothTime = 0.055f;

    [Header("딱딱한 피격 반응")]
    [SerializeField] private float hitKickPixels = 4.2f;
    [SerializeField] private float tankHitKickPixels = 2.1f;
    [SerializeField] private float hitKickDuration = 0.075f;
    [SerializeField] private float hitScaleAmount = 0.10f;

    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private Quaternion baseLocalRotation;
    private Rigidbody2D body;
    private Vector2 desiredVelocity;
    private Vector2 actionDirection = Vector2.down;
    private ActionState actionState;
    private float arming01;
    private float spawn01 = 1f;
    private float phaseOffset;

    private Vector3 smoothedLocomotionPosition;
    private Vector3 locomotionPositionVelocity;
    private Vector3 smoothedLocomotionScale = Vector3.one;
    private Vector3 locomotionScaleVelocity;
    private float currentRotation;
    private float rotationVelocity;
    private Vector3 shadowBaseScale = Vector3.one;

    private Vector2 hitDirection;
    private float hitStrength;
    private float hitStartTime = -999f;

    public Transform VisualRoot => visualRoot;

    public void RefreshBasePose()
    {
        if (visualRoot == null)
            visualRoot = FindVisualRoot();
        if (visualRoot == null)
            return;

        baseLocalPosition = visualRoot.localPosition;
        baseLocalScale = visualRoot.localScale;
        baseLocalRotation = visualRoot.localRotation;
        currentRotation = NormalizeAngle(baseLocalRotation.eulerAngles.z);
        smoothedLocomotionPosition = baseLocalPosition;
        smoothedLocomotionScale = baseLocalScale;
        locomotionPositionVelocity = Vector3.zero;
        locomotionScaleVelocity = Vector3.zero;
        rotationVelocity = 0f;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (visualRoot == null)
            visualRoot = FindVisualRoot();
        if (role == null)
            role = GetComponent<EnemyRole>();

        phaseOffset = Mathf.Abs(transform.position.x * 0.173f + transform.position.y * 0.317f + transform.GetSiblingIndex() * 0.619f);

        RefreshBasePose();

        if (groundShadow != null)
            shadowBaseScale = groundShadow.transform.localScale;
    }

    private Transform FindVisualRoot()
    {
        Transform named = transform.Find("VisualRoot");
        if (named != null)
            return named;

        SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.transform : transform;
    }

    public void SetDesiredVelocity(Vector2 velocity)
    {
        desiredVelocity = velocity;
    }

    public void SetAction(ActionState state, Vector2 direction)
    {
        actionState = state;
        if (direction.sqrMagnitude > 0.0001f)
            actionDirection = direction.normalized;
    }

    public void SetArming01(float value)
    {
        arming01 = Mathf.Clamp01(value);
        if (arming01 > 0f)
            actionState = ActionState.Arming;
    }

    public void SetSpawnProgress(float value)
    {
        spawn01 = Mathf.Clamp01(value);
        if (spawn01 < 0.999f)
            actionState = ActionState.Spawn;
    }

    public void PlayHit(Vector2 direction, float strength = 1f)
    {
        hitDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        hitStrength = Mathf.Max(hitStrength * 0.55f, Mathf.Max(0.25f, strength));
        hitStartTime = Time.unscaledTime;
    }

    private void LateUpdate()
    {
        if (visualRoot == null)
            return;

        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        Vector2 velocity = desiredVelocity;
        if (velocity.sqrMagnitude < 0.0004f && body != null)
            velocity = body.linearVelocity;

        float speed = velocity.magnitude;
        float move01 = Mathf.Clamp01(speed / 2.7f);
        float time = Time.unscaledTime;
        float movingWave = Mathf.Sin(time * bobFrequency + phaseOffset);
        float idleWave = Mathf.Sin(time * (bobFrequency * 0.43f) + phaseOffset * 1.7f);
        float roleBob = GetRoleBobMultiplier();
        float bob = movingWave * bobPixels * move01 * roleBob + idleWave * idleFloatPixels * (1f - move01);

        Vector2 locomotionPixels = new Vector2(0f, bob);
        Vector3 targetScaleMultiplier = Vector3.one;
        float normalizedX = speed > 0.001f ? velocity.normalized.x : 0f;
        float targetRotationOffset = -normalizedX * maxTiltDegrees * move01;

        float stepSquash = movingWave * moveSquash * move01;
        targetScaleMultiplier.x += stepSquash;
        targetScaleMultiplier.y -= stepSquash * 0.72f;

        switch (actionState)
        {
            case ActionState.Windup:
                locomotionPixels -= actionDirection * 2.2f;
                targetScaleMultiplier = Vector3.Scale(targetScaleMultiplier, new Vector3(1.06f, 0.90f, 1f));
                break;
            case ActionState.Attack:
                locomotionPixels += actionDirection * 3.0f;
                targetScaleMultiplier = Vector3.Scale(targetScaleMultiplier,
                    Mathf.Abs(actionDirection.x) >= Mathf.Abs(actionDirection.y)
                        ? new Vector3(1.15f, 0.89f, 1f)
                        : new Vector3(0.91f, 1.15f, 1f));
                break;
            case ActionState.Recovery:
                targetScaleMultiplier = Vector3.Scale(targetScaleMultiplier, new Vector3(0.96f, 1.04f, 1f));
                break;
            case ActionState.Arming:
                float jitter = Mathf.Sin(time * Mathf.Lerp(18f, 52f, arming01));
                locomotionPixels.x += jitter * Mathf.Lerp(0.6f, 2.6f, arming01);
                float inflate = Mathf.Lerp(1f, 1.18f, arming01);
                targetScaleMultiplier = new Vector3(inflate, inflate, 1f);
                targetRotationOffset += jitter * Mathf.Lerp(1f, 6f, arming01);
                break;
            case ActionState.Spawn:
                targetScaleMultiplier = new Vector3(Mathf.Lerp(0.82f, 1f, spawn01), Mathf.Lerp(0.64f, 1f, spawn01), 1f);
                locomotionPixels.y -= Mathf.Lerp(4f, 0f, spawn01);
                break;
        }

        Vector3 locomotionLocal = PixelsToLocal(locomotionPixels);
        if (snapOffsetsToPixel)
            locomotionLocal = SnapLocal(locomotionLocal);

        Vector3 targetLocomotionPosition = baseLocalPosition + locomotionLocal;
        Vector3 targetLocomotionScale = Vector3.Scale(baseLocalScale, targetScaleMultiplier);
        smoothedLocomotionPosition = Vector3.SmoothDamp(smoothedLocomotionPosition, targetLocomotionPosition, ref locomotionPositionVelocity, locomotionSmoothTime, Mathf.Infinity, dt);
        smoothedLocomotionScale = Vector3.SmoothDamp(smoothedLocomotionScale, targetLocomotionScale, ref locomotionScaleVelocity, locomotionSmoothTime, Mathf.Infinity, dt);

        float baseAngle = NormalizeAngle(baseLocalRotation.eulerAngles.z);
        float targetAngle = baseAngle + targetRotationOffset;
        currentRotation = Mathf.SmoothDampAngle(currentRotation, targetAngle, ref rotationVelocity, locomotionSmoothTime, Mathf.Infinity, dt);

        Vector3 hitOffset = Vector3.zero;
        Vector3 hitScale = Vector3.one;
        float hitAge = Time.unscaledTime - hitStartTime;
        if (hitAge >= 0f && hitAge < hitKickDuration)
        {
            float t = Mathf.Clamp01(hitAge / Mathf.Max(0.001f, hitKickDuration));
            float stepped;
            if (t < 0.32f) stepped = 1f;
            else if (t < 0.62f) stepped = 0.48f;
            else if (t < 0.84f) stepped = 0.18f;
            else stepped = 0f;

            float pixels = role != null && role.CurrentRole == EnemyRole.Role.Tank ? tankHitKickPixels : hitKickPixels;
            hitOffset = PixelsToLocal(hitDirection * pixels * hitStrength * stepped);
            if (snapOffsetsToPixel)
                hitOffset = SnapLocal(hitOffset);

            float scaleKick = hitScaleAmount * hitStrength * stepped;
            hitScale = new Vector3(1f + scaleKick, 1f - scaleKick * 0.72f, 1f);
        }
        else if (hitAge >= hitKickDuration)
        {
            hitStrength = 0f;
        }

        visualRoot.localPosition = smoothedLocomotionPosition + hitOffset;
        visualRoot.localScale = Vector3.Scale(smoothedLocomotionScale, hitScale);
        visualRoot.localRotation = Quaternion.Euler(0f, 0f, currentRotation);

        UpdateShadow(move01, bob);
    }

    private float GetRoleBobMultiplier()
    {
        if (role == null)
            return 1f;
        switch (role.CurrentRole)
        {
            case EnemyRole.Role.Tank: return 0.62f;
            case EnemyRole.Role.Bomber: return 1.35f;
            case EnemyRole.Role.Ranged: return 0.88f;
            default: return 1.05f;
        }
    }

    private Vector3 PixelsToLocal(Vector2 pixels)
    {
        float parentScaleX = visualRoot.parent != null ? Mathf.Max(0.0001f, Mathf.Abs(visualRoot.parent.lossyScale.x)) : 1f;
        float parentScaleY = visualRoot.parent != null ? Mathf.Max(0.0001f, Mathf.Abs(visualRoot.parent.lossyScale.y)) : 1f;
        return new Vector3(pixels.x / pixelsPerUnit / parentScaleX, pixels.y / pixelsPerUnit / parentScaleY, 0f);
    }

    private Vector3 SnapLocal(Vector3 value)
    {
        float parentScaleX = visualRoot.parent != null ? Mathf.Max(0.0001f, Mathf.Abs(visualRoot.parent.lossyScale.x)) : 1f;
        float parentScaleY = visualRoot.parent != null ? Mathf.Max(0.0001f, Mathf.Abs(visualRoot.parent.lossyScale.y)) : 1f;
        float stepX = 1f / pixelsPerUnit / parentScaleX;
        float stepY = 1f / pixelsPerUnit / parentScaleY;
        value.x = Mathf.Round(value.x / stepX) * stepX;
        value.y = Mathf.Round(value.y / stepY) * stepY;
        return value;
    }

    private void UpdateShadow(float move01, float bobPixelsValue)
    {
        if (groundShadow == null)
            return;

        float bob01 = Mathf.Abs(bobPixelsValue) / Mathf.Max(0.01f, bobPixels);
        float armingPulse = actionState == ActionState.Arming ? Mathf.Sin(Time.unscaledTime * 22f) * 0.10f * arming01 : 0f;
        float scaleMultiplier = 1f - bob01 * 0.10f + move01 * 0.04f + armingPulse;
        groundShadow.transform.localScale = new Vector3(
            Mathf.Sign(shadowBaseScale.x) * Mathf.Max(0.01f, Mathf.Abs(shadowBaseScale.x) * scaleMultiplier),
            Mathf.Sign(shadowBaseScale.y) * Mathf.Max(0.01f, Mathf.Abs(shadowBaseScale.y) * (1f - bob01 * 0.06f)),
            shadowBaseScale.z);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
}
