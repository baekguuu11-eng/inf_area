using System.Collections.Generic;
using UnityEngine;
public partial class ChipSlotManager
{
    private readonly HashSet<RoomController> visitedRoomsV22=new HashSet<RoomController>();
    private int byteFifthsV22;
    public void VisitRoomV22(RoomController room)
    {
        if(room==null || !visitedRoomsV22.Add(room)) return;
        if(visitedRoomsV22.Count==1 || !IsChipEquipped(ChipType.RoomRepair)) return;
        PlayerHealth hp=FindAnyObjectByType<PlayerHealth>();
        if(hp!=null && !hp.IsDead && hp.currentHealth<hp.maxHealth)
        { hp.Heal(1); CombatPostProcessV61.PulseHeal(); }
    }
    public int BonusBytesV22(int count)
    {
        if(!IsChipEquipped(ChipType.DataCollector)) return count;
        byteFifthsV22+=count;int extra=byteFifthsV22/5;byteFifthsV22%=5;return count+extra;
    }
    public void EnemyHitV22(EnemyHealth enemy,DamageContext context,bool boss)
    {
        if(boss || context.ChipSecondary || context.HitKind!=EnemyHitKind.Ranged || enemy.CurrentHealth<=0 || !IsChipEquipped(ChipType.SlowRounds)) return;
        EnemySlowStatusV14 slow=enemy.GetComponent<EnemySlowStatusV14>();
        if(slow==null) slow=enemy.gameObject.AddComponent<EnemySlowStatusV14>();slow.Apply(.75f,1f);
    }
    public void EnemyKilledV22(EnemyHealth enemy,DamageContext context,bool boss)
    {
        if(boss || context.ChipSecondary || (context.HitKind!=EnemyHitKind.Melee && context.HitKind!=EnemyHitKind.Ranged)) return;
        if(context.HitKind==EnemyHitKind.Melee && IsChipEquipped(ChipType.AmmoRecovery))
        {PlayerAmmoController ammo=FindAnyObjectByType<PlayerAmmoController>();if(ammo!=null) ammo.AddReserveAmmo(1);}
        if(!IsChipEquipped(ChipType.DeathBurst)) return;
        Vector2 origin=enemy.transform.position;
        ChipBurstVisualV22.Spawn(origin);
        HashSet<EnemyHealth> hit=new HashSet<EnemyHealth>();
        foreach(Collider2D col in Physics2D.OverlapCircleAll(origin,1f))
        {
            EnemyHealth target=col.GetComponentInParent<EnemyHealth>();
            if(target==null || target==enemy || target.IsDead || !hit.Add(target)) continue;
            DamageContext burst=new DamageContext(Mathf.Max(1,Mathf.RoundToInt(context.Damage*.4f)),(Vector2)target.transform.position-origin,target.transform.position,EnemyHitKind.Environment);
            burst.ChipSecondary=true; target.TryTakeDamage(burst);
        }
    }
}
