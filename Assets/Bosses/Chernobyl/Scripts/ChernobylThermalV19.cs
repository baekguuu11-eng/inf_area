using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class ChernobylBossController
{
    private bool thermalVulnerableV19;
    private ChernobylAudioV21 audioV21;
    private readonly List<int> recentFamiliesV21=new List<int>();
    private float thermalHeatV19, thermalOpenV19, nextBlockedV19;
    private ChernobylThermalVisualV19 thermalVisualV19;
    private readonly List<int> thermalBagV19 = new List<int>();
    private int thermalBagPhaseV19, thermalLastV19 = -1;
    private void InitializeThermalV19()
    {
        thermalVisualV19 = gameObject.AddComponent<ChernobylThermalVisualV19>();
        thermalVisualV19.Initialize(visualRoot, coreRenderer);
    }
    private void ResetThermalV19()
    {
        thermalVulnerableV19 = false; thermalHeatV19 = thermalOpenV19 = 0f;
        thermalBagV19.Clear(); recentFamiliesV21.Clear(); thermalBagPhaseV19 = 0; thermalLastV19 = -1;
        SetThermalV19(0f, 0f, false);
    }
    private void SetThermalV19(float heat, float open, bool vulnerable)
    {
        thermalHeatV19 = heat; thermalOpenV19 = open; thermalVulnerableV19 = vulnerable;
        if (thermalVisualV19 != null) thermalVisualV19.SetState(heat, open, vulnerable);
    }
    public bool AllowDamageV19(DamageContext context)
    {
        if (suppressTransitions) return true;
        if (!dead && combatStarted && thermalVulnerableV19) return true;
        if (!dead && combatStarted && Time.time >= nextBlockedV19)
        {
            nextBlockedV19 = Time.time + 0.10f;
            if (thermalVisualV19 != null) thermalVisualV19.Blocked();
            ChernobylBossEffects.SpawnCoreSparks(context.HitPoint, new Color(0.75f,0.83f,0.88f), 3, 1.2f);
            if(audioV21!=null) audioV21.Blocked();
        }
        return false;
    }
    private static int PatternFamilyV21(int id) {return ChernobylPatternsV23.Family(id);}
    private int NextThermalPatternV19()
    {
        bool changed=thermalBagPhaseV19!=phase;
        int count=ChernobylPatternsV23.Count(phase);
        if(changed) {thermalBagV19.Clear();recentFamiliesV21.Clear();thermalBagPhaseV19=phase;}
        if(thermalBagV19.Count==0) for(int i=0;i<count;i++) thermalBagV19.Add(i);
        List<int> eligible=new List<int>();
        foreach(int id in thermalBagV19) if(!recentFamiliesV21.Contains(PatternFamilyV21(id))) eligible.Add(id);
        // Relax the oldest family only, preserving eventual coverage of every bag entry.
        while(eligible.Count==0 && recentFamiliesV21.Count>0)
        {
            recentFamiliesV21.RemoveAt(recentFamiliesV21.Count-1);
            foreach(int id in thermalBagV19) if(!recentFamiliesV21.Contains(PatternFamilyV21(id))) eligible.Add(id);
        }
        int chosen=changed?(phase==1?0:phase==2?8:13):eligible[Random.Range(0,eligible.Count)];
        thermalBagV19.Remove(chosen);thermalLastV19=chosen;
        recentFamiliesV21.Insert(0,PatternFamilyV21(chosen));
        if(recentFamiliesV21.Count>3) recentFamiliesV21.RemoveAt(3);
        return chosen;
    }
    private IEnumerator ThermalLoopV19()
    {
        ResetThermalV19();

        while (!dead && combatStarted && (playerHealth == null || !playerHealth.IsDead))
        {
            int cyclePhase=phase;
            yield return ExplosionCycleV20();
            CleanupSpawnedObjects(); ClearAllGridWarningsV11();
            if(dead || (playerHealth!=null && playerHealth.IsDead)) break;
            if(audioV21!=null) { audioV21.PlayNamed("HatchOpen",.42f); audioV21.Steam(true); }
            float elapsed=0f;
            while(elapsed<0.45f && !dead)
            {
                elapsed+=Time.deltaTime; SetThermalV19(1f,Mathf.Clamp01(elapsed/0.45f),false); yield return null;
            }
            SetThermalV19(1f,1f,true);
            yield return WaitCombatSeconds(cyclePhase==1?4.6f:cyclePhase==2?4.2f:3.7f);
            if(dead) yield break;
            int nextPhase=health.NormalizedHealth<=MeltdownRatio?3:health.NormalizedHealth<=PhaseTwoRatio?2:1;
            if(nextPhase>phase) yield return PhaseCutsceneV22(nextPhase);
            else
            {
                if(thermalVisualV19!=null) thermalVisualV19.WarnRestart(true);
                if(audioV21!=null) {audioV21.Steam(false);audioV21.PlayNamed("Restart",.48f);}
                yield return RestartCueV22(cyclePhase>=2);
                if(dead) yield break;
                SetThermalV19(0f,0f,false);
                if(thermalVisualV19!=null) thermalVisualV19.WarnRestart(false);
                if(cyclePhase>=2) yield return ThermalDischargeV19(cyclePhase);
            }
            if(dead) yield break;
            transitionRequested=meltdownRequested=false;
            // Next warning begins immediately; only the overheat interval is a rest.
        }
        SetThermalV19(0f,0f,false); CleanupSpawnedObjects(); ClearAllGridWarningsV11();
        ReleaseAntiBurstInputLockV11();
        if(audioV21!=null) audioV21.StopAll();
    }
    private IEnumerator ThermalPatternV19(int id) { yield return ExplosionCycleV20(); }
    private IEnumerator ThermalGapV19() { yield break; }
    private IEnumerator ThermalDischargeV19(int cyclePhase)
    {
        GameObject wave=new GameObject("CHN_ThermalDischargeV19"); RegisterSpawnedObject(wave);
        wave.transform.position=transform.position;
        LineRenderer ring=ChernobylRingV22.Create(wave.transform,.35f);
        float radius=0f, speed=cyclePhase==2?6.3f:7.65f;
        Bounds arena=GetArenaBounds(); float end=Vector2.Distance(arena.center,transform.position)+((Vector2)arena.extents).magnitude+0.5f;
        bool resolved=false; Coroutine push=null;
        PlaySfx(antiBurstFireSfxV11,0.7f,0.9f);
        while(radius<end && !dead)
        {
            float previous=radius; radius+=speed*Time.deltaTime;
            ChernobylRingV22.Draw(ring,radius,new Color(1f,.5f,.16f,.85f));
            if(!resolved && playerHealth!=null && !playerHealth.IsDead)
            {
                float distance=Vector2.Distance(playerHealth.transform.position,transform.position);
                if(distance>=previous-0.175f && distance<=radius+0.175f)
                {
                    resolved=true;
                    PlayerDashController dash=playerHealth.GetComponent<PlayerDashController>();
                    if(dash==null || !dash.IsInvulnerable)
                    {
                        playerHealth.TakeDamage(1);
                        if(!playerHealth.IsDead) push=StartCoroutine(ThermalPushV19(cyclePhase==2?2.8f:3.6f));
                    }
                }
            }
            yield return null;
        }
        ForgetAndDestroy(wave);
        if(push!=null) yield return push;
    }
    private IEnumerator ThermalPushV19(float distance)
    {
        yield return PushPlayerFromCoreV11(distance,0.25f);
        if(playerHealth!=null && !playerHealth.IsDead) playerHealth.GrantTemporaryInvulnerability(0.5f);
    }
}
