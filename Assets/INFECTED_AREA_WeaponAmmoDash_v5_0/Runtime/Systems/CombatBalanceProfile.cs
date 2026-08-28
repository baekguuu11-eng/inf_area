using UnityEngine;
[CreateAssetMenu(menuName="INFECTED AREA/Combat Balance Profile",fileName="CombatBalanceProfile")]
public sealed class CombatBalanceProfile : ScriptableObject
{
    public int basicEnemyHealth=65;
    public int rangedEnemyHealth=55;
    public int bomberHealth=48;
    public int tankHealth=170;
    public float desiredBasicTtkMin=.8f;
    public float desiredBasicTtkMax=1.6f;
    public float desiredTankTtkMin=7f;
    public float desiredTankTtkMax=12f;
    [Range(.1f,1f)] public float rangedAmmoReturnRatio=.70f;
}
