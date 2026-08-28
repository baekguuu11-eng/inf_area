using UnityEngine;

public struct PlayerProjectileContext
{
    public WeaponDefinition weapon;
    public Vector2 direction;
    public Vector2 origin;
    public int damage;
    public float speed;
    public int pierce;

    public PlayerProjectileContext(WeaponDefinition weapon, Vector2 direction, Vector2 origin, int damage, float speed, int pierce)
    {
        this.weapon = weapon;
        this.direction = direction;
        this.origin = origin;
        this.damage = damage;
        this.speed = speed;
        this.pierce = pierce;
    }
}
