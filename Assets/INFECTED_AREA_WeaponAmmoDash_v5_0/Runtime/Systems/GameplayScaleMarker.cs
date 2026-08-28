using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameplayScaleMarker : MonoBehaviour
{
    [SerializeField] private float appliedMultiplier = 1f;
    public float AppliedMultiplier => appliedMultiplier;

    public void MarkApplied(float multiplier)
    {
        appliedMultiplier = Mathf.Max(0.01f, multiplier);
    }
}
