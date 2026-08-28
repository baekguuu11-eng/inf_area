using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponAttackTimeline : MonoBehaviour
{
    public enum AttackPhase { Idle, Windup, Active, Recovery }
    public AttackPhase Phase { get; private set; } = AttackPhase.Idle;
    public float PhaseProgress { get; private set; }
    private int generation;

    public void Cancel()
    {
        generation++;
        Phase = AttackPhase.Idle;
        PhaseProgress = 0f;
    }

    public IEnumerator Play(float windup, float active, float recovery,
        Action<float> onWindup, Action onActiveBegin, Action<float> onActive,
        Action onRecoveryBegin, Action<float> onRecovery, Action onComplete,
        Func<bool> shouldCancel = null)
    {
        int token = ++generation;
        yield return RunPhase(token, AttackPhase.Windup, Mathf.Max(0f, windup), onWindup, shouldCancel);
        if (token != generation || (shouldCancel != null && shouldCancel())) yield break;
        Phase = AttackPhase.Active; PhaseProgress = 0f; onActiveBegin?.Invoke();
        yield return RunPhase(token, AttackPhase.Active, Mathf.Max(0.001f, active), onActive, shouldCancel);
        if (token != generation || (shouldCancel != null && shouldCancel())) yield break;
        Phase = AttackPhase.Recovery; PhaseProgress = 0f; onRecoveryBegin?.Invoke();
        yield return RunPhase(token, AttackPhase.Recovery, Mathf.Max(0f, recovery), onRecovery, shouldCancel);
        if (token != generation) yield break;
        Phase = AttackPhase.Idle; PhaseProgress = 0f; onComplete?.Invoke();
    }

    private IEnumerator RunPhase(int token, AttackPhase phase, float duration, Action<float> tick, Func<bool> shouldCancel)
    {
        Phase = phase; PhaseProgress = 0f;
        if (duration <= 0f) { tick?.Invoke(1f); yield break; }
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (token != generation || (shouldCancel != null && shouldCancel())) { Cancel(); yield break; }
            elapsed += Time.deltaTime;
            PhaseProgress = Mathf.Clamp01(elapsed / duration);
            tick?.Invoke(PhaseProgress);
            yield return null;
        }
        PhaseProgress = 1f;
    }
}
