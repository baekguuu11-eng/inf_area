using System.Collections;
using UnityEngine;

public enum JHLPartMotionState
{
    Idle,
    Anticipation,
    Attack,
    Impact,
    Recovery,
    Disabled
}

/// <summary>
/// Heavy, non-gummy motion rig for JHL parts.
/// Position uses acceleration/braking and a stopping-distance solver instead of spring interpolation.
/// Rotation follows the same idea; scale is deliberately critically damped without overshoot.
/// </summary>
[DisallowMultipleComponent]
public sealed class JHLPartMotion : MonoBehaviour
{
    [SerializeField] private Transform visual;

    private Vector3 targetPosition;
    private Vector3 positionVelocity;
    private float targetRotation;
    private float angularVelocity;
    private Vector3 baseVisualScale = Vector3.one;
    private Vector3 targetScaleMultiplier = Vector3.one;

    private float maxSpeed = 28f;
    private float acceleration = 72f;
    private float brakeAcceleration = 95f;
    private float angularMaxSpeed = 180f;
    private float angularAcceleration = 520f;
    private float angularBrake = 700f;
    private float scaleSpeed = 5.8f;
    private bool initialized;
    private Rigidbody2D physicsBody;
    private JHLHandBarrier handBarrier;

    public Vector3 TargetPosition => targetPosition;
    public Vector3 Velocity => positionVelocity;
    public float AngularVelocity => angularVelocity;
    public JHLPartMotionState State { get; private set; } = JHLPartMotionState.Idle;
    public float DistanceToTarget => Vector2.Distance(transform.position, targetPosition);
    public bool IsSettled => DistanceToTarget <= 0.045f && positionVelocity.magnitude <= 0.16f && Mathf.Abs(angularVelocity) <= 2.5f;

    public void Initialize(Transform visualTransform, Vector3 startingPosition, Vector3 visualBaseScale)
    {
        visual = visualTransform;
        baseVisualScale = visualBaseScale;
        physicsBody = GetComponent<Rigidbody2D>();
        handBarrier = GetComponentInChildren<JHLHandBarrier>(true);
        initialized = true;
        Snap(startingPosition, 0f);
        SetScaleMultiplier(Vector3.one, true);
    }

    /// <summary>
    /// Compatibility profile used by the existing pattern code.
    /// frequency controls acceleration/response, damping controls braking strength, speedLimit is literal max speed.
    /// No hidden spring overshoot is introduced.
    /// </summary>
    public void SetMotionProfile(float frequency, float damping, float speedLimit)
    {
        float response = Mathf.InverseLerp(0.6f, 11f, Mathf.Clamp(frequency, 0.6f, 11f));
        float damp = Mathf.InverseLerp(0.35f, 1.8f, Mathf.Clamp(damping, 0.35f, 1.8f));
        maxSpeed = Mathf.Max(0.5f, speedLimit);
        acceleration = Mathf.Lerp(28f, 165f, response);
        brakeAcceleration = acceleration * Mathf.Lerp(0.92f, 1.55f, damp);
    }

    public void SetKinematicProfile(float speed, float accel, float brake)
    {
        maxSpeed = Mathf.Max(0.5f, speed);
        acceleration = Mathf.Max(1f, accel);
        brakeAcceleration = Mathf.Max(1f, brake);
    }

    public void SetRotationProfile(float frequency, float damping)
    {
        float response = Mathf.InverseLerp(0.6f, 12f, Mathf.Clamp(frequency, 0.6f, 12f));
        float damp = Mathf.InverseLerp(0.35f, 1.8f, Mathf.Clamp(damping, 0.35f, 1.8f));
        angularMaxSpeed = Mathf.Lerp(105f, 285f, response);
        angularAcceleration = Mathf.Lerp(260f, 920f, response);
        angularBrake = angularAcceleration * Mathf.Lerp(1.0f, 1.55f, damp);
    }

    public void SetScaleProfile(float frequency, float damping)
    {
        float response = Mathf.InverseLerp(0.6f, 14f, Mathf.Clamp(frequency, 0.6f, 14f));
        scaleSpeed = Mathf.Lerp(2.2f, 11.0f, response) * Mathf.Lerp(0.92f, 1.08f, Mathf.InverseLerp(0.35f, 1.8f, damping));
    }

    public void SetTarget(Vector3 worldPosition, JHLPartMotionState motionState)
    {
        targetPosition = worldPosition;
        targetPosition.z = transform.position.z;
        State = motionState;
    }

    public void SetRotation(float degrees, JHLPartMotionState motionState)
    {
        targetRotation = degrees;
        State = motionState;
    }

    public void SetBaseVisualScale(Vector3 newBaseScale, bool immediate = true)
    {
        baseVisualScale = new Vector3(
            Mathf.Max(0.05f, newBaseScale.x),
            Mathf.Max(0.05f, newBaseScale.y),
            Mathf.Max(0.05f, newBaseScale.z));
        if (immediate && visual != null)
            visual.localScale = Vector3.Scale(baseVisualScale, targetScaleMultiplier);
    }

    public void SetScaleMultiplier(Vector3 multiplier, bool immediate = false)
    {
        targetScaleMultiplier = new Vector3(
            Mathf.Max(0.05f, multiplier.x),
            Mathf.Max(0.05f, multiplier.y),
            Mathf.Max(0.05f, multiplier.z));

        if (immediate && visual != null)
            visual.localScale = Vector3.Scale(baseVisualScale, targetScaleMultiplier);
    }

    /// <summary>Explicit follow-through impulse. This is the only source of overshoot.</summary>
    public void AddVelocityImpulse(Vector2 impulse)
    {
        positionVelocity += (Vector3)impulse;
        float cap = maxSpeed * 1.08f;
        if (positionVelocity.magnitude > cap)
            positionVelocity = positionVelocity.normalized * cap;
    }

    public void AddAngularImpulse(float degreesPerSecond)
    {
        angularVelocity = Mathf.Clamp(angularVelocity + degreesPerSecond, -angularMaxSpeed * 1.1f, angularMaxSpeed * 1.1f);
    }

    public void Snap(Vector3 worldPosition, float rotation)
    {
        transform.position = worldPosition;
        transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        targetPosition = worldPosition;
        targetRotation = rotation;
        positionVelocity = Vector3.zero;
        angularVelocity = 0f;
        State = JHLPartMotionState.Idle;
    }

    public void StopMotion()
    {
        targetPosition = transform.position;
        targetRotation = transform.eulerAngles.z;
        positionVelocity = Vector3.zero;
        angularVelocity = 0f;
        State = JHLPartMotionState.Idle;
    }

    public IEnumerator WaitForSettle(float maxDuration, float distanceTolerance = 0.08f)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.02f, maxDuration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            if (DistanceToTarget <= distanceTolerance && positionVelocity.magnitude <= 0.25f)
                yield break;
            yield return null;
        }
    }

    private void Update()
    {
        if (!initialized || State == JHLPartMotionState.Disabled)
            return;

        float dt = Mathf.Min(0.0333f, Mathf.Max(0f, Time.deltaTime));
        if (dt <= 0f)
            return;

        // While a hand is intentionally acting as a physical wall, position/rotation are
        // advanced from FixedUpdate through Rigidbody2D.MovePosition/MoveRotation.
        // This avoids transform-driven kinematic colliders tunnelling or jittering.
        if (!UsesPhysicsBarrierStep())
        {
            StepPosition(dt, false);
            StepRotation(dt, false);
        }
        StepScale(dt);
    }

    private void FixedUpdate()
    {
        if (!initialized || State == JHLPartMotionState.Disabled || !UsesPhysicsBarrierStep())
            return;

        float dt = Mathf.Max(0.0001f, Time.fixedDeltaTime);
        StepPosition(dt, true);
        StepRotation(dt, true);
    }

    private bool UsesPhysicsBarrierStep()
    {
        return physicsBody != null && handBarrier != null && handBarrier.IsBarrierEnabled;
    }

    private void StepPosition(float dt, bool physicsStep)
    {
        Vector3 currentPosition = physicsStep && physicsBody != null ? (Vector3)physicsBody.position : transform.position;
        Vector3 toTarget = targetPosition - currentPosition;
        toTarget.z = 0f;
        float distance = toTarget.magnitude;

        if (distance <= 0.003f && positionVelocity.magnitude <= 0.05f)
        {
            if (physicsStep && physicsBody != null) physicsBody.MovePosition(targetPosition);
            else transform.position = targetPosition;
            positionVelocity = Vector3.zero;
            return;
        }

        Vector3 direction = distance > 0.0001f ? toTarget / distance : Vector3.zero;
        float stoppingSpeed = Mathf.Sqrt(Mathf.Max(0f, 2f * brakeAcceleration * distance));
        float desiredSpeed = Mathf.Min(maxSpeed, stoppingSpeed);
        Vector3 desiredVelocity = direction * desiredSpeed;

        bool braking = positionVelocity.sqrMagnitude > 0.01f &&
            (Vector3.Dot(positionVelocity.normalized, direction) < 0.35f || positionVelocity.magnitude > desiredSpeed + 0.25f);
        float accel = braking ? brakeAcceleration : acceleration;
        positionVelocity = Vector3.MoveTowards(positionVelocity, desiredVelocity, accel * dt);

        Vector3 before = currentPosition;
        Vector3 next = before + positionVelocity * dt;
        Vector3 beforeToTarget = targetPosition - before;
        Vector3 afterToTarget = targetPosition - next;
        if (Vector3.Dot(beforeToTarget, afterToTarget) <= 0f && beforeToTarget.sqrMagnitude <= Mathf.Max(0.04f, positionVelocity.sqrMagnitude * dt * dt * 1.6f))
        {
            if (physicsStep && physicsBody != null) physicsBody.MovePosition(targetPosition);
            else transform.position = targetPosition;
            positionVelocity *= 0.18f;
        }
        else
        {
            if (physicsStep && physicsBody != null) physicsBody.MovePosition(next);
            else transform.position = next;
        }
    }

    private void StepRotation(float dt, bool physicsStep)
    {
        float current = physicsStep && physicsBody != null ? physicsBody.rotation : transform.eulerAngles.z;
        float delta = Mathf.DeltaAngle(current, targetRotation);
        if (Mathf.Abs(delta) <= 0.05f && Mathf.Abs(angularVelocity) <= 0.5f)
        {
            if (physicsStep && physicsBody != null) physicsBody.MoveRotation(targetRotation);
            else transform.rotation = Quaternion.Euler(0f, 0f, targetRotation);
            angularVelocity = 0f;
            return;
        }

        float sign = Mathf.Sign(delta);
        float stoppingSpeed = Mathf.Sqrt(Mathf.Max(0f, 2f * angularBrake * Mathf.Abs(delta)));
        float desired = sign * Mathf.Min(angularMaxSpeed, stoppingSpeed);
        bool braking = Mathf.Abs(angularVelocity) > Mathf.Abs(desired) + 1f || Mathf.Sign(angularVelocity) != sign;
        angularVelocity = Mathf.MoveTowards(angularVelocity, desired, (braking ? angularBrake : angularAcceleration) * dt);
        float next = current + angularVelocity * dt;
        if (Mathf.Sign(Mathf.DeltaAngle(next, targetRotation)) != sign && Mathf.Abs(delta) < Mathf.Abs(angularVelocity * dt) * 1.4f)
        {
            next = targetRotation;
            angularVelocity *= 0.16f;
        }
        if (physicsStep && physicsBody != null) physicsBody.MoveRotation(next);
        else transform.rotation = Quaternion.Euler(0f, 0f, next);
    }

    private void StepScale(float dt)
    {
        if (visual == null)
            return;
        Vector3 target = Vector3.Scale(baseVisualScale, targetScaleMultiplier);
        visual.localScale = Vector3.MoveTowards(visual.localScale, target, scaleSpeed * dt * Mathf.Max(1f, baseVisualScale.magnitude * 0.33f));
    }
}
