using UnityEngine;

public static class CombatPhysicsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureLayerCollisions()
    {
        int player = LayerMask.NameToLayer("Player");
        int enemy = LayerMask.NameToLayer("Enemy");
        int playerProjectile = LayerMask.NameToLayer("PlayerProjectile");
        int enemyProjectile = LayerMask.NameToLayer("EnemyProjectile");
        int pickup = LayerMask.NameToLayer("Pickup");
        int ui = LayerMask.NameToLayer("UI");

        Ignore(playerProjectile, playerProjectile, true);
        Ignore(playerProjectile, player, true);
        Ignore(playerProjectile, enemyProjectile, true);
        Ignore(playerProjectile, pickup, true);
        Ignore(playerProjectile, ui, true);

        Ignore(enemyProjectile, enemyProjectile, true);
        Ignore(enemyProjectile, enemy, true);
        Ignore(enemyProjectile, pickup, true);
        Ignore(enemyProjectile, ui, true);

        Ignore(pickup, enemy, true);
        Ignore(pickup, playerProjectile, true);
        Ignore(pickup, enemyProjectile, true);
    }

    private static void Ignore(int firstLayer, int secondLayer, bool ignore)
    {
        if (firstLayer < 0 || secondLayer < 0)
            return;
        Physics2D.IgnoreLayerCollision(firstLayer, secondLayer, ignore);
    }
}
