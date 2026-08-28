// TARGET: Assets/Scripts/Enemy/EnemyRole.cs
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyRole : MonoBehaviour
{
    public enum Role
    {
        Melee,
        Ranged,
        Tank,
        Bomber,
        Boss
    }

    [SerializeField] private Role role = Role.Melee;
    [SerializeField, Min(0.1f)] private float separationRadius = 0.85f;
    [SerializeField, Min(0f)] private float separationStrength = 1.5f;
    [SerializeField, Range(0f, 1f)] private float knockbackResistance = 0.1f;

    public Role CurrentRole => role;
    public float SeparationRadius => Mathf.Max(0.1f, separationRadius);
    public float SeparationStrength => Mathf.Max(0f, separationStrength);
    public float KnockbackResistance => Mathf.Clamp01(knockbackResistance);
}
